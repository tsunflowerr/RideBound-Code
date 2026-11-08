"""make_job_configs.py: the job configurations, byte for byte as the old two-step generator wrote them, and the
refusal of inputs unlike those of the final runs before anything is written."""
import contextlib
import hashlib
import io
import json
import os
import pathlib
import shutil
import sys
import tempfile
import unittest

ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

import make_job_configs  # noqa: E402
from ridebound_fleetpy.mapping import FleetPyProtocolMapper  # noqa: E402

W08 = "data/predictions/shadow-inputs/d20190919-vf-r0-w08.json"
W17 = "data/predictions/shadow-inputs/d20190919-vf-r0-w17.json"
GATE = "data/predictions-forecast-gate/forecast-gate-profiles.json"
# A tiny panel. The numbers, keys and strings are chosen to exercise every format rule.
INPUTS = {
    "templates/G2-RCE35.json": r'''{"configurationVersion": "1.0.0", "policies": [{"policyId": "p",
  "dropEtaBudgetSlackPermille": 350.0, "2": "an array-index key", "1": 1e21, "ratchetLocks": ["pickupEta"]}],
  "stopDistances": []}''',
    "templates/G2-EOHZ35.json": r'''{"configurationVersion": "1.0.0", "policies": [{"policyId": "q",
  "dropEtaDeadlineSlackPermille": 350, "dropEtaDeadlineRelaxed": true, "limits": [{"hardLimit": 0}, {}]}],
  "stopDistances": []}''',
    "templates/wp4.json": r'''{"configurationVersion": "1.0.0", "policyId": "ridebound-hard-vector",
  "note": "tab\t quote\" back\\ bell\u0007 del\u007f", "solverExecution": {"randomSeed": 7,
  "maximumSolverWorkUnits": 1e7, "big": 9007199254740993, "zero": -0.0}, "10": true, "auditHash": "light-v1"}''',
    W08: r'''{"cell": "d20190919-vf-r0-w08",
  "kappa": 1.0734567890123457, "factors": [1, 0.1, 1e21, 2e-7, -0, 1152921504606846976],
  "globalOf": [17, 4410, 0, 251], "prior": {"O": [1, 2.0, 3e2], "D": [0, 3]}, "phi": 0.30000000000000004,
  "sampled": 12, "alphaS": 0.15, "strideMs": 0, "omegaPms": 6e5, "intervalMs": 1.5e-7, "theta": 1e-7,
  "zoneIds": ["not read", 4], "forecast": {"profile": [1, 1.25, 0.000001, 123e-20], "binMs": 900000}}''',
    W17: r'''{"cell": "d20190919-vf-r0-w17",
  "kappa": -1e-7, "factors": [], "globalOf": [5, 6], "prior": {"O": [], "D": [7]}, "phi": 0, "sampled": 0,
  "alphaS": 0.15, "strideMs": 0, "omegaPms": 600000, "intervalMs": 600000, "theta": 0.0123,
  "forecast": {"profile": [2], "binMs": 900000}}''',
    GATE: r'''{"d20191218-vf-r2-w17": [2], "d20190919-vf-r0-w08": [1, 1.5e-7, 100]}''',
    "net/network.json": '{"N": 4411}',
}
# sha256 of every file the old generator (make_job_configs.js forecast-gate and fullsys, then
# make_job_configs_v2.py) wrote from INPUTS with the network folder "net"; CONFIGS-V2.json with LF line ends.
EXPECTED_SHA256 = {
    "commitment/RCE35.json": "6eaf1ae30bb6cd7c1f6588ff9b33aa1b7af2845c28dae2ad6d3546616eb3e9eb",
    "commitment/SHHZ71.json": "330acb4ddc68a46dbedc62c9bd1a47f9cd879a8569115ea25602b08955532a41",
    "commitment/SHRC71.json": "e84455c8c0f47886ca174c2213bafc6d309f1ce2314bce1632dfb9c5eb660580",
    "CONFIGS-V2.json": "b7e1d99d6fb4b103b60eb5d72b365148e81d9b2d2ec2d0de314d27ef9d57e589",
    "wp4/FS-G2-RCE35-CL/d20190919-vf-r0-w08.json": "fd2f7e950b5ccdd2aa7bdac13d5b42ae6ab07b30c223796f0fd7e6e8e7fb926d",
    "wp4/FS-G2-RCE35-CL/d20191218-vf-r2-w17.json": "6bbbf5eee1c8d38974e0e9c4b98d9dd4dea92183a28a8b8b937dca08df83844f",
    "wp4/FS-G2-SHHZ71-N/d20190919-vf-r0-w08.json": "629636df08d67561bf1ded9392cf9b24498e52a61b38acd3754948c188085d56",
    "wp4/FS-G2-SHHZ71-N/d20190919-vf-r0-w17.json": "925e444d6655b3615369f02780c0d4dd0399ffc2673ca5dbc4916422676f17f1",
    "wp4/FS-G2-SHRC71-CL/d20190919-vf-r0-w08.json": "e46d8fa79142954d6b81dc9b93d899f6b282eab65aa4ead9a45a6e1e3a2864ba",
    "wp4/FS-G2-SHRC71-CL/d20190919-vf-r0-w17.json": "bd0dd02781476437f4f7e911420925b53c99c823dd503b317a118fa608526bcc",
}


def make_panel(root):
    for name, text in INPUTS.items():
        (root / name).parent.mkdir(parents=True, exist_ok=True)
        (root / name).write_text(text, encoding="utf-8")
    (root / "net" / "D.u16").write_bytes(bytes(range(16)))


def run(root, *extra):
    """main() in the panel's folder, so that the network folder is the relative path "net". An option given again
    in extra replaces the first one."""
    args = ["--data", "data", "--network", "net", "--g2-rce35", "templates/G2-RCE35.json",
            "--g2-eohz35", "templates/G2-EOHZ35.json", "--wp4-template", "templates/wp4.json", *extra]
    here = os.getcwd()
    os.chdir(root)
    try:
        with contextlib.redirect_stdout(io.StringIO()):
            return make_job_configs.main(args)
    finally:
        os.chdir(here)


def replace(name, old, new):
    """A change of the panel: one text replacement in one input file."""
    def change(root):
        text = (root / name).read_text(encoding="utf-8")
        assert text.count(old) == 1, (name, old)
        (root / name).write_text(text.replace(old, new), encoding="utf-8")
    return change


def nested(levels):
    """A WP4 template whose lists and objects are nested `levels` deep."""
    return replace("templates/wp4.json", '"10": true', '"deep": ' + "[" * (levels - 1) + "]" * (levels - 1))


class NumberFormatTests(unittest.TestCase):
    def test_js_number_is_javascript_string_of_a_double(self):
        # (double, String(x) in Node 20)
        cases = [(0.0, "0"), (-0.0, "0"), (1.0, "1"), (-1.5, "-1.5"), (350.0, "350"), (0.1, "0.1"),
                 (0.1 + 0.2, "0.30000000000000004"), (4.35, "4.35"), (1e21, "1e+21"), (1e20, "100000000000000000000"),
                 (999999999999999900000.0, "999999999999999900000"), (1e-7, "1e-7"), (1.5e-7, "1.5e-7"),
                 (1e-6, "0.000001"), (2.5e-6, "0.0000025"), (1e-5, "0.00001"), (123e-20, "1.23e-18"),
                 (2.0**53, "9007199254740992"), (2.0**60, "1152921504606847000"), (1e16, "10000000000000000"),
                 (5e-324, "5e-324"), (2.2250738585072014e-308, "2.2250738585072014e-308"),
                 (1.7976931348623157e308, "1.7976931348623157e+308"), (1.2345678901234568e21, "1.2345678901234568e+21"),
                 (123456789012345680000.0, "123456789012345680000"), (1 / 3, "0.3333333333333333"),
                 (-1e-7, "-1e-7"), (0.15, "0.15"), (1.0734567890123457, "1.0734567890123456"),
                 (9007199254740993, "9007199254740992"), (600000, "600000")]
        for x, expected in cases:
            self.assertEqual(make_job_configs.js_number(x), expected, repr(x))

    def test_numbers_and_keys_are_read_as_javascript_reads_them(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / "x.json"
            path.write_text(f'[2.0, 123456789012345678901234567890, -0, 1e-400, 1{"0" * 300}]', encoding="utf-8")
            self.assertEqual([make_job_configs.js_number(x) for x in make_job_configs.read_json(path)],
                             ["2", "1.2345678901234568e+29", "0", "0", "1e+300"])
            path.write_text('{"b": 1, "10": 2, "a": 3, "2": 4, "02": 5, "4294967295": 6, "4294967294": 7}',
                            encoding="utf-8")
            self.assertEqual(list(make_job_configs.read_json(path)),
                             ["2", "10", "4294967294", "b", "a", "02", "4294967295"])  # JavaScript's key order
            for text in ("[NaN]", "[Infinity]", "[-Infinity]"):
                path.write_text(text, encoding="utf-8")
                with self.assertRaisesRegex(SystemExit, "is not valid JSON"):
                    make_job_configs.read_json(path)

    def test_js_json_is_json_stringify_with_indent_2(self):
        value = {"a": [1.0, 1e21, 1e-7, -0.0, "tab\t del\x7f \x01", [], {}], "b": {"c": None, "d": False}}
        expected = ('{\n  "a": [\n    1,\n    1e+21,\n    1e-7,\n    0,\n    "tab\\t del\x7f \\u0001",\n    [],\n'
                    '    {}\n  ],\n  "b": {\n    "c": null,\n    "d": false\n  }\n}')
        self.assertEqual(make_job_configs.js_json(value), expected)


class PanelTests(unittest.TestCase):
    def test_panel_files_equal_the_old_generator(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            make_panel(root)
            self.assertEqual(run(root, "--forecast-gate"), 0)
            out = root / "data" / "job-configs-v2"
            found = {path.relative_to(out).as_posix(): path.read_bytes() for path in out.rglob("*") if path.is_file()}
            found["CONFIGS-V2.json"] = found["CONFIGS-V2.json"].replace(b"\r\n", b"\n")
            self.assertEqual({name: hashlib.sha256(data).hexdigest() for name, data in found.items()},
                             EXPECTED_SHA256)

    def test_shadow_config_fields(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            make_panel(root)
            run(root)
            out = root / "data" / "job-configs-v2"
            self.assertFalse((out / "commitment" / "RCE35.json").exists())  # no forecast gate without the option
            config = json.loads((out / "wp4/FS-G2-SHRC71-CL/d20190919-vf-r0-w08.json").read_text(encoding="utf-8"))
            shadow = config["shadowAllocation"]
            self.assertEqual(shadow["networkDirectory"], "net")
            self.assertEqual(shadow["distancesSha256"], hashlib.sha256(bytes(range(16))).hexdigest())
            self.assertEqual(shadow["kappa"], "1.0734567890123456")
            self.assertEqual(shadow["factors"], ["1", "0.1", "1e+21", "2e-7", "0", "1152921504606847000"])
            self.assertEqual(shadow["threshold"], "1e-7")
            self.assertEqual((shadow["pickupWindowMs"], shadow["intervalMs"]), (600000, 1.5e-7))
            self.assertEqual(shadow["priorOrigin"], [1, 2, 300])
            self.assertEqual(config["planningForecast"], {"profile": ["1", "1.25", "0.000001", "1.23e-18"],
                                                          "binMs": 900000})
            mapper = FleetPyProtocolMapper()
            self.assertEqual(shadow["networkNodes"], {mapper.node_id(i): g for i, g in enumerate([17, 4410, 0, 251])})
            self.assertEqual(next(iter(shadow["networkNodes"])),
                             "node-176f918dfe090b75ce86691ec554f8512afcfa10b021ea4e283d4a09a696d897")
            plain = json.loads((out / "wp4/FS-G2-SHHZ71-N/d20190919-vf-r0-w17.json").read_text(encoding="utf-8"))
            self.assertNotIn("planningForecast", plain)
            self.assertEqual(plain["shadowAllocation"]["threshold"], "0.0123")
            policy = json.loads((out / "commitment/SHHZ71.json").read_text(encoding="utf-8"))["policies"][0]
            self.assertEqual((policy["dropEtaDeadlineSlackPermille"], policy["dropEtaShadowAllocation"]), (1000, True))


class RefusalTests(unittest.TestCase):
    def test_nesting_up_to_32_levels_is_read(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            make_panel(root)
            nested(32)(root)
            self.assertEqual(run(root, "--forecast-gate"), 0)

    def test_inputs_unlike_the_final_runs_are_refused_before_anything_is_written(self):
        def remove(name):
            return lambda root: (root / name).unlink()

        def add_file(name, text="{}"):
            return lambda root: (root / name).write_text(text, encoding="utf-8")

        def both(first, second):
            return lambda root: (first(root), second(root))

        def empty_inputs(root):
            shutil.rmtree(root / "data/predictions/shadow-inputs")
            (root / "data/predictions/shadow-inputs").mkdir()

        cell = '"cell": "d20190919-vf-r0-w08"'
        def data_a_file(root):
            shutil.rmtree(root / "data")
            (root / "data").write_text("{}", encoding="utf-8")

        cases = [  # (what, change of the panel, words of the message, extra arguments)
            ("output folder exists", lambda root: (root / "data/job-configs-v2").mkdir(), "output folder exists"),
            ("output folder inside a file", add_file("afile"), "afile is a file", "--out", "afile/sub"),
            ("data folder a file", data_a_file, "data is a file"),
            ("network not ASCII", None, "an ASCII path is expected", "--network", "n\u00e9t"),
            ("network without D.u16", remove("net/D.u16"), "D.u16"),
            ("network without network.json", remove("net/network.json"), "network.json"),
            ("template not UTF-8", lambda root: (root / "templates/wp4.json").write_bytes(b'{"a": "\xff"}'), "utf-8"),
            ("template not JSON", replace("templates/G2-EOHZ35.json", '"stopDistances": []', '"x": ]'),
             "Expecting value"),
            ("NaN", replace(W08, "1.0734567890123457", "NaN"), "NaN is not valid JSON"),
            ("text not ASCII", replace("templates/wp4.json", "tab", "caf\u00e9"), "not ASCII"),
            ("unpaired surrogate", replace(W08, '"zoneIds"', r'"\ud800"'), "not ASCII"),
            ("number beyond a double", replace("templates/wp4.json", "1e7", "1e400"), "beyond the range"),
            ("integer beyond a double", replace(W17, '"O": []', '"O": [' + "9" * 400 + "]"), "beyond the range"),
            ("integer of 4,301 digits", replace("templates/wp4.json", "1e7", "9" * 4301), "wp4.json: "),
            ("nested 33 levels", nested(33), "nested more than 32 levels"),
            ("nested 5,000 levels", nested(5000), "nested more than 32 levels"),
            ("key twice, the first value not ASCII", replace("templates/wp4.json", '"10": true',
                                                         r'"10": "caf\u00e9", "10": true'), '"10" appears twice'),
            ("key twice, the first value not a number", replace(W08, '"kappa": ', '"kappa": "x", "kappa": '),
             '"kappa" appears twice'),
            ("key twice in a nested object", replace(W17, '"D": [7]', '"D": [[[7]]], "D": [7]'), '"D" appears twice'),
            ("gate cell twice", replace(GATE, '{"d20191218', '{"d20190919-vf-r0-w08": [1e400], "d20191218'),
             '"d20190919-vf-r0-w08" appears twice'),
            ("slack not 350", replace("templates/G2-RCE35.json", "350.0", "351"), "not 350"),
            ("fence not relaxed", replace("templates/G2-EOHZ35.json", "true", "false"), "not relaxed"),
            ("two policies", replace("templates/G2-EOHZ35.json", '"policies": [{', '"policies": [{}, {'),
             "list of one policy"),
            ("policies an object", both(replace("templates/G2-RCE35.json", '"policies": [{', '"policies": {"0": {'),
                                        replace("templates/G2-RCE35.json", "}],", '}, "length": 1},')),
             "list of one policy"),
            ("WP4 template not an object", lambda root: (root / "templates/wp4.json").write_text("[]"),
             "a JSON object is expected"),
            ("no shadow-inputs folder", lambda root: shutil.rmtree(root / "data/predictions/shadow-inputs"),
             "the folder is missing"),
            ("no cell", empty_inputs, "no cell"),
            ("file not named <cell>.json", add_file("data/predictions/shadow-inputs/notes.txt"), "only files named"),
            ("cell name in capitals", add_file("data/predictions/shadow-inputs/D20191218-vf-r2-w17.json"),
             "only files named"),
            ("folder named like a cell", lambda root: (root / "data/predictions/shadow-inputs/"
                                                       "d20191218-vf-r2-w17.json").mkdir(), "only files named"),
            ("cell not the file name", replace(W08, cell, '"cell": "d20190919-vf-r1-w08"'), '"cell"'),
            ("cell not a string", replace(W08, cell, '"cell": 12'), '"cell"'),
            ("kappa missing", replace(W08, '"kappa"', '"other"'), "kappa must be a number"),
            ("kappa a string", replace(W08, "1.0734567890123457", '"1.07"'), "kappa must be a number"),
            ("theta null", replace(W08, '"theta": 1e-7', '"theta": null'), "theta must be a number"),
            ("sampled true", replace(W08, '"sampled": 12', '"sampled": true'), "sampled must be a number"),
            ("omegaPms missing", replace(W08, '"omegaPms"', '"other"'), "omegaPms must be a number"),
            ("factor null", replace(W08, "[1, 0.1,", "[null, 0.1,"), "factors must be a list of numbers"),
            ("prior not an object", replace(W17, '{"O": [], "D": [7]}', "[7]"), "prior.O must be a list of numbers"),
            ("prior.D missing", replace(W17, '"D": [7]', '"E": [7]'), "prior.D must be a list of numbers"),
            ("forecast missing", replace(W17, '"forecast"', '"other"'), "forecast.profile must be a list of numbers"),
            ("globalOf with 5.0", replace(W17, "[5, 6]", "[5.0, 6]"), "globalOf must be"),
            ("globalOf with true", replace(W17, "[5, 6]", "[true, 6]"), "globalOf must be"),
            ("globalOf with a node twice", replace(W08, "[17, 4410,", "[17, 17,"), "globalOf must be"),
            ("globalOf outside the network", replace(W08, "4410", "4411"), "globalOf must be"),
            ("globalOf with a list", replace(W17, "[5, 6]", "[[5], 6]"), "globalOf must be"),
            ("gate profiles not an object", lambda root: (root / GATE).write_text("[[1]]"),
             "a JSON object is expected"),
            ("gate cell in a folder", replace(GATE, '"d20191218-vf-r2-w17"', '"sub/d20191218-vf-r2-w17"'),
             "is not a cell name"),
            ("gate cell 7", replace(GATE, '"d20191218-vf-r2-w17"', '"7"'), "is not a cell name"),
            ("gate cell with a drive", replace(GATE, '"d20191218-vf-r2-w17"', '"C:d20191218-vf-r2-w17"'),
             "is not a cell name"),
            ("gate cell unpaired surrogate", replace(GATE, '"d20191218-vf-r2-w17"', r'"\ud800"'), "not ASCII"),
            ("gate profile with null", replace(GATE, "[2]", "[null]"), "must be a list of numbers"),
            ("no gate profiles", remove(GATE), "forecast-gate-profiles.json"),
        ]
        for what, change, words, *extra in cases:
            with self.subTest(what), tempfile.TemporaryDirectory() as folder:
                root = pathlib.Path(folder)
                make_panel(root)
                if change:
                    change(root)
                before = sorted(root.rglob("*"))
                with self.assertRaises(SystemExit) as caught:
                    run(root, "--forecast-gate", *extra)
                self.assertTrue(str(caught.exception.code).startswith("refused: "), caught.exception.code)
                self.assertIn(words, str(caught.exception.code))
                self.assertEqual(sorted(root.rglob("*")), before)  # nothing written


if __name__ == "__main__":
    unittest.main()
