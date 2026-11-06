#!/usr/bin/env bash
# Poll the rented machine every 10 min; exit (one notification) when a job fails, the run reaches step 8/9 or ends,
# or the machine is unreachable 3 times in a row. Prints a progress line each poll to the output file.
HOST="-p 44887 -o BatchMode=yes -o ConnectTimeout=30 root@77.104.167.149"
base_fail=""
misses=0
while true; do
    out=$(ssh $HOST '
        n=$(ls /rb/runs/final/*/evidence.json.gz 2>/dev/null | wc -l)
        f=$(grep -cE "failed-rc|finalize-failed|deferred-|STOP: some|STOP: final|GATES FAILED|Traceback|Killed" /root/rb-upload/run_linux.log)
        s=$(grep -E "^\[20[0-9-]+T[0-9:]+\] [0-9] " /root/rb-upload/run_linux.log | tail -1 | cut -c1-60)
        e=$(pgrep -fxc "bash run_linux.sh")
        echo "$n|$f|$s|$e"' 2>/dev/null)
    if [ -z "$out" ]; then
        misses=$((misses + 1)); echo "$(date +%H:%M) unreachable ($misses)"
        [ "$misses" -ge 3 ] && { echo "RESULT: machine unreachable 3 times in a row"; exit 2; }
        sleep 600; continue
    fi
    misses=0
    IFS='|' read -r done fails step running <<<"$out"
    [ -z "$base_fail" ] && base_fail=$fails
    echo "$(date +%H:%M) evidence $done/168; failure lines $fails (baseline $base_fail); $step; run_linux.sh processes $running"
    if [ "$fails" -gt "$base_fail" ]; then echo "RESULT: a failure line appeared in the run log"; exit 1; fi
    case "$step" in *"] 8 "*|*"] 9 "*) echo "RESULT: run reached: $step (evidence $done/168)"; exit 0;; esac
    if [ "$running" = 0 ]; then echo "RESULT: run_linux.sh is no longer running (step: $step, evidence $done/168)"; exit 3; fi
    sleep 600
done
