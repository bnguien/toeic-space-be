#!/usr/bin/env python3
"""Read-only MySQL preservation check for the local PBL-26 test workflow.

Before tests: python scripts/verify_practice_set_data.py --baseline PATH --record
After tests:  python scripts/verify_practice_set_data.py --baseline PATH

Only test-created PBL26-SMOKE-* sets and their links are excluded. Hashes cover all
columns, including original ordering, ownership and timestamps. SQL is read-only;
credentials stay inside the existing database container and are never printed.
"""

import argparse
import hashlib
import json
import subprocess
from pathlib import Path


def capture(container):
    result = {}
    tables = ["ToeicPracticeSets", "ToeicPracticeSetItems", "ToeicQuestions",
              "ToeicPassages", "ToeicAttempts", "ToeicAttemptAnswers", "__EFMigrationsHistory"]
    for table in tables:
        condition = ""
        if table == "ToeicPracticeSets":
            condition = " WHERE Code NOT LIKE 'PBL26-SMOKE-%'"
        elif table == "ToeicPracticeSetItems":
            condition = " WHERE PracticeSetId IN (SELECT Id FROM ToeicPracticeSets WHERE Code NOT LIKE 'PBL26-SMOKE-%')"
        query = f"SELECT * FROM `{table}`{condition} ORDER BY 1,2"
        data = subprocess.check_output([
            "docker", "exec", container, "sh", "-c",
            'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql -uroot "$MYSQL_DATABASE" -N -B -e "$1"',
            "sh", query])
        result[table] = {"rows": len(data.splitlines()), "sha256": hashlib.sha256(data).hexdigest()}
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True, type=Path)
    parser.add_argument("--container", default="toeic-space-be-assessment-db-1")
    parser.add_argument("--record", action="store_true")
    args = parser.parse_args()
    current = capture(args.container)
    if args.record:
        if args.baseline.exists():
            parser.error("Baseline already exists; choose a new path to preserve the original evidence.")
        args.baseline.write_text(json.dumps(current, indent=2), encoding="utf-8")
        print("Recorded", args.baseline)
    else:
        baseline = json.loads(args.baseline.read_text(encoding="utf-8"))
        for table, expected in baseline.items():
            assert current[table] == expected, f"Original data changed in {table}: {current[table]} != {expected}"
            print(f"PASS {table}: {expected['rows']} original rows, SHA-256 unchanged", flush=True)
        print("PASS original Assessment data and migration history preserved")
