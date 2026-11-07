// Copies the fixture cells that the section 9 checks read (data/cells.tsv: 22 cells and 2 world files, about 19 MB) out
// of commit ebca912 of this repository, where they were committed with the research (also public on branch
// research/final-real-runs-2026-11-06), and checks the sha256 of every file. Files already there with the right sha256
// are kept.
// Usage: node fetch_cells.js <cells dir> [<git repository that holds commit ebca912>]   (default: this repository)
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const { execFileSync } = require('child_process');

const COMMIT = 'ebca912223ba423e15d1ec5c8553b838023b8438';
const [outDir, repo = __dirname] = process.argv.slice(2);
if (!outDir) { console.error('usage: node fetch_cells.js <cells dir> [<git repository>]'); process.exit(2); }
const git = (args, stdio) => execFileSync('git', ['-C', repo, ...args], { maxBuffer: 1 << 26, stdio });
const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

try {
  git(['cat-file', '-e', COMMIT + '^{commit}'], 'ignore');
} catch {
  console.error(`commit ${COMMIT.slice(0, 7)} is not in ${repo}; fetch it first, for example with\n` +
    '  git fetch origin research/final-real-runs-2026-11-06   (or git fetch --unshallow in a shallow clone)');
  process.exit(3);
}
const rows = fs.readFileSync(path.join(__dirname, 'data', 'cells.tsv'), 'utf8').trim().split('\n').slice(1)
  .map(l => l.split('\t'));
let fetched = 0;
for (const [file, sha, source] of rows) {
  const dest = path.join(outDir, file);
  if (fs.existsSync(dest) && sha256(fs.readFileSync(dest)) === sha) continue;
  const bytes = git(['cat-file', 'blob', COMMIT + ':' + source]);
  if (sha256(bytes) !== sha) throw new Error(source + ': sha256 differs from data/cells.tsv');
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  fs.writeFileSync(dest, bytes);
  fetched++;
}
console.log(`${rows.length} files in ${outDir}: ${fetched} fetched from commit ${COMMIT.slice(0, 7)}, ` +
  `${rows.length - fetched} already there`);
