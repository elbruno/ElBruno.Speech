import contextlib
import io
import json
from pathlib import Path
import shutil
import struct
import tempfile
import unittest
import zipfile

import fixtures
from check_report import check_report


class FixtureToolsTests(unittest.TestCase):
    def test_committed_bundle(self):
        fixtures.verify(fixtures.DEFAULT_BUNDLE)

    def test_pack_is_deterministic_and_roundtrips(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ("first", "second"):
                fixtures.pack(fixtures.DEFAULT_BUNDLE, root / name)
            first = (root / "first" / "speech-fixtures-v1.0.0.zip").read_bytes()
            self.assertEqual(first, (root / "second" / "speech-fixtures-v1.0.0.zip").read_bytes())
            with zipfile.ZipFile(io.BytesIO(first)) as archive:
                archive.extractall(root / "extracted")
            fixtures.verify(root / "extracted")
            with self.assertRaises(ValueError):
                fixtures.pack(fixtures.DEFAULT_BUNDLE, root / "first")

    def test_rejects_paths(self):
        for path in ("../x", "/x", "a\\b", "a//b", "C:/x", "a/./b"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                fixtures.safe_path(fixtures.DEFAULT_BUNDLE, path)

    def test_rejects_corruption_and_extra_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            bundle = Path(temporary) / "bundle"
            shutil.copytree(fixtures.DEFAULT_BUNDLE, bundle)
            (bundle / "extra.txt").write_text("unexpected")
            with self.assertRaises(ValueError):
                fixtures.verify(bundle)
            (bundle / "extra.txt").unlink()
            audio = next(bundle.rglob("*.wav"))
            audio.write_bytes(b"broken")
            with self.assertRaises(ValueError):
                fixtures.verify(bundle)

    def test_float_conversion_rounding_and_clamping(self):
        values = [-2.0, -1.0, -0.5, 0, 0.5, 1.0, 2.0]
        data = struct.pack("<7f", *values)
        source = (b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVEfmt " +
                  struct.pack("<IHHIIHH", 16, 3, 1, 16000, 64000, 4, 32) +
                  b"data" + struct.pack("<I", len(data)) + data)
        output, count, _ = fixtures.pcm16(source)
        self.assertEqual(7, count)
        self.assertEqual((-32768, -32768, -16384, 0, 16384, 32767, 32767),
                         struct.unpack("<7h", output[44:]))

    def test_enforces_actual_uncompressed_size(self):
        with tempfile.TemporaryDirectory() as temporary:
            bundle = Path(temporary) / "bundle"
            shutil.copytree(fixtures.DEFAULT_BUNDLE, bundle)
            notice = b"x" * (fixtures.LIMIT + 1)
            (bundle / "README.md").write_bytes(notice)
            path = bundle / "manifest.json"
            manifest = json.loads(path.read_text(encoding="utf-8"))
            manifest["notices"]["README.md"] = fixtures.sha(notice)
            path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "exceeds 20 MiB"):
                fixtures.verify(bundle)

    def test_limited_stream_rejects_full_archive_reads(self):
        reader = fixtures.LimitedReader(io.BytesIO())
        with self.assertRaises(ValueError):
            reader.read(33 * 1024 * 1024)

    def test_report_rejects_empty_coverage(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "report.json"
            path.write_text(json.dumps({"complete": False}))
            with self.assertRaises(ValueError):
                check_report(path)

    def test_report_checks_real_ids_and_approved_limits(self):
        manifest_bytes = (fixtures.DEFAULT_BUNDLE / "manifest.json").read_bytes()
        manifest = json.loads(manifest_bytes)

        def score(count):
            counts = {"substitutions": 0, "deletions": 0, "insertions": 0,
                      "referenceCount": count, "hypothesisCount": count, "errors": 0, "rate": 0}
            return {"words": dict(counts), "characters": dict(counts)}

        report = {
            "complete": True, "expectedClips": 20, "evaluatedClips": 20,
            "manifestSha256": fixtures.sha(manifest_bytes), "bundleVersion": "1.0.0",
            "revision": fixtures.REVISION, "thresholdStatus": "proposed", "acceptedAccuracyGate": False,
            "proposedThresholdComparisons": {"en_us": True, "es_419": True},
            "languages": {"en_us": score(10), "es_419": score(10)}, "corpus": score(20),
            "clips": [{"id": entry["id"], "language": entry["language"], "sourceId": entry["sourceId"],
                       "reference": entry["transcript"], "hypothesis": entry["transcript"], "score": score(1)}
                      for entry in manifest["fixtures"]],
        }
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "report.json"
            path.write_text(json.dumps(report), encoding="utf-8")
            check_report(path)
            report["clips"][0]["id"] = "unrelated-fixture"
            path.write_text(json.dumps(report), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "pinned fixture"):
                check_report(path)
            report["clips"][0]["id"] = manifest["fixtures"][0]["id"]
            report["thresholdStatus"] = "approved"
            report["acceptedAccuracyGate"] = True
            report["proposedThresholdComparisons"]["en_us"] = False
            path.write_text(json.dumps(report), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Approved accuracy"):
                check_report(path)

    def test_fixture_releases_do_not_route_to_nuget(self):
        workflow = (fixtures.ROOT / ".github" / "workflows" / "publish.yml").read_text(encoding="utf-8")
        guard = "if: github.event_name != 'release' || !startsWith(github.event.release.tag_name, 'speech-fixtures-')"
        self.assertIn(guard, workflow)
        for event, tag, expected in (
                ("release", "speech-fixtures-v1.0.0", False),
                ("release", "v1.6.0-preview.1", True),
                ("workflow_dispatch", "", True)):
            self.assertEqual(expected, event != "release" or not tag.startswith("speech-fixtures-"))


if __name__ == "__main__":
    with contextlib.redirect_stdout(io.StringIO()):
        unittest.main()
