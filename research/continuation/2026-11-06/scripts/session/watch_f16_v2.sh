#!/usr/bin/env bash
# Poll the F16 run on vast every 10 min (v2, 2026-11-05 23:00).
# v1 waited for "=== run_f16_linux.sh exited", which tmux prints to the pane, not to the log, so it never fired on success.
# v2 ends on the "done: download" step line, on STOP/STOPPED, when run_f16_linux.sh is no longer running, or when a job
# ends without status-pass. On "done: download" it downloads the three archives into a NEW folder and checks sha256,
# but only if E: keeps >= 20 GiB free afterwards (owner's limit for the final download). Nothing is deleted anywhere.
S="ssh -o ConnectTimeout=20 -o BatchMode=yes -o ServerAliveInterval=30 -i C:/Users/quang/.ssh/id_ed25519 -p 44887 root@77.104.167.149"
SCP="scp -o ConnectTimeout=20 -o BatchMode=yes -o ServerAliveInterval=30 -i C:/Users/quang/.ssh/id_ed25519 -P 44887"
HOST=root@77.104.167.149
LOG=/root/rb-upload-f16/run_f16_linux.log
DEST=E:/RideBoundData/research/fullsys-f16-v2/runs-linux-2026-11-06
FLOOR=$((20 * 1024 * 1024 * 1024))

download() {
    sha=$($S "ls -t /rb/ridebound-f16-results-*.sha256 | head -1") || { echo "RESULT: no .sha256 on vast"; exit 4; }
    [ -n "$sha" ] || { echo "RESULT: no .sha256 on vast"; exit 4; }
    names=$($S "awk '{print \$2}' $sha" | tr -d '\r' | tr '\n' ' ') || { echo "RESULT: cannot read $sha"; exit 4; }
    need=$($S "cd /rb && stat -c %s $names | awk '{s += \$1} END {print s}'")
    free=$(powershell -NoProfile -Command "(Get-PSDrive E).Free" | tr -d '\r')
    echo "$(date +%H:%M) archives: $sha -> $(echo $names); need $need B, E free $free B"
    if [ $((free - need)) -lt $FLOOR ]; then echo "RESULT: not downloading, E would drop below 20 GiB"; exit 5; fi
    mkdir -p "$DEST" || exit 6
    $SCP "$HOST:$sha" "$DEST/" || { echo "RESULT: scp of $sha failed"; exit 7; }
    for n in $names; do
        if [ -e "$DEST/$n" ]; then echo "RESULT: $DEST/$n exists, not overwriting"; exit 8; fi
        echo "$(date +%H:%M) downloading $n"
        $SCP "$HOST:/rb/$n" "$DEST/" || { echo "RESULT: scp of $n failed (partial file left as is)"; exit 7; }
    done
    echo "$(date +%H:%M) checking sha256"
    if (cd "$DEST" && sha256sum -c "$(basename "$sha")"); then
        echo "RESULT: downloaded and sha256 OK into $DEST"; exit 0
    fi
    echo "RESULT: sha256 MISMATCH in $DEST"; exit 9
}

fails=0
for i in $(seq 1 120); do
    o=$($S "grep -c ' end   ' $LOG; grep -c -- '-> status-pass' $LOG; if pgrep -f '[b]ash run_f16_linux.sh' >/dev/null; then echo ALIVE; else echo GONE; fi; grep -E '^STOP:|STOPPED at line|done: download|NOT allExact|^allExact:|^\[[^]]*\] [0-9]+ ' $LOG | tail -3" 2>/dev/null)
    rc=$?
    if [ $rc -eq 255 ]; then
        fails=$((fails + 1)); echo "$(date +%H:%M) unreachable ($fails)"
        [ $fails -ge 3 ] && { echo "RESULT: unreachable 3x"; exit 1; }
        sleep 600; continue
    fi
    fails=0
    ends=$(echo "$o" | sed -n 1p); pass=$(echo "$o" | sed -n 2p); alive=$(echo "$o" | sed -n 3p); last=$(echo "$o" | tail -1)
    echo "$(date +%H:%M) job ends $ends, status-pass $pass, $alive; $last"
    if [ "$ends" != "$pass" ]; then
        echo "RESULT: a job ended without status-pass"; $S "grep ' end   ' $LOG | grep -v 'status-pass' | tail -5"; exit 2
    fi
    if echo "$o" | grep -q 'done: download'; then
        $S "tail -12 $LOG"; download
    fi
    if echo "$o" | grep -qE '^STOP:|STOPPED at line' || [ "$alive" = GONE ]; then
        echo "RESULT: run_f16_linux.sh ended without the done line"; $S "tail -25 $LOG"; exit 3
    fi
    sleep 600
done
echo "RESULT: watcher timeout (20 h)"
