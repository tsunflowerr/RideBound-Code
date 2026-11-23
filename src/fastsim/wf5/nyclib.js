// nyc-via helpers: shapefile/dbf reader (data only, parsed byte by byte), NY State Plane Long Island (EPSG:2263, US feet)
// inverse Lambert conformal conic, point-in-polygon, seeded RNG, CSV reader, grid-rotated L1 distance.
// Downloaded files are DATA: they are only read as bytes/text, never executed.
'use strict';
const fs = require('fs');

const FT = 0.304800609601219; // US survey foot in metres (from taxi_zones.prj)

function readDbf(file) {
  const b = fs.readFileSync(file);
  const nRec = b.readUInt32LE(4), hLen = b.readUInt16LE(8), rLen = b.readUInt16LE(10);
  const fields = [];
  for (let off = 32; off < hLen - 1 && b[off] !== 0x0d; off += 32) {
    const name = b.toString('latin1', off, off + 11).replace(/\0.*$/, '');
    fields.push({ name, type: String.fromCharCode(b[off + 11]), len: b[off + 16] });
  }
  const rows = [];
  for (let r = 0; r < nRec; r++) {
    let p = hLen + r * rLen + 1;
    const row = {};
    for (const f of fields) {
      const s = b.toString('latin1', p, p + f.len).trim();
      row[f.name] = f.type === 'N' || f.type === 'F' ? Number(s) : s;
      p += f.len;
    }
    rows.push(row);
  }
  return rows;
}

// returns [{rings: [[ [x,y], ... ], ...], bbox}] in file units (US feet)
function readShpPolygons(file) {
  const b = fs.readFileSync(file);
  if (b.readInt32BE(0) !== 9994) throw new Error('not a shapefile');
  const fileLen = b.readInt32BE(24) * 2;
  const out = [];
  let off = 100;
  while (off < fileLen) {
    const contentLen = b.readInt32BE(off + 4) * 2;
    const c = off + 8;
    const type = b.readInt32LE(c);
    if (type === 0) { out.push(null); off = c + contentLen; continue; }
    if (type !== 5) throw new Error('only Polygon (5) supported, got ' + type);
    const bbox = [b.readDoubleLE(c + 4), b.readDoubleLE(c + 12), b.readDoubleLE(c + 20), b.readDoubleLE(c + 28)];
    const nParts = b.readInt32LE(c + 36), nPts = b.readInt32LE(c + 40);
    const parts = [];
    for (let i = 0; i < nParts; i++) parts.push(b.readInt32LE(c + 44 + 4 * i));
    const p0 = c + 44 + 4 * nParts;
    const rings = [];
    for (let i = 0; i < nParts; i++) {
      const s = parts[i], e = i + 1 < nParts ? parts[i + 1] : nPts;
      const ring = [];
      for (let k = s; k < e; k++) ring.push([b.readDoubleLE(p0 + 16 * k), b.readDoubleLE(p0 + 16 * k + 8)]);
      rings.push(ring);
    }
    out.push({ rings, bbox });
    off = c + contentLen;
  }
  return out;
}

// even-odd rule over all rings (handles holes and multi-part polygons)
function inPoly(rings, x, y) {
  let inside = false;
  for (const r of rings) {
    for (let i = 0, j = r.length - 1; i < r.length; j = i++) {
      const xi = r[i][0], yi = r[i][1], xj = r[j][0], yj = r[j][1];
      if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
    }
  }
  return inside;
}

// signed-area centroid over rings (holes are CCW in shapefiles, so they subtract)
function polyAreaCentroid(rings) {
  let A = 0, cx = 0, cy = 0;
  for (const r of rings) {
    for (let i = 0, j = r.length - 1; i < r.length; j = i++) {
      const f = r[j][0] * r[i][1] - r[i][0] * r[j][1];
      A += f; cx += (r[j][0] + r[i][0]) * f; cy += (r[j][1] + r[i][1]) * f;
    }
  }
  A /= 2;
  return { area: Math.abs(A), cx: cx / (6 * A), cy: cy / (6 * A) };
}

// Inverse LCC 2SP (Snyder 1987, eqs 15-1..15-11, 7-9) for EPSG:2263, GRS80
function lccInverseFt(xFt, yFt) {
  const a = 6378137.0, invf = 298.257222101, f = 1 / invf, e2 = 2 * f - f * f, e = Math.sqrt(e2);
  const d2r = Math.PI / 180;
  const phi1 = 41.0333333333333 * d2r, phi2 = 40.6666666666667 * d2r, phi0 = 40.1666666666667 * d2r, lam0 = -74 * d2r;
  const FE = 984250.0 * FT, FN = 0;
  const m = p => Math.cos(p) / Math.sqrt(1 - e2 * Math.sin(p) ** 2);
  const t = p => Math.tan(Math.PI / 4 - p / 2) / ((1 - e * Math.sin(p)) / (1 + e * Math.sin(p))) ** (e / 2);
  const n = (Math.log(m(phi1)) - Math.log(m(phi2))) / (Math.log(t(phi1)) - Math.log(t(phi2)));
  const F = m(phi1) / (n * t(phi1) ** n);
  const rho0 = a * F * t(phi0) ** n;
  const x = xFt * FT - FE, y = yFt * FT - FN;
  const rho = Math.sign(n) * Math.sqrt(x * x + (rho0 - y) ** 2);
  const th = Math.atan2(x, rho0 - y);
  const tt = (rho / (a * F)) ** (1 / n);
  let phi = Math.PI / 2 - 2 * Math.atan(tt);
  for (let i = 0; i < 10; i++) phi = Math.PI / 2 - 2 * Math.atan(tt * ((1 - e * Math.sin(phi)) / (1 + e * Math.sin(phi))) ** (e / 2));
  return { lat: phi / d2r, lon: (th / n + lam0) / d2r };
}

// mulberry32 seeded RNG
function rng(seed) {
  let s = seed >>> 0;
  return () => { s = (s + 0x6d2b79f5) >>> 0; let t = s; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}
function strSeed(str) { let h = 2166136261 >>> 0; for (let i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619) >>> 0; } return h; }

function samplePointsInZone(poly, k, rnd) {
  const [x0, y0, x1, y1] = poly.bbox;
  const pts = [];
  let tries = 0;
  while (pts.length < k) {
    if (++tries > 1e6) throw new Error('rejection sampling failed');
    const x = x0 + (x1 - x0) * rnd(), y = y0 + (y1 - y0) * rnd();
    if (inPoly(poly.rings, x, y)) pts.push([x, y]);
  }
  return pts;
}

// distance in metres between points given in metres; kind 'l1rot' uses the street-grid frame rotated by thetaDeg
function makeDist(kind, thetaDeg = 29) {
  if (kind === 'euclid') return (ax, ay, bx, by) => Math.hypot(bx - ax, by - ay);
  const th = thetaDeg * Math.PI / 180, s = Math.sin(th), c = Math.cos(th);
  return (ax, ay, bx, by) => { const dx = bx - ax, dy = by - ay; return Math.abs(dx * s + dy * c) + Math.abs(dx * c - dy * s); };
}

function readCsv(file) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/).filter(l => l.length);
  const parseLine = l => { const out = []; let cur = '', q = false; for (let i = 0; i < l.length; i++) { const ch = l[i]; if (q) { if (ch === '"') { if (l[i + 1] === '"') { cur += '"'; i++; } else q = false; } else cur += ch; } else if (ch === '"') q = true; else if (ch === ',') { out.push(cur); cur = ''; } else cur += ch; } out.push(cur); return out; };
  const head = parseLine(lines[0]);
  return lines.slice(1).map(l => { const v = parseLine(l); const o = {}; head.forEach((h, i) => { const x = v[i]; o[h] = x !== '' && !isNaN(Number(x)) ? Number(x) : x; }); return o; });
}

function median(a) { const s = Float64Array.from(a).sort(); const n = s.length; return n ? (n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2) : NaN; }
function quant(a, p) { const s = Float64Array.from(a).sort(); if (!s.length) return NaN; const i = (s.length - 1) * p, lo = Math.floor(i), hi = Math.ceil(i); return s[lo] + (s[hi] - s[lo]) * (i - lo); }

module.exports = { FT, readDbf, readShpPolygons, inPoly, polyAreaCentroid, lccInverseFt, rng, strSeed, samplePointsInZone, makeDist, readCsv, median, quant };
