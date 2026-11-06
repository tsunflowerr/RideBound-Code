#!/usr/bin/env bash
# Poll the rented machine every 10 min during steps 8-9; exit when run_linux.sh ends (done or failed),
# when a failure line appears, or after 3 unreachable polls in a row.
HOST="-p 44887 -o BatchMode=yes -o ConnectTimeout=30 root@77.104.167.149"
misses=0
while true; do
    out=$(ssh $HOST '
        r=$(wc -l < /rb/runs/report/riders-real-riders.tsv 2>/dev/null || echo 0)
        c=$(cut -f1-2 /rb/runs/report/riders-real-riders.tsv 2>/dev/null | sort -u | wc -l)
        f=$(grep -cE "Traceback|Killed|STOP|die |ERROR|failed" /root/rb-upload/run_linux.log)
        s=$(grep -E "^\[20[0-9-]+T[0-9:]+\] [0-9] " /root/rb-upload/run_linux.log | tail -1 | cut -c1-50)
        d=$(grep -c "done:" /root/rb-upload/run_linux.log)
        e=$(pgrep -fxc "bash run_linux.sh")
        echo "$r|$c|$f|$s|$d|$e"' 2>/dev/null)
    if [ -z "$out" ]; then
        misses=$((misses + 1)); echo "$(date +%H:%M) unreachable ($misses)"
        [ "$misses" -ge 3 ] && { echo "RESULT: machine unreachable 3 times in a row"; exit 2; }
        sleep 600; continue
    fi
    misses=0
    IFS='|' read -r riders cells fails step done running <<<"$out"
    [ -z "${base:-}" ] && base=$fails
    echo "$(date +%H:%M) riders $riders; cell-arm groups $((cells - 1)); failure-like lines $fails (baseline $base); $step; done-lines $done; running $running"
    if [ "$fails" -gt "$base" ]; then echo "RESULT: a failure-like line appeared"; exit 1; fi
    if [ "$running" = 0 ]; then echo "RESULT: run_linux.sh ended (step: $step; done-lines $done)"; exit 0; fi
    sleep 600
done
