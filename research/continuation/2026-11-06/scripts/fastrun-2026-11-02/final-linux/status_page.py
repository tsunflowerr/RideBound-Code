"""Read-only status page for the final runs on the rented Linux machine (2026-11-04). Standard library only.
Serves http://127.0.0.1:8080/ (localhost only; view it through an SSH tunnel: ssh -p PORT root@HOST -L 8080:localhost:8080).
It only reads: run log, job lists, run folders, PROGRESS-*.json, GATES-REPORT.json, FINAL-REPORT.json, /proc.
Usage: python3.10 -I -B status_page.py [--port 8080]
"""
import argparse
import datetime as dt
import html
import http.server
import json
import os
import pathlib
import re
import shutil

ROOT = pathlib.Path("/rb")
RUNS = ROOT / "runs"
LOG = pathlib.Path("/root/rb-upload/run_linux.log")
JOBS = ROOT / "RideBoundData/research/fullsys-f14-v2/jobs-linux"
LISTS = (("Cổng nhánh chính (4)", "jobs-cloud-check.json", RUNS / "final"),
         ("Cổng quy công (4)", "jobs-gate-attr.json", RUNS / "gates"),
         ("168 job cuối", "jobs-final.json", RUNS / "final"))
END = re.compile(r"end\s+(\S+)\s+([\d.]+) min -> (\S+)")


def read_json(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def job_state(folder):
    if (folder / "evidence.json.gz").exists():
        return "xong"
    summary = read_json(folder / "summary.json")
    if summary is not None:
        return "đang finalize" if summary.get("status") == "pass" else "LỖI"
    return "đang chạy" if folder.exists() else "chờ"


def meminfo():
    values = dict(re.findall(r"^(\w+):\s+(\d+) kB", pathlib.Path("/proc/meminfo").read_text(), re.M))
    return int(values["MemTotal"]) / 2**20, int(values["MemAvailable"]) / 2**20


def page():
    now = dt.datetime.now()
    log_lines = LOG.read_text(encoding="utf-8", errors="replace").splitlines() if LOG.exists() else []
    steps = [line for line in log_lines if re.match(r"^\[\d{4}-\d\d-\d\dT", line) and "] " in line
             and re.search(r"\] \d ", line)]
    durations = [float(m.group(2)) for line in log_lines for m in [END.search(line)] if m]
    workers = next((int(m.group(1)) for line in log_lines for m in [re.search(r"workers (\d+) \(", line)] if m), 0)
    rows, totals = [], {}
    for title, name, out in LISTS:
        jobs = read_json(JOBS / name) or []
        states = {}
        for job in jobs:
            state = job_state(out / job["name"])
            states[state] = states.get(state, 0) + 1
        totals[name] = (len(jobs), states)
        detail = ", ".join(f"{k}: {v}" for k, v in sorted(states.items()))
        done = states.get("xong", 0)
        pct = 100 * done / len(jobs) if jobs else 0
        rows.append(f"<tr><td>{html.escape(title)}</td><td><b>{done}/{len(jobs)}</b></td>"
                    f"<td><div class=bar><div style='width:{pct:.0f}%'></div></div></td>"
                    f"<td>{html.escape(detail)}</td></tr>")
    gates = read_json(RUNS / "gates-report" / "GATES-REPORT.json")
    if gates is None:
        gate_html = "<span class=wait>chưa có (đang chạy 8 job cổng)</span>"
    elif gates.get("pass"):
        gate_html = "<span class=ok>QUA — Linux cho kết quả trùng dự đoán đã khóa</span>"
    else:
        gate_html = "<span class=bad>ĐỎ — dừng, không chạy 168 job; xem /rb/runs/gates-report</span>"
    final = read_json(RUNS / "report" / "FINAL-REPORT.json")
    final_html = ("chưa có" if final is None else
                  f"allExact = <b>{html.escape(str(final.get('allExact')))}</b>")
    total_final, states_final = totals["jobs-final.json"]
    remaining = total_final - states_final.get("xong", 0)
    avg = sum(durations) / len(durations) if durations else None
    eta = "chưa đủ số đo"
    if avg and workers:
        minutes = -(-remaining // workers) * avg
        eta = (f"khoảng {minutes / 60:.1f} giờ nữa (xong cỡ {(now + dt.timedelta(minutes=minutes)):%H:%M %d/%m}); "
               f"trung bình {avg:.0f} phút/job, {workers} job song song")
    total_gib, available_gib = meminfo()
    load = os.getloadavg()
    disk = shutil.disk_usage(ROOT)
    running = sum(1 for p in pathlib.Path("/proc").iterdir() if p.name.isdigit()
                  and _cmd(p).startswith("/rb/RideBoundData/final-v2-linux/dotnet/runtime/dotnet"))
    tail = html.escape("\n".join(log_lines[-40:]))
    step = html.escape(steps[-1]) if steps else "chưa bắt đầu"
    return f"""<!doctype html><html lang=vi><head><meta charset=utf-8><meta http-equiv=refresh content=30>
<meta name=viewport content="width=device-width,initial-scale=1"><title>RideBound chạy cuối</title><style>
body{{font:15px/1.45 system-ui,sans-serif;margin:16px;max-width:1100px;background:#fafafa;color:#222}}
h1{{font-size:20px}} table{{border-collapse:collapse;width:100%;background:#fff}}
td,th{{border:1px solid #ddd;padding:6px 8px;text-align:left;vertical-align:middle}}
.bar{{background:#eee;height:12px;width:160px;border-radius:6px;overflow:hidden}}
.bar div{{background:#2b7a3d;height:12px}} .ok{{color:#1b6b2e;font-weight:600}} .bad{{color:#b00020;font-weight:700}}
.wait{{color:#8a6d00}} pre{{background:#111;color:#ddd;padding:10px;overflow-x:auto;font-size:12px;border-radius:6px}}
small{{color:#666}}</style></head><body>
<h1>RideBound — chạy thật lần cuối trên máy thuê</h1>
<small>Cập nhật {now:%H:%M:%S %d/%m} (giờ VN), tự làm mới 30 giây. Trang chỉ đọc, không điều khiển gì.</small>
<p><b>Bước hiện tại:</b> {step}</p>
<p><b>Cổng B + C:</b> {gate_html}</p>
<table><tr><th>Danh sách</th><th>Xong</th><th></th><th>Chi tiết</th></tr>{''.join(rows)}</table>
<p><b>Còn lại (168 job):</b> {remaining} job; {html.escape(eta)}</p>
<p><b>Báo cáo cuối:</b> {final_html}</p>
<p><b>Máy:</b> Runner đang chạy: {running}; RAM còn {available_gib:.1f}/{total_gib:.1f} GiB; tải CPU {load[0]:.1f}
(1 phút) trên {os.cpu_count()} nhân; đĩa trống {disk.free / 2**30:.0f} GiB</p>
<h3>40 dòng cuối của log</h3><pre>{tail}</pre></body></html>"""


def _cmd(proc):
    try:
        return (proc / "cmdline").read_bytes().split(b"\0")[0].decode(errors="replace")
    except OSError:
        return ""


class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        try:
            body = page().encode("utf-8")
            code = 200
        except Exception as error:  # noqa: BLE001 - shown on the page instead of crashing the server
            body = f"<pre>status page error: {html.escape(repr(error))}</pre>".encode("utf-8")
            code = 500
        self.send_response(code)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *args):
        pass


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--once", action="store_true", help="print the page once and exit (self-test)")
    args = parser.parse_args()
    if args.once:
        print(page())
        return
    http.server.ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
