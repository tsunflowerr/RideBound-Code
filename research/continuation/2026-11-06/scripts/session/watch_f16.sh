#!/usr/bin/env bash
# Poll the F16 run on vast every 10 min; exit when run_f16_linux.sh ends, a STOP line appears, or vast is unreachable 3x.
S="ssh -o ConnectTimeout=20 -o BatchMode=yes -i C:/Users/quang/.ssh/id_ed25519 -p 44887 root@77.104.167.149"
LOG=/root/rb-upload-f16/run_f16_linux.log
fails=0
for i in $(seq 1 120); do
    o=$($S "grep -c ' end   ' $LOG; grep -c -- '-> status-pass' $LOG; grep -E '^STOP:|STOPPED at line|=== run_f16_linux.sh exited|done: download|NOT allExact' $LOG | tail -3; grep -E '^\[.*\] [0-9] ' $LOG | tail -1" 2>/dev/null)
    rc=$?
    if [ $rc -eq 255 ]; then
        fails=$((fails + 1)); echo "$(date +%H:%M) unreachable ($fails)"
        [ $fails -ge 3 ] && { echo "RESULT: unreachable 3x"; exit 1; }
        sleep 600; continue
    fi
    fails=0
    ends=$(echo "$o" | sed -n 1p); pass=$(echo "$o" | sed -n 2p); last=$(echo "$o" | tail -1)
    echo "$(date +%H:%M) job ends $ends, status-pass $pass; $last"
    if echo "$o" | grep -qE '^STOP:|STOPPED at line|=== run_f16_linux.sh exited'; then
        echo "RESULT: script ended or stopped"; $S "tail -15 $LOG"; exit 0
    fi
    if [ "$ends" != "$pass" ]; then
        echo "RESULT: a job ended without status-pass"; $S "grep ' end   ' $LOG | grep -v 'status-pass' | tail -5"; exit 2
    fi
    sleep 600
done
echo "RESULT: watcher timeout (20 h)"
