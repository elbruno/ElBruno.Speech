import copy
import hashlib
import json
import unittest

import check_report


class ReportTests(unittest.TestCase):
    def setUp(self):
        payload = (check_report.BUNDLE / "manifest.json").read_bytes()
        manifest = json.loads(payload)
        clips = []
        for fixture in manifest["fixtures"]:
            counts = dict(substitutions=0, deletions=0, insertions=0, referenceCount=1,
                          hypothesisCount=1, errors=0, rate=0)
            clips.append(dict(id=fixture["id"], language=fixture["language"],
                              reference=fixture["transcript"], hypothesis=fixture["transcript"],
                              inferenceSeconds=1, score=dict(words=counts, characters=counts)))
        aggregate = dict(substitutions=0, deletions=0, insertions=0, referenceCount=10,
                         hypothesisCount=10, errors=0, rate=0)
        self.report = dict(complete=True, bundleVersion="1.0.0",
                           manifestSha256=hashlib.sha256(payload).hexdigest(),
                           scoringPolicy=check_report.POLICY, clips=clips,
                           languages={language: dict(words=aggregate, characters=aggregate)
                                      for language in ("en_us", "es_419")})

    def test_accepts_internally_consistent_complete_report(self):
        check_report.check(self.report)

    def test_rejects_partial_coverage_modified_references_and_changed_policy(self):
        for change in (
            lambda r: r.update(complete=False),
            lambda r: r["clips"].pop(),
            lambda r: r["clips"][0].update(reference="not the source"),
            lambda r: r.update(scoringPolicy="changed"),
            lambda r: r["clips"][0].update(inferenceSeconds=-1),
            lambda r: r["clips"][0].update(hypothesis=""),
        ):
            report = copy.deepcopy(self.report)
            change(report)
            with self.assertRaises(ValueError):
                check_report.check(report)

    def test_rejects_inconsistent_rates_and_aggregates(self):
        for change in (
            lambda r: r["clips"][0]["score"]["words"].update(rate=1),
            lambda r: r["languages"]["en_us"]["words"].update(referenceCount=9),
        ):
            report = copy.deepcopy(self.report)
            change(report)
            with self.assertRaises(ValueError):
                check_report.check(report)

    def test_checks_rtf_against_complete_audio_duration_and_rejects_nan(self):
        manifest = json.loads((check_report.BUNDLE / "manifest.json").read_bytes())
        seconds = sum(f["durationSeconds"] for f in manifest["fixtures"])
        self.report.update(audioSeconds=seconds, inferenceSeconds=20, realTimeFactor=20 / seconds)
        check_report.check(self.report)
        for invalid in (float("nan"), 0, 1):
            report = copy.deepcopy(self.report)
            report["realTimeFactor"] = invalid
            with self.assertRaises(ValueError):
                check_report.check(report)


if __name__ == "__main__":
    unittest.main()
