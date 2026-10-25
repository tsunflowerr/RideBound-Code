"""Diagnostic launcher (NOT evidence): bridge_launcher_g2.py plus a dump of the two legs at a locked-leg mismatch.

Wraps RideBoundFleetControl.assign_vehicle_plan in this process only. When it raises RBWP7_LOCKED_LEG_MISMATCH, writes
to RB_DIAG_DUMP (JSON): the vehicle, the time, FleetPy's locked current leg and the first legs built from the new plan
(each as the adapter's own _leg_signature), FleetPy's stored plan head, and the Runner route the plan came from. Then it
re-raises, so the job fails exactly as before. No repository file changes.
Usage: python -B diag_launcher_p1.py <world wrapper .py> <wrapper arguments...>
"""
from __future__ import annotations

import importlib.util
import json
import os
import runpy
import sys

LAUNCHER = r"E:\Code\Report_INT3508\ridebound-scratchpad\via-bridge-2026-10-22\bridge_launcher_g2.py"


def load_launcher():
    spec = importlib.util.spec_from_file_location("bridge_launcher_g2", LAUNCHER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def text(value):
    return value if isinstance(value, (int, float, str, bool, type(None))) else repr(value)


FLEETPY_ROOT = r"E:\RideBoundData\wp7\FleetPy-1.0.2"   # the --fleetpy-root of every bridge job


def install_dump(adapter_root: str) -> None:
    sys.path[:0] = [adapter_root, FLEETPY_ROOT]       # fleet_control imports FleetPy's src package
    try:
        from ridebound_fleetpy import fleet_control as fc
    finally:
        sys.path.remove(adapter_root)
        sys.path.remove(FLEETPY_ROOT)
    original = fc.RideBoundFleetControl.assign_vehicle_plan

    def assign_vehicle_plan(self, veh_obj, vehicle_plan, sim_time, *args, **kwargs):
        try:
            return original(self, veh_obj, vehicle_plan, sim_time, *args, **kwargs)
        except Exception as failure:
            if "RBWP7_LOCKED_LEG_MISMATCH" in str(failure):
                stored = getattr(self, "veh_plans", {}).get(veh_obj.vid)
                head = stored.list_plan_stops[0] if stored is not None and stored.list_plan_stops else None
                new_legs = self._build_VRLs(vehicle_plan, veh_obj, sim_time)
                dump = {
                    "vid": veh_obj.vid, "simTime": sim_time, "vehiclePos": text(veh_obj.pos),
                    "currentLeg": [text(v) for v in self._leg_signature(veh_obj.assigned_route[0])],
                    "newLegs": [[text(v) for v in self._leg_signature(leg)] for leg in new_legs[:3]],
                    "storedHead": None if head is None else {
                        "pos": text(head.get_pos()), "locked": head.is_locked(), "startedAt": text(head.get_started_at()),
                        "boarding": [text(r) for r in head.get_list_boarding_rids()],
                        "alighting": [text(r) for r in head.get_list_alighting_rids()]},
                    "newPlanStops": [{"pos": text(s.get_pos()), "locked": s.is_locked(),
                                      "boarding": [text(r) for r in s.get_list_boarding_rids()],
                                      "alighting": [text(r) for r in s.get_list_alighting_rids()]}
                                     for s in vehicle_plan.list_plan_stops[:3]],
                    "failure": str(failure),
                }
                with open(os.environ["RB_DIAG_DUMP"], "x", encoding="utf-8") as handle:
                    json.dump(dump, handle, indent=2)
            raise

    fc.RideBoundFleetControl.assign_vehicle_plan = assign_vehicle_plan


def main() -> None:
    launcher = load_launcher()
    applied = launcher.install_fix()
    install_dump(launcher.ADAPTER_ROOT)
    print("diag_launcher_p1: " + ", ".join(applied + ["locked-leg dump"]), file=sys.stderr, flush=True)
    wrapper = sys.argv[1]
    sys.argv = sys.argv[1:]
    runpy.run_path(wrapper, run_name="__main__")


if __name__ == "__main__":
    main()
