// Kiểm số liệu chương trình chính của khóa luận (.tex 27/11, 04-thuc-nghiem.tex) từ bảng từng khách của 360 lượt.
//
// Đầu vào (ra bởi src/adapter/riders.py ở đầu nhánh, hai nhánh chính mặc định):
//   riders.tsv     --out        (role, cell, requestId, lifecycle, t0, p0, D0, A, served, sumAbsC, nonzeroC, netC, maxAbsC, shared)
//   sequences.tsv  --sequences  (role, cell, requestId, dropSequence = các giờ trả đã công bố, bỏ lần công bố lúc trả)
// Tùy chọn:
//   --pred <tsv>         dự đoán mô phỏng nhanh F14 + F16 nối lại (cột aSH = nhóm Shadow; 0 = hạn mức 0), cho 04:320
//   --full-common <json> full_common.json của src/fastsim/adapter60/full_common.py (sai số theo thời gian; đối chiếu chéo)
//
// Định nghĩa theo 04-thuc-nghiem.tex mục 5.1.3 và full_common.py: chỉ khách hoàn tất (served = 1); đổi qua lại = có cả
// dời muộn và dời sớm ≥ ngưỡng, mỗi lần so với giờ báo liền trước; tổng mức đổi giờ = ngưỡng 5 s, mốc là giờ báo ghi
// nhận gần nhất; gộp theo ngày (đếm: tổng ba mẫu chia 3, cộng hai khung; tỷ lệ: cộng trong ngày rồi chia); hiệu = kết
// hợp − đối chứng cùng ngày; khoảng tin cậy 95 % theo t(29).
//
// usage: node kiem_chay_chinh.js <riders.tsv> <sequences.tsv | -> [--pred <tsv>] [--full-common <json>]
// In ra từng dòng "OK | KHÁC | THÔNG TIN", nhãn, số trong khóa luận, số tính lại, vị trí .tex. Dòng cuối: tổng OK/KHÁC.
'use strict';
const fs = require('fs');

const args = process.argv.slice(2);
const opt = (name) => { const i = args.indexOf(name); if (i < 0) return null; const v = args[i + 1]; args.splice(i, 2); return v; };
const predFile = opt('--pred'), fullCommonFile = opt('--full-common');
const [ridersFile, seqFile] = args;
if (!ridersFile || !seqFile) { console.error('usage: node kiem_chay_chinh.js <riders.tsv> <sequences.tsv> [--pred <tsv>] [--full-common <json>]'); process.exit(2); }

const CTL = 'FS-G2-SHHZ71-N', FULL = 'FS-G2-SHRC71-CL';
const CELL = /^d(\d{8})-vf-r([012])-(w08|w17)$/;
const T975_29 = 2.0452296;

function readTsv(file, onRow) {
  const text = fs.readFileSync(file, 'utf8');
  let start = 0, head = null;
  while (start < text.length) {
    let end = text.indexOf('\n', start);
    if (end < 0) end = text.length;
    const line = text.slice(start, end).replace(/\r$/, '');
    start = end + 1;
    if (!line) continue;
    const f = line.split('\t');
    if (!head) { head = f; continue; }
    const r = {};
    for (let i = 0; i < head.length; i++) r[head[i]] = f[i];
    onRow(r);
  }
}

// ---------------------------------------------------------------- đọc
const seq = new Map();   // role|cell|requestId -> [giờ trả đã công bố]
const haveSeq = seqFile !== '-';   // '-' = không có chuỗi giờ báo: bỏ qua các chỉ số tính từ chuỗi
if (haveSeq) readTsv(seqFile, (r) => seq.set(`${r.role}|${r.cell}|${r.requestId}`, JSON.parse(r.dropSequence)));
const riders = { [CTL]: new Map(), [FULL]: new Map() };   // key day|sample|window|requestId -> rider
const cellsSeen = { [CTL]: new Set(), [FULL]: new Set() };
let otherRoles = 0, notServed = 0, noSeq = 0;
readTsv(ridersFile, (r) => {
  if (!(r.role in riders)) { otherRoles++; return; }
  const m = CELL.exec(r.cell);
  if (!m) throw new Error(`cell ${r.cell}`);
  cellsSeen[r.role].add(r.cell);
  if (r.served !== '1') { notServed++; return; }
  const s = haveSeq ? seq.get(`${r.role}|${r.cell}|${r.requestId}`) : null;
  if (haveSeq && !s) { noSeq++; return; }
  const p0 = Number(r.p0), A = Number(r.A);
  if (s && s[0] !== p0) throw new Error(`${r.role} ${r.cell} ${r.requestId}: sequence starts at ${s[0]}, p0 ${p0}`);
  riders[r.role].set(`${m[1]}|${m[2]}|${m[3]}|${r.requestId}`, {
    day: m[1], sample: m[2], win: m[3], cell: r.cell, requestId: r.requestId, p0, A, s,
    sumAbsC: Number(r.sumAbsC), netC: Number(r.netC), nonzeroC: Number(r.nonzeroC),
  });
});

// ---------------------------------------------------------------- chỉ số từng khách
function rev(s, thr) {
  let last = 0;
  for (let i = 1; i < s.length; i++) {
    const z = s[i] - s[i - 1];
    if (z !== 0 && Math.abs(z) >= thr) { const g = z > 0 ? 1 : -1; if (last !== 0 && g !== last) return 1; last = g; }
  }
  return 0;
}
function tv5(s) {
  let shown = s[0], tot = 0;
  for (let i = 1; i < s.length; i++) if (Math.abs(s[i] - shown) >= 5000) { tot += Math.abs(s[i] - shown); shown = s[i]; }
  return tot / 1000;
}
for (const role of [CTL, FULL]) for (const x of riders[role].values()) {
  x.late = x.A - x.p0 > 60000 ? 1 : 0;
  x.rev = x.s ? rev(x.s, 60000) : NaN; x.rev30 = x.s ? rev(x.s, 30000) : NaN; x.rev120 = x.s ? rev(x.s, 120000) : NaN;
  x.tv5 = x.s ? tv5(x.s) : NaN;
  x.initAbs = Math.abs(x.A - x.p0) / 60000; x.initS = (x.A - x.p0) / 60000;
}

// ---------------------------------------------------------------- gộp theo ngày
const COUNT = ['served', 'rev', 'rev30', 'rev120'], RATE = ['late'], MEAN = ['tv5', 'initAbs', 'initS'];
function perDay(role, keys, win) {
  const acc = new Map();
  for (const k of keys) {
    const x = riders[role].get(k);
    if (win && x.win !== win) continue;
    let a = acc.get(x.day);
    if (!a) acc.set(x.day, a = { n: 0, rev: 0, rev30: 0, rev120: 0, late: 0, tv5: 0, initAbs: 0, initS: 0 });
    a.n++; a.rev += x.rev; a.rev30 += x.rev30; a.rev120 += x.rev120; a.late += x.late;
    a.tv5 += x.tv5; a.initAbs += x.initAbs; a.initS += x.initS;
  }
  const out = new Map();
  for (const [d, a] of acc) out.set(d, {
    served: a.n / 3, rev: a.rev / 3, rev30: a.rev30 / 3, rev120: a.rev120 / 3, late: 1000 * a.late / a.n,
    tv5: a.tv5 / a.n, initAbs: a.initAbs / a.n, initS: a.initS / a.n,
  });
  return out;
}
const mean = (v) => v.reduce((s, x) => s + x, 0) / v.length;
const sd = (v) => { const m = mean(v); return Math.sqrt(v.reduce((s, x) => s + (x - m) ** 2, 0) / (v.length - 1)); };
function level(d, k) { return mean([...d.values()].map((v) => v[k])); }
function paired(df, dc, k) {
  const days = [...dc.keys()].sort();
  if (days.length !== 30 || [...df.keys()].sort().join() !== days.join()) throw new Error(`days ${days.length}`);
  const v = days.map((d) => df.get(d)[k] - dc.get(d)[k]);
  if (v.some(Number.isNaN)) return { m: NaN, lo: NaN, hi: NaN, up: NaN, down: NaN };
  const m = mean(v), hw = T975_29 * sd(v) / Math.sqrt(v.length);
  return { m, lo: m - hw, hi: m + hw, up: v.filter((x) => x > 0).length, down: v.filter((x) => x < 0).length };
}
const pooled = (role, keys, k, win) => { const v = [...keys].map((q) => riders[role].get(q)).filter((x) => !win || x.win === win); return mean(v.map((x) => x[k])); };

// ---------------------------------------------------------------- so với khóa luận
let ok = 0, bad = 0, skipped = 0;
const skip = (label, where) => { skipped++; console.log(`BỎ QUA\t${label}\t(cần chuỗi giờ báo)\t${where}`); };
const dec = (s) => (s.includes(',') ? s.split(',')[1].length : 0);
const num = (s) => Number(s.replace(/\./g, '').replace(',', '.').replace('−', '-').replace('+', ''));
const fmt = (x, d) => x.toFixed(d).replace('.', ',');
function check(label, thesis, got, where) {
  if (Number.isNaN(got)) return skip(label, where);
  const d = dec(thesis);
  const same = Math.abs(Number(got.toFixed(d)) - num(thesis)) < 1e-9;
  same ? ok++ : bad++;
  console.log(`${same ? 'OK   ' : 'KHÁC '}\t${label}\tkhóa luận ${thesis}\ttính lại ${fmt(got, Math.max(d, 2))} (${got})\t${where}`);
}
function checkInt(label, thesis, got, where) {
  if (Number.isNaN(got)) return skip(label, where);
  const same = num(thesis) === got;
  same ? ok++ : bad++;
  console.log(`${same ? 'OK   ' : 'KHÁC '}\t${label}\tkhóa luận ${thesis}\ttính lại ${got}\t${where}`);
}
function checkText(label, thesis, got, where) {
  if (got.includes('NaN')) return skip(label, where);
  const same = thesis === got;
  same ? ok++ : bad++;
  console.log(`${same ? 'OK   ' : 'KHÁC '}	${label}	khóa luận ${thesis}	tính lại ${got}	${where}`);
}
function checkCi(label, t, r, where) {   // t = [mean, lo, hi]
  check(label + ' hiệu', t[0], r.m, where); check(label + ' cận dưới', t[1], r.lo, where); check(label + ' cận trên', t[2], r.hi, where);
}
const info = (...a) => console.log('THÔNG TIN\t' + a.join('\t'));

info('đầu vào', `khách hoàn tất: đối chứng ${riders[CTL].size}, kết hợp ${riders[FULL].size}`, `ô: ${cellsSeen[CTL].size} + ${cellsSeen[FULL].size}`,
  `bỏ qua: nhánh khác ${otherRoles} dòng, chưa hoàn tất ${notServed}, thiếu chuỗi ${noSeq}`);
if (noSeq) { console.error('thiếu chuỗi giờ báo của khách hoàn tất'); process.exit(1); }
checkInt('số lượt mỗi cấu hình', '180', cellsSeen[CTL].size, '04:72');
checkInt('số lượt mỗi cấu hình (kết hợp)', '180', cellsSeen[FULL].size, '04:72');

const allC = new Set(riders[CTL].keys()), allF = new Set(riders[FULL].keys());
const common = new Set([...allC].filter((k) => allF.has(k)));
checkInt('khách hoàn tất đối chứng', '63.277', allC.size, '04:121 Bảng 5.3');
checkInt('khách hoàn tất kết hợp', '62.678', allF.size, '04:121 Bảng 5.3');
checkInt('khách chung', '56.330', common.size, '04:136');
check('khách chung / đối chứng (%)', '89,0', 100 * common.size / allC.size, '04:136');
check('khách chung / kết hợp (%)', '89,9', 100 * common.size / allF.size, '04:136');

// Bảng 5.2 (04:84-89) và các câu quanh nó
const dc = perDay(CTL, allC), df = perDay(FULL, allF);
const T52 = [
  ['served', 'khách phục vụ', '703,1', '696,4', ['−6,66', '−8,75', '−4,56'], '4/26'],
  ['rev', 'đổi qua lại 60 s', '45,8', '18,5', ['−27,34', '−31,38', '−23,31'], '0/30'],
  ['late', 'muộn > 1 phút /1.000', '245,2', '211,0', ['−34,23', '−40,15', '−28,32'], '1/29'],
  ['tv5', 'tổng mức đổi giờ s/khách', '230,4', '192,7', ['−37,71', '−41,40', '−34,03'], '0/30'],
];
for (const [k, label, c, f, ci, ud] of T52) {
  check(`Bảng 5.2 ${label}: đối chứng`, c, level(dc, k), '04:88-91');
  check(`Bảng 5.2 ${label}: kết hợp`, f, level(df, k), '04:88-91');
  const r = paired(df, dc, k);
  checkCi(`Bảng 5.2 ${label}:`, ci, r, '04:88-91');
  checkText(`Bảng 5.2 ${label}: số ngày cao/thấp`, ud, `${r.up}/${r.down}`, '04:88-91');
}
check('đổi qua lại giảm (%)', '59,6', 100 * (1 - level(df, 'rev') / level(dc, 'rev')), '04:77');
checkInt('đổi qua lại giảm ở mọi ngày', '30', paired(df, dc, 'rev').down, '04:77');
check('khách phục vụ giảm (%)', '0,95', 100 * (1 - level(df, 'served') / level(dc, 'served')), '04:77');

// theo khung giờ (04:96-100)
const w = {};
for (const win of ['w08', 'w17']) w[win] = { c: perDay(CTL, allC, win), f: perDay(FULL, allF, win) };
check('sáng: đổi qua lại hiệu', '−6,91', paired(w.w08.f, w.w08.c, 'rev').m, '04:98');
check('sáng: tổng mức đổi giờ hiệu', '−49,52', paired(w.w08.f, w.w08.c, 'tv5').m, '04:98');
check('chiều: đổi qua lại hiệu', '−20,43', paired(w.w17.f, w.w17.c, 'rev').m, '04:98');
check('chiều: tổng mức đổi giờ hiệu', '−26,70', paired(w.w17.f, w.w17.c, 'tv5').m, '04:98');
checkCi('sáng: muộn', ['−93,26', '−103,2', '−83,3'], paired(w.w08.f, w.w08.c, 'late'), '04:100');
checkCi('chiều: muộn', ['+21,83', '+13,6', '+30,0'], paired(w.w17.f, w.w17.c, 'late'), '04:100');
checkInt('chiều: muộn tăng ở mọi ngày', '30', paired(w.w17.f, w.w17.c, 'late').up, '04:100');

// sai số giờ báo đầu (04:100-102): gộp trên từng khách
check('sai số giờ báo đầu, đối chứng (gộp khách)', '3,05', pooled(CTL, allC, 'initAbs'), '04:102');
check('sai số giờ báo đầu, kết hợp (gộp khách)', '2,88', pooled(FULL, allF, 'initAbs'), '04:102');
for (const [win, c, f, where] of [['w08', '3,10', '2,57', '04:104 sáng'], ['w17', '3,00', '3,18', '04:104 chiều']]) {
  check(`sai số giờ báo đầu ${where}, đối chứng (gộp khách)`, c, pooled(CTL, allC, 'initAbs', win), where);
  check(`sai số giờ báo đầu ${where}, kết hợp (gộp khách)`, f, pooled(FULL, allF, 'initAbs', win), where);
  info(`sai số giờ báo đầu ${where}, trung bình theo ngày`, fmt(level(w[win].c, 'initAbs'), 3), fmt(level(w[win].f, 'initAbs'), 3));
}

// Bảng 5.3 (04:116-130)
const cc = perDay(CTL, common), fc = perDay(FULL, common);
checkCi('Bảng 5.3 mọi khách: đổi qua lại', ['−27,34', '−31,38', '−23,31'], paired(df, dc, 'rev'), '04:123');
checkCi('Bảng 5.3 mọi khách: muộn', ['−34,23', '−40,15', '−28,32'], paired(df, dc, 'late'), '04:124');
checkCi('Bảng 5.3 mọi khách: tổng mức đổi giờ', ['−37,71', '−41,40', '−34,03'], paired(df, dc, 'tv5'), '04:125');
checkCi('Bảng 5.3 mọi khách: sai số giờ báo đầu', ['−0,17', '−0,22', '−0,11'], paired(df, dc, 'initAbs'), '04:126');
checkCi('Bảng 5.3 khách chung: đổi qua lại', ['−24,59', '−28,17', '−21,00'], paired(fc, cc, 'rev'), '04:123');
checkCi('Bảng 5.3 khách chung: muộn', ['−34,02', '−39,63', '−28,42'], paired(fc, cc, 'late'), '04:124');
checkCi('Bảng 5.3 khách chung: tổng mức đổi giờ', ['−40,04', '−43,82', '−36,25'], paired(fc, cc, 'tv5'), '04:125');
checkCi('Bảng 5.3 khách chung: sai số giờ báo đầu', ['−0,19', '−0,25', '−0,14'], paired(fc, cc, 'initAbs'), '04:126');
const wcc = perDay(CTL, common, 'w17'), wfc = perDay(FULL, common, 'w17');
checkCi('Bảng 5.3 chiều mọi khách: muộn', ['+21,83', '+13,64', '+30,03'], paired(w.w17.f, w.w17.c, 'late'), '04:129');
checkCi('Bảng 5.3 chiều mọi khách: sai số giờ báo đầu', ['+0,18', '+0,11', '+0,25'], paired(w.w17.f, w.w17.c, 'initAbs'), '04:130');
checkCi('Bảng 5.3 chiều khách chung: muộn', ['+20,84', '+13,36', '+28,32'], paired(wfc, wcc, 'late'), '04:129');
checkCi('Bảng 5.3 chiều khách chung: sai số giờ báo đầu', ['+0,17', '+0,10', '+0,23'], paired(wfc, wcc, 'initAbs'), '04:130');
info('Bảng 5.3 khách chung: hiệu số khách phục vụ (phải 0)', paired(fc, cc, 'served').m);

// 04:581 sai số có dấu buổi chiều, tách phần thay đổi ròng do điều phối
for (const [role, keys, net, rest] of [[CTL, allC, '2,77', '−0,54'], [FULL, allF, '2,94', '0,00']]) {
  const v = [...keys].map((k) => riders[role].get(k)).filter((x) => x.win === 'w17');
  check(`chiều ${role}: dời muộn ròng do điều phối (phút, gộp khách)`, net, mean(v.map((x) => x.netC / 60000)), '04:581');
  check(`chiều ${role}: sai số có dấu trừ phần ròng (phút, gộp khách)`, rest, mean(v.map((x) => (x.A - x.p0 - x.netC) / 60000)), '04:581');
  const d = perDay(role, keys, 'w17');
  info(`chiều ${role}: sai số có dấu A−p0 (gộp khách / theo ngày)`, fmt(mean(v.map((x) => x.initS)), 3), fmt(level(d, 'initS'), 3));
}

// 04:583 tỷ lệ của mức giảm tổng |c| đến từ dời sớm
const sums = (role, keys) => { let S = 0, E = 0; for (const k of keys) { const x = riders[role].get(k); S += x.sumAbsC; E += (x.sumAbsC - x.netC) / 2; } return { S, E }; };
const sc = sums(CTL, allC), sf = sums(FULL, allF);
check('phần dời sớm trong mức giảm tổng |c| (%)', '73,1', 100 * (sc.E - sf.E) / (sc.S - sf.S), '04:583');
info('số lần chuyển xe 3.033 → 757', 'không tính từ bảng từng khách; đã kiểm 18/11 (riders-compare: mọi lần chuyển; sổ cam kết 2.918 → 726 chỉ tính sau giờ báo đầu)');

// 04:705-714 ngưỡng 30 / 60 / 120 s
for (const [k, c, f, pct, where] of [['rev30', '79,60', '53,43', '32,9', '30 s'], ['rev', '45,84', '18,50', '59,6', '60 s'], ['rev120', '21,50', '5,49', '74,5', '120 s']]) {
  check(`ngưỡng ${where}: đối chứng (khách/ngày)`, c, level(dc, k), 'máy nhà 23/11');
  check(`ngưỡng ${where}: kết hợp (khách/ngày)`, f, level(df, k), 'máy nhà 23/11');
  check(`ngưỡng ${where}: mức giảm (%)`, pct, 100 * (1 - level(df, k) / level(dc, k)), '04:712');
  const r = paired(df, dc, k);
  checkInt(`ngưỡng ${where}: cận trên khoảng tin cậy < 0`, '1', Number.isNaN(r.hi) ? NaN : r.hi < 0 ? 1 : 0, '04:713');
}

// 04:320 nhóm hạn mức 0 theo phân loại Shadow của mô phỏng nhanh
if (predFile) {
  const cls = new Map();
  readTsv(predFile, (r) => cls.set(`${r.role}|${r.cell}|${r.requestId}`, r.aSH));
  for (const [role, keys, all, changed, where] of [[CTL, allC, '43.339', '2.854', '04:320'], [FULL, allF, '42.543', '0', '04:320']]) {
    let n = 0, ch = 0, miss = 0;
    for (const k of keys) {
      const x = riders[role].get(k), a = cls.get(`${role}|${x.cell}|${x.requestId}`);
      if (a === undefined) { miss++; continue; }
      if (Number(a) === 0) { n++; if (x.nonzeroC > 0) ch++; }
    }
    checkInt(`nhóm hạn mức 0 ${role}: số khách`, all, n, where);
    checkInt(`nhóm hạn mức 0 ${role}: có thay đổi do điều phối`, changed, ch, where);
    info(`nhóm hạn mức 0 ${role}`, `tỷ lệ ${fmt(100 * n / keys.size, 1)} % (khóa luận: khoảng 68 %)`, `không có trong dự đoán: ${miss}`);
  }
  check('nhóm hạn mức 0 đối chứng: tỷ lệ có thay đổi (%)', '6,59', 100 * 2854 / 43339, '04:320 (số học trên số khóa luận)');
} else info('04:320', 'bỏ qua: thiếu --pred');

// sai số theo thời gian: chỉ có từ full_common.json (cần thời điểm công bố)
if (fullCommonFile) {
  const fcj = JSON.parse(fs.readFileSync(fullCommonFile, 'utf8'));
  checkInt('full_common: khách đối chứng', '63.277', fcj.n.ctl, 'đối chiếu chéo');
  checkInt('full_common: khách kết hợp', '62.678', fcj.n.full, 'đối chiếu chéo');
  checkInt('full_common: khách chung', '56.330', fcj.n.common, 'đối chiếu chéo');
  check('sai số theo thời gian đối chứng (gộp khách)', '0,68', fcj.pooled.ctl_all.timed_abs, '04:102');
  check('sai số theo thời gian kết hợp (gộp khách)', '0,60', fcj.pooled.full_all.timed_abs, '04:102');
  for (const [win, c, f] of [['w08', '0,665', '0,520'], ['w17', '0,688', '0,671']]) {
    check(`sai số theo thời gian ${win} đối chứng (theo ngày)`, c, fcj.levels[win].ctl_all.timed_abs, '04:104');
    check(`sai số theo thời gian ${win} kết hợp (theo ngày)`, f, fcj.levels[win].full_all.timed_abs, '04:104');
  }
  const t = fcj.diff.w17.all.timed_abs;
  checkInt('chiều: hiệu sai số theo thời gian chưa rõ (khoảng tin cậy chứa 0)', '1', t.lo < 0 && t.hi > 0 ? 1 : 0, '04:104');
  const a = fcj.diff.day.all.timed_abs, c = fcj.diff.day.common.timed_abs;
  check('Bảng 5.3 mọi khách: sai số theo thời gian hiệu', '−0,080', a.mean, '04:127');
  check('Bảng 5.3 mọi khách: sai số theo thời gian cận dưới', '−0,097', a.lo, '04:127');
  check('Bảng 5.3 mọi khách: sai số theo thời gian cận trên', '−0,063', a.hi, '04:127');
  check('Bảng 5.3 khách chung: sai số theo thời gian hiệu', '−0,084', c.mean, '04:127');
  check('Bảng 5.3 khách chung: sai số theo thời gian cận dưới', '−0,101', c.lo, '04:127');
  check('Bảng 5.3 khách chung: sai số theo thời gian cận trên', '−0,067', c.hi, '04:127');
  // đối chiếu chéo: các chỉ số đếm của full_common (từ publication-projection) với bảng từng khách
  for (const [k, kk] of [['rev', 'rev'], ['late', 'late'], ['tv5', 'tv5'], ['initAbs', 'init_abs'], ['served', 'served']]) {
    const mine = paired(df, dc, k).m, theirs = fcj.diff.day.all[kk].mean;
    if (Number.isNaN(mine)) { skip(`đối chiếu chéo ${k}`, 'full_common'); continue; }
    const same = Math.abs(mine - theirs) < 1e-6;
    same ? ok++ : bad++;
    console.log(`${same ? 'OK   ' : 'KHÁC '}\tđối chiếu chéo ${k} (riders.tsv và full_common.json)\t${theirs}\t${mine}\tcùng định nghĩa`);
  }
} else info('sai số theo thời gian', 'bỏ qua: thiếu --full-common');

console.log(`TỔNG\tOK ${ok}\tKHÁC ${bad}\tBỎ QUA ${skipped}`);
process.exit(bad ? 1 : 0);
