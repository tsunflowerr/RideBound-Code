"""Write the Runner configurations of the F14 or F16 campaign from the templates and each cell's sealed inputs.

  python -B make_job_configs.py --data <panel data> --network <network folder>
      --g2-rce35 <G2-RCE35.json> --g2-eohz35 <G2-EOHZ35.json> --wp4-template <wp4-via-g2k8-a1-fast.json>
      [--forecast-gate] [--out <folder>]

Reads <data>/predictions/shadow-inputs/<cell>.json and, with --forecast-gate (F14),
<data>/predictions-forecast-gate/forecast-gate-profiles.json. Writes into --out (default <data>/job-configs-v2,
refused if it exists):
  commitment/SHRC71.json            G2-RCE35 with dropEtaBudgetSlackPermille 1000 + dropEtaShadowAllocation (rule C)
  commitment/SHHZ71.json            G2-EOHZ35 with dropEtaDeadlineSlackPermille 1000 + dropEtaShadowAllocation
  wp4/FS-G2-SHRC71-CL/<cell>.json   WP4 template + shadowAllocation + planningForecast
  wp4/FS-G2-SHHZ71-N/<cell>.json    WP4 template + shadowAllocation
  commitment/RCE35.json             G2-RCE35 as it is (forecast gate only)
  wp4/FS-G2-RCE35-CL/<cell>.json    WP4 template + planningForecast (forecast gate only)
  CONFIGS-V2.json                   cell and node counts and the SHA-256 of every file above
The --network string goes as it is into shadowAllocation.networkDirectory, so the Runner must find the network
(D.u16 and network.json, both hashed into the configs) at that path.

Every input is checked before anything is written. Inputs unlike those of the final runs are refused:
  - a cell name is dYYYYMMDD-vf-rN-wHH, and shadow-inputs holds only <cell>.json files, each with that "cell";
  - every JSON file is UTF-8 with ASCII text, numbers that are finite doubles, lists and objects nested at most
    32 levels (the repository's templates: 6) and each key at most once in an object;
  - the Shadow inputs read here are numbers or lists of numbers, and globalOf holds distinct integers below 4,411;
  - the --network path is ASCII, and no folder on the --out path is a file.

On such inputs the files are byte-identical to those of the old generator of the final runs, which worked in two
steps: a JavaScript script wrote the configs, then a Python script added networkNodes to the Shadow configs and wrote
them again. That is why this script reads JSON the way JavaScript does and writes the commitment and forecast-gate
files in JavaScript's format.
"""
import argparse
import hashlib
import json
import math
import pathlib
import re

from ridebound_fleetpy.mapping import FleetPyProtocolMapper

NETWORK_NODES = 4411  # nodes of the Manhattan network of the final runs
BIN_MS = 900000  # 15-minute bins of the forecast profile and the Shadow factors
CELL = re.compile(r"d[0-9]{8}-vf-r[0-9]-w[0-9]{2}")  # a cell of the final runs, such as d20190919-vf-r0-w08
MAX_DEPTH = 32  # lists and objects nested in one input file
NUMBERS = ("kappa", "alphaS", "phi", "theta", "omegaPms", "strideMs", "intervalMs", "sampled")
NUMBER_LISTS = ("factors", "prior.O", "prior.D", "forecast.profile")


def refuse(where, problem):
    raise SystemExit(f"refused: {where}: {problem}")


def is_array_index(key):
    """True for a key such as "0" or "17": JavaScript puts those first in an object, in numeric order."""
    return re.fullmatch("0|[1-9][0-9]{0,9}", key) is not None and int(key) < 2**32 - 1


def js_object(pairs):
    """A JSON object with the key order that JavaScript's JSON.parse gives it. A key that appears twice is refused:
    JSON.parse would keep only its last value, and JSON.stringify and json.dumps never write a key twice."""
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError(f"the key {json.dumps(key)} appears twice in one object")
        value[key] = item
    keys = sorted((key for key in value if is_array_index(key)), key=int)
    return {key: value[key] for key in keys + [key for key in value if not is_array_index(key)]}


def refuse_constant(name):
    """Python's json reads NaN, Infinity and -Infinity; JavaScript refuses them, and so do we."""
    raise ValueError(f"{name} is not valid JSON")


def is_number(value):
    """True for a JSON number that reads as a finite double (true and false are not numbers)."""
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return False
    try:
        return math.isfinite(float(value))
    except OverflowError:  # an integer beyond the largest double
        return False


def is_number_list(value):
    return isinstance(value, list) and all(is_number(item) for item in value)


def check_value(value, where, depth=1):
    """Refuse what no input file of the final runs held: text that is not ASCII, a number beyond the range of a
    double, or lists and objects nested more than MAX_DEPTH levels."""
    if isinstance(value, (dict, list)):
        if depth > MAX_DEPTH:
            refuse(where, f"lists and objects nested more than {MAX_DEPTH} levels")
        items = list(value) + list(value.values()) if isinstance(value, dict) else value  # keys and values
        for item in items:
            check_value(item, where, depth + 1)
    elif isinstance(value, str) and not value.isascii():
        refuse(where, f"text that is not ASCII: {ascii(value)}")
    elif isinstance(value, (int, float)) and not isinstance(value, bool) and not is_number(value):
        refuse(where, "a number beyond the range of a double")


def read_json(path):
    """A JSON file read as JavaScript's JSON.parse reads it, then checked with check_value."""
    try:
        value = json.loads(pathlib.Path(path).read_text(encoding="utf-8"), object_pairs_hook=js_object,
                           parse_constant=refuse_constant)
    except RecursionError:  # Python's reader gives up after a few thousand levels
        refuse(path, f"lists and objects nested more than {MAX_DEPTH} levels")
    except (OSError, ValueError) as error:  # missing, not UTF-8, not JSON, NaN, a key twice, over 4,300 digits
        refuse(path, error)
    check_value(value, path)
    return value


def js_number(value):
    """JavaScript's String(x) for the double x of a finite number: the shortest digits that read back as x, in
    JavaScript's notation."""
    x = float(value)
    if x == 0:
        return "0"  # also for -0
    if x < 0:
        return "-" + js_number(-x)
    mantissa, _, power = repr(x).partition("e")  # repr gives the same shortest digits, in Python's notation
    whole, _, fraction = mantissa.partition(".")
    digits = (whole + fraction).lstrip("0")
    point = len(whole) + int(power or 0) - (len(whole + fraction) - len(digits))  # x = 0.<digits> * 10**point
    digits = digits.rstrip("0")
    if len(digits) <= point <= 21:
        return digits + "0" * (point - len(digits))
    if 0 < point <= 21:
        return digits[:point] + "." + digits[point:]
    if -6 < point <= 0:
        return "0." + "0" * -point + digits
    sign = "+" if point > 1 else "-"
    return (digits[0] + "." + digits[1:] if len(digits) > 1 else digits) + f"e{sign}{abs(point - 1)}"


def js_json(value, indent=""):
    """JSON.stringify(value, null, 2): the format of the files of the old JavaScript step."""
    inner = indent + "  "
    if isinstance(value, dict) and value:
        items = (f"{inner}{js_json(key)}: {js_json(item, inner)}" for key, item in value.items())
        return "{\n" + ",\n".join(items) + "\n" + indent + "}"
    if isinstance(value, list) and value:
        return "[\n" + ",\n".join(inner + js_json(item, inner) for item in value) + "\n" + indent + "]"
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return js_number(value)  # JavaScript holds every number as a double
    # ASCII text escaped as JSON.stringify escapes it (both leave DEL as it is), true, false, null, {} and []
    return json.dumps(value, ensure_ascii=False)


def read_commitment(path):
    """A commitment template, which must hold exactly one policy."""
    config = read_json(path)
    policies = config.get("policies") if isinstance(config, dict) else None
    if not isinstance(policies, list) or len(policies) != 1 or not isinstance(policies[0], dict):
        refuse(path, "a JSON object with a list of one policy is expected")
    return config


def shadow_policies(rce35_path, eohz35_path):
    """SHRC71 and SHHZ71: the slack of each template goes from 350 to 1000 permille, and Shadow is switched on."""
    shrc71, shhz71 = read_commitment(rce35_path), read_commitment(eohz35_path)
    rule_c, fence = shrc71["policies"][0], shhz71["policies"][0]
    if rule_c.get("dropEtaBudgetSlackPermille") != 350:
        refuse(rce35_path, "the template changed: dropEtaBudgetSlackPermille is not 350")
    if fence.get("dropEtaDeadlineSlackPermille") != 350 or fence.get("dropEtaDeadlineRelaxed") is not True:
        refuse(eohz35_path, "the template changed: dropEtaDeadlineSlackPermille is not 350 or not relaxed")
    rule_c.update(dropEtaBudgetSlackPermille=1000, dropEtaShadowAllocation=True)
    fence.update(dropEtaDeadlineSlackPermille=1000, dropEtaShadowAllocation=True)
    return shrc71, shhz71


def read_gate_profiles(data):
    """The forecast gate's profile of each cell (F14)."""
    path = data / "predictions-forecast-gate" / "forecast-gate-profiles.json"
    profiles = read_json(path)
    if not isinstance(profiles, dict):
        refuse(path, "a JSON object is expected")
    for cell, profile in profiles.items():
        if not CELL.fullmatch(cell):
            refuse(path, f"{json.dumps(cell)} is not a cell name dYYYYMMDD-vf-rN-wHH")
        if not is_number_list(profile):
            refuse(path, f"the profile of {cell} must be a list of numbers")
    return profiles


def field(inputs, name):
    """inputs["a"]["b"] for the name "a.b", or None if a part is missing."""
    for key in name.split("."):
        inputs = inputs.get(key) if isinstance(inputs, dict) else None
    return inputs


def read_cells(folder):
    """Each cell's sealed Shadow inputs, checked, by cell name."""
    if not folder.is_dir():
        refuse(folder, "the folder is missing")
    cells = {}
    for path in sorted(folder.iterdir()):
        cell = path.name.removesuffix(".json")
        if not path.is_file() or not path.name.endswith(".json") or not CELL.fullmatch(cell):
            refuse(path, "shadow-inputs may hold only files named dYYYYMMDD-vf-rN-wHH.json")
        inputs = read_json(path)
        if field(inputs, "cell") != cell:
            refuse(path, f'a JSON object with "cell": "{cell}" is expected')
        for name in NUMBERS:
            if not is_number(field(inputs, name)):
                refuse(path, f"{name} must be a number")
        for name in NUMBER_LISTS:
            if not is_number_list(field(inputs, name)):
                refuse(path, f"{name} must be a list of numbers")
        global_of = inputs.get("globalOf")
        if (not isinstance(global_of, list) or not all(type(g) is int and 0 <= g < NETWORK_NODES for g in global_of)
                or len(set(global_of)) != len(global_of)):
            refuse(path, f"globalOf must be a list of distinct integers from 0 to {NETWORK_NODES - 1}")
        cells[cell] = inputs
    if not cells:
        refuse(folder, "no cell")
    return cells


def file_sha256(path):
    try:
        return hashlib.sha256(path.read_bytes()).hexdigest()
    except OSError as error:
        refuse(path, error)


def forecast_block(profile):
    return {"profile": [js_number(x) for x in profile], "binMs": BIN_MS}


def shadow_block(inputs, network, hashes):
    """The shadowAllocation of one cell (without networkNodes), in the old JavaScript step's key order."""
    return {"networkDirectory": network,
            "distancesSha256": hashes[0],
            "graphSha256": hashes[1],
            "kappa": js_number(inputs["kappa"]),
            "factors": [js_number(x) for x in inputs["factors"]],
            "pickupWindowMs": inputs["omegaPms"],
            "strideMs": inputs["strideMs"],
            "intervalMs": inputs["intervalMs"],
            "alpha": js_number(inputs["alphaS"]),
            "phi": js_number(inputs["phi"]),
            "sampled": inputs["sampled"],
            "priorOrigin": inputs["prior"]["O"],
            "priorDestination": inputs["prior"]["D"],
            "threshold": js_number(inputs["theta"]),
            "binMs": BIN_MS}


def network_nodes(global_of):
    """Run node id -> network node index. The run node ids are the adapter's ids of FleetPy nodes 0, 1, 2, ..."""
    mapper = FleetPyProtocolMapper()
    return {mapper.node_id(i): g for i, g in enumerate(global_of)}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--data", required=True, type=pathlib.Path, help="the panel's data folder")
    parser.add_argument("--network", required=True, help="the folder of D.u16 and network.json, as the Runner sees it")
    parser.add_argument("--g2-rce35", required=True, type=pathlib.Path, help="commitment template G2-RCE35.json")
    parser.add_argument("--g2-eohz35", required=True, type=pathlib.Path, help="commitment template G2-EOHZ35.json")
    parser.add_argument("--wp4-template", required=True, type=pathlib.Path,
                        help="WP4 template wp4-via-g2k8-a1-fast.json")
    parser.add_argument("--forecast-gate", action="store_true", help="also write the forecast gate arm (F14)")
    parser.add_argument("--out", type=pathlib.Path, help="default: <data>/job-configs-v2")
    args = parser.parse_args(argv)
    out = args.out or args.data / "job-configs-v2"
    if out.exists():
        refuse(out, "the output folder exists")
    for parent in out.parents:
        if parent.is_file():
            refuse(out, f"{parent} is a file, not a folder")
    if not args.network.isascii():
        refuse("--network", f"an ASCII path is expected, not {ascii(args.network)}")

    files = {}  # file in the output folder -> its text; nothing is written before every input is checked
    shrc71, shhz71 = shadow_policies(args.g2_rce35, args.g2_eohz35)
    files["commitment/SHRC71.json"] = js_json(shrc71) + "\n"
    files["commitment/SHHZ71.json"] = js_json(shhz71) + "\n"
    wp4 = read_json(args.wp4_template)
    if not isinstance(wp4, dict):
        refuse(args.wp4_template, "a JSON object is expected")
    if args.forecast_gate:
        files["commitment/RCE35.json"] = js_json(read_commitment(args.g2_rce35)) + "\n"
        for cell, profile in read_gate_profiles(args.data).items():
            config = {**wp4, "planningForecast": forecast_block(profile)}
            files[f"wp4/FS-G2-RCE35-CL/{cell}.json"] = js_json(config) + "\n"
    cells = read_cells(args.data / "predictions" / "shadow-inputs")
    network = pathlib.Path(args.network)
    hashes = file_sha256(network / "D.u16"), file_sha256(network / "network.json")
    for cell, inputs in cells.items():
        shadow = shadow_block(inputs, args.network, hashes)
        nodes = network_nodes(inputs["globalOf"])
        configs = {"FS-G2-SHRC71-CL": {**wp4, "shadowAllocation": shadow,
                                       "planningForecast": forecast_block(inputs["forecast"]["profile"])},
                   "FS-G2-SHHZ71-N": {**wp4, "shadowAllocation": shadow}}
        for arm, config in configs.items():
            value = json.loads(js_json(config))  # what the old Python step read back from the JavaScript step
            value["shadowAllocation"]["networkNodes"] = nodes
            files[f"wp4/{arm}/{cell}.json"] = json.dumps(value, indent=2) + "\n"

    for name, text in files.items():
        (out / name).parent.mkdir(parents=True, exist_ok=True)
        (out / name).write_text(text, encoding="utf-8", newline="\n")
    summary = {"cells": len(cells), "nodes": sum(len(inputs["globalOf"]) for inputs in cells.values()),
               "sha256": {path.relative_to(out).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                          for path in sorted(out.rglob("*.json"))}}
    # Text mode as before: the recorded CONFIGS-V2.json files were written on Windows, with CRLF line ends.
    (out / "CONFIGS-V2.json").write_text(json.dumps(summary, indent=1), encoding="utf-8")
    print(f"{summary['cells']} cells, {summary['nodes']} node ids; {len(summary['sha256'])} files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
