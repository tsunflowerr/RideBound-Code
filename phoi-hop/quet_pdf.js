// Quét dấu vết tải về trong một PDF trước khi đưa vào references/ (quy ước của [7.01]).
// Giải nén mọi luồng Flate rồi tìm trong byte thô, các luồng đã giải nén và (nếu có) văn bản trích ra:
// "downloaded by/from/on", "This work is downloaded", "for personal use only", địa chỉ IPv4, ngày tạo/sửa trong metadata,
// địa chỉ e-mail, và các từ trong một danh sách cấm (tùy chọn, mỗi dòng một từ).
// usage: node quet_pdf.js <file.pdf> [--text <file.txt>] [--forbidden <words.txt>]
// Kỳ vọng với bản nhà xuất bản: không có dòng "download", không có IP, ngày chỉ là ngày sản xuất của nhà xuất bản,
// e-mail chỉ của tác giả hoặc của lược đồ XMP/PRISM. "All rights reserved" trong font nhúng thì bỏ qua được.
'use strict';
const fs = require('fs');
const zlib = require('zlib');

const args = process.argv.slice(2);
const opt = (name) => { const i = args.indexOf(name); if (i < 0) return null; const v = args[i + 1]; args.splice(i, 2); return v; };
const txtFile = opt('--text'), forbFile = opt('--forbidden');
const [pdfFile] = args;
if (!pdfFile) { console.error('usage: node quet_pdf.js <file.pdf> [--text <file.txt>] [--forbidden <words.txt>]'); process.exit(2); }

const buf = fs.readFileSync(pdfFile);
const parts = [buf.toString('latin1')];
let pos = 0, streams = 0, inflated = 0;
for (;;) {
  const s = buf.indexOf('stream', pos, 'latin1');
  if (s < 0) break;
  let a = s + 6;
  if (buf[a] === 0x0d) a++;
  if (buf[a] === 0x0a) a++;
  const e = buf.indexOf('endstream', a, 'latin1');
  if (e < 0) break;
  streams++;
  const body = buf.subarray(a, e);
  try { parts.push(zlib.inflateSync(body).toString('latin1')); inflated++; } catch {
    try { parts.push(zlib.inflateRawSync(body).toString('latin1')); inflated++; } catch { /* not Flate */ }
  }
  pos = e + 9;
}
if (txtFile) parts.push(fs.readFileSync(txtFile, 'utf8'));
const all = parts.join('\n');
const ctx = (i, len) => all.slice(Math.max(0, i - 60), i + len + 60).replace(/[^\x20-\x7e]/g, '.');

const hits = [];
if (forbFile) {
  const words = fs.readFileSync(forbFile, 'utf8').split(/\r?\n/).map((w) => w.trim()).filter((w) => w && !w.startsWith('#'));
  const low = all.toLowerCase();
  for (const w of words) { const i = low.indexOf(w.toLowerCase()); if (i >= 0) hits.push(['CẤM', '(một từ trong danh sách)', ctx(i, w.length)]); }
}
const checks = [
  ['TẢI VỀ', /download(ed)?\s+(by|from|on)|this work is downloaded|for personal use only/gi],
  ['IPv4', /\b(?:\d{1,3}\.){3}\d{1,3}\b/g],
  ['E-MAIL', /[\w.+-]+@[\w-]+\.[\w.-]+/g],
];
// ngày trong metadata: in ba ngày mới nhất (ngày tải về, nếu có, là ngày mới nhất)
const dates = new Map();
for (const m of all.matchAll(/(?:Creation|Mod)Date\s*\(D:(\d{4})(\d{2})(\d{2})|<xmp:(?:Create|Modify|Metadata)Date>(\d{4})-(\d{2})-(\d{2})/g)) {
  const d = m[1] ? `${m[1]}-${m[2]}-${m[3]}` : `${m[4]}-${m[5]}-${m[6]}`;
  if (!dates.has(d)) dates.set(d, ctx(m.index, m[0].length));
}
for (const d of [...dates.keys()].sort().slice(-3).reverse()) hits.push(['NGÀY MỚI NHẤT', d, dates.get(d)]);
for (const [tag, re] of checks) {
  const seen = new Set();
  for (const m of all.matchAll(re)) {
    if (seen.has(m[0]) || seen.size >= 15) continue;
    seen.add(m[0]);
    hits.push([tag, m[0], ctx(m.index, m[0].length)]);
  }
}
console.log(`${pdfFile}: ${buf.length} byte, ${streams} luồng, giải nén ${inflated}`);
for (const h of hits) console.log('  ' + h.join(' | '));
