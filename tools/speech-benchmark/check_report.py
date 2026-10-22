"""Check complete, pinned, internally consistent fixed-reference comparison evidence."""

import argparse
import hashlib
import json
import math
from pathlib import Path

BUNDLE = Path(__file__).resolve().parents[2] / "tests" / "fixtures" / "speech" / "v1.0.0"
POLICY = "unicode-nfc-lower-punctuation-space-v1"


def check(report):
    payload = (BUNDLE / "manifest.json").read_bytes()
    manifest = json.loads(payload)
    expected = {fixture["id"]: fixture for fixture in manifest["fixtures"]}
    if (report["complete"] is not True or report["bundleVersion"] != "1.0.0"
            or report["manifestSha256"] != hashlib.sha256(payload).hexdigest()
            or report["scoringPolicy"] != POLICY):
        raise ValueError("Incomplete or incorrectly pinned benchmark")
    clips = report["clips"]
    if (len(clips) != 20 or {clip["id"] for clip in clips} != set(expected)
            or set(report["languages"]) != {"en_us", "es_419"}):
        raise ValueError("Incomplete fixture coverage")
    fields = ("substitutions", "deletions", "insertions", "referenceCount", "hypothesisCount")
    for clip in clips:
        fixture = expected[clip["id"]]
        if (clip["reference"] != fixture["transcript"] or clip["language"] != fixture["language"]
                or not clip["hypothesis"].strip()
                or not math.isfinite(clip["inferenceSeconds"]) or clip["inferenceSeconds"] < 0):
            raise ValueError("Incorrect reference, hypothesis, or timing")
    for score in [*(clip["score"] for clip in clips), *report["languages"].values()]:
        for kind in ("words", "characters"):
            counts = score[kind]
            if any(type(counts[field]) is not int or counts[field] < 0 for field in fields):
                raise ValueError("Invalid edit counts")
            errors = counts["substitutions"] + counts["deletions"] + counts["insertions"]
            if (counts["referenceCount"] <= 0 or counts["errors"] != errors
                    or not math.isfinite(counts["rate"])
                    or abs(counts["rate"] - errors / counts["referenceCount"]) > 1e-12):
                raise ValueError("Inconsistent edit counts/rate")
    for language, score in report["languages"].items():
        selected = [clip for clip in clips if clip["language"] == language]
        if len(selected) != 10:
            raise ValueError("Incomplete language coverage")
        for kind in ("words", "characters"):
            for field in fields:
                if score[kind][field] != sum(clip["score"][kind][field] for clip in selected):
                    raise ValueError("Incorrect aggregate")
    if "realTimeFactor" in report:
        if any(not math.isfinite(report[field]) for field in ("inferenceSeconds", "audioSeconds", "realTimeFactor")):
            raise ValueError("Non-finite total duration/RTF")
        total = sum(clip["inferenceSeconds"] for clip in clips)
        seconds = sum(fixture["durationSeconds"] for fixture in manifest["fixtures"])
        if (abs(total - report["inferenceSeconds"]) > 1e-9
                or abs(seconds - report["audioSeconds"]) > 1e-9
                or abs(total / seconds - report["realTimeFactor"]) > 1e-9):
            raise ValueError("Incorrect total duration/RTF")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    args = parser.parse_args()
    check(json.loads(args.report.read_text(encoding="utf-8")))
    print("Complete, consistently scored fixed-reference evidence:", args.report)
