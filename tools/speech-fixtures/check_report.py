"""Fail if a real-model fixture report is missing, incomplete, or internally inconsistent."""

import argparse
import json
import math
from pathlib import Path

import fixtures


def check_report(path):
    report = json.loads(path.read_text(encoding="utf-8"))
    manifest_bytes = (fixtures.DEFAULT_BUNDLE / "manifest.json").read_bytes()
    manifest = json.loads(manifest_bytes)
    if (report["complete"] is not True or report["expectedClips"] != 20
            or report["evaluatedClips"] != 20 or len(report["clips"]) != 20
            or len({clip["id"] for clip in report["clips"]}) != 20
            or set(report["languages"]) != {"en_us", "es_419"}):
        raise ValueError("Evaluation did not cover every fixture")
    expected = {entry["id"]: entry for entry in manifest["fixtures"]}
    if (report["manifestSha256"] != fixtures.sha(manifest_bytes)
            or report["bundleVersion"] != manifest["bundleVersion"]
            or report["revision"] != manifest["revision"]
            or {clip["id"] for clip in report["clips"]} != set(expected)):
        raise ValueError("Report does not match the pinned fixture bundle")
    for clip in report["clips"]:
        reference = expected[clip["id"]]
        if (clip["language"] != reference["language"] or clip["sourceId"] != reference["sourceId"]
                or clip["reference"] != reference["transcript"] or not clip["hypothesis"].strip()):
            raise ValueError("Clip report has incorrect references or missing hypotheses")
    if report["thresholdStatus"] not in ("proposed", "approved"):
        raise ValueError("Report is not a configured regression evaluation")
    if report["acceptedAccuracyGate"] != (report["thresholdStatus"] == "approved"):
        raise ValueError("Contradictory accuracy-gate status")
    if set(report["proposedThresholdComparisons"]) != {"en_us", "es_419"}:
        raise ValueError("Missing per-language threshold comparisons")
    for language in ("en_us", "es_419"):
        if sum(clip["language"] == language for clip in report["clips"]) != 10:
            raise ValueError(f"Incomplete coverage for {language}")
    for score in [report["corpus"], *report["languages"].values(),
                  *(clip["score"] for clip in report["clips"])]:
        for kind in ("words", "characters"):
            counts = score[kind]
            rate = counts["rate"]
            errors = counts["substitutions"] + counts["deletions"] + counts["insertions"]
            if (counts["referenceCount"] <= 0 or counts["errors"] != errors
                    or not math.isfinite(rate)
                    or abs(rate - errors / counts["referenceCount"]) > 1e-12):
                raise ValueError("Invalid error counts or rate")
    for language, aggregate in [("all", report["corpus"]), *report["languages"].items()]:
        clips = [clip for clip in report["clips"] if language == "all" or clip["language"] == language]
        for kind in ("words", "characters"):
            for field in ("substitutions", "deletions", "insertions", "referenceCount", "hypothesisCount"):
                if aggregate[kind][field] != sum(clip["score"][kind][field] for clip in clips):
                    raise ValueError("Aggregate error counts differ from clip results")
    if report["acceptedAccuracyGate"] and not all(report["proposedThresholdComparisons"].values()):
        raise ValueError("Approved accuracy limits exceeded")
    if not report["acceptedAccuracyGate"] and not all(report["proposedThresholdComparisons"].values()):
        print("::warning::Proposed regression limits exceeded; limits still require review.")
    print(f"Complete evaluation; threshold status: {report['thresholdStatus']}; "
          f"within proposed limits: {report['proposedThresholdComparisons']}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    check_report(parser.parse_args().report)
