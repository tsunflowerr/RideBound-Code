// Downloads the four FleetPy Manhattan network files that network.js reads (FleetPy, Zenodo record 15187906, CC BY 4.0)
// into a data root and checks them against expected/network.sha256. Then it builds the all-pairs table once
// (network.js writes the cache file) and checks the cache against the same list.
// Only those four members are read from the 409 MB zip on Zenodo, with HTTP range requests (about 0.2 MB in all).
// Files that are already there with the right sha256 are kept.
//
// Usage: node fetch_network.js <data root>          (the folder later given to run.js as --data)
// Check by hand: cd <data root> && sha256sum -c <this folder>/expected/network.sha256
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const zlib = require('zlib');
const NETWORK = require('./network.js');

const ZIP_URL = 'https://zenodo.org/records/15187906/files/FleetPy_Manhattan.zip?download=1';
const SUMS = path.join(__dirname, 'expected', 'network.sha256');
const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

// one HTTP range request on the zip: 'bytes=<first>-<last>' or 'bytes=-<n>' (the last n bytes)
async function zipBytes(range) {
  const res = await fetch(ZIP_URL, { headers: { Range: range, 'User-Agent': 'ridebound-fetch-network' } });
  if (res.status !== 206) throw new Error(`${ZIP_URL} (${range}): HTTP ${res.status}, expected 206 Partial Content`);
  return Buffer.from(await res.arrayBuffer());
}

// the zip's central directory: member name -> { method, size (compressed), offset (of its local header) }
async function zipMembers() {
  const tail = await zipBytes('bytes=-65557');      // the end record: 22 bytes and a comment of at most 65,535 bytes
  const end = tail.lastIndexOf(Buffer.from([0x50, 0x4b, 0x05, 0x06]));
  if (end < 0) throw new Error('zip: no end of central directory record');
  const size = tail.readUInt32LE(end + 12), offset = tail.readUInt32LE(end + 16);
  const dir = await zipBytes(`bytes=${offset}-${offset + size - 1}`);
  const members = new Map();
  for (let p = 0; p < dir.length;) {
    if (dir.readUInt32LE(p) !== 0x02014b50) throw new Error('zip: bad central directory entry');
    const nameLength = dir.readUInt16LE(p + 28);
    members.set(dir.toString('utf8', p + 46, p + 46 + nameLength),
      { method: dir.readUInt16LE(p + 10), size: dir.readUInt32LE(p + 20), offset: dir.readUInt32LE(p + 42) });
    p += 46 + nameLength + dir.readUInt16LE(p + 30) + dir.readUInt16LE(p + 32);
  }
  return members;
}

// one member's bytes: its local header says where the data starts (method 8 = deflate, 0 = stored)
async function zipMember(m) {
  const header = await zipBytes(`bytes=${m.offset}-${m.offset + 29}`);
  const start = m.offset + 30 + header.readUInt16LE(26) + header.readUInt16LE(28);
  const data = await zipBytes(`bytes=${start}-${start + m.size - 1}`);
  if (m.method === 8) return zlib.inflateRawSync(data);
  if (m.method === 0) return data;
  throw new Error('zip: compression method ' + m.method);
}

async function main(dataRoot) {
  if (!dataRoot) throw new Error('usage: node fetch_network.js <data root>');
  const sums = fs.readFileSync(SUMS, 'utf8').split(/\r?\n/).filter(Boolean).map(line => {
    const m = /^([0-9a-f]{64}) {2}(\S+)$/.exec(line);
    if (!m) throw new Error('bad line in expected/network.sha256: ' + line);
    return { sha: m[1], file: m[2] };
  });
  let members = null;
  for (const { sha, file } of sums.filter(s => s.file.endsWith('.csv'))) {
    const target = path.join(dataRoot, file);
    if (fs.existsSync(target) && sha256(fs.readFileSync(target)) === sha) { console.log('ok       ' + file); continue; }
    members = members || await zipMembers();
    // the zip member: FleetPy_Manhattan__networks__... is FleetPy_Manhattan/networks/...
    const name = path.basename(file).split('__').join('/');
    if (!members.has(name)) throw new Error('zip: no member ' + name);
    const bytes = await zipMember(members.get(name));
    if (sha256(bytes) !== sha) throw new Error(`${name}: sha256 ${sha256(bytes)}, expected ${sha}`);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, bytes);
    console.log('fetched  ' + file);
  }
  NETWORK.build(dataRoot);                    // computes and caches the all-pairs table if it is not there yet
  for (const { sha, file } of sums.filter(s => !s.file.endsWith('.csv'))) {
    const got = sha256(fs.readFileSync(path.join(dataRoot, file)));
    if (got !== sha) throw new Error(`${file}: sha256 ${got}, expected ${sha} (delete the file and run again)`);
    console.log('ok       ' + file);
  }
}

if (require.main === module) {
  main(process.argv[2]).catch(e => { console.error(String(e && e.message || e)); process.exit(1); });
}
module.exports = { main };
