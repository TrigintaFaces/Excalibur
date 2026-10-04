#!/usr/bin/env python3
"""Compare each independently planned invocation with its own raw execution evidence.

This complements validate-shard-results.ps1's raw TRX structural checks. A matching
count or a passing sibling is insufficient: adapter IDs must match in each context.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
CONTEXT = ("candidateSha", "runId", "runAttempt", "job", "shard", "os", "provider", "source", "sourceSha256", "filter")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_json(path):
    def unique_pairs(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, f"Duplicate JSON field: {key}")
            result[key] = value
        return result
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_pairs)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def owned(root, relative):
    require(isinstance(relative, str) and relative and "\\" not in relative,
            "Evidence paths must be nonempty portable relative paths")
    require(not Path(relative).is_absolute() and not re.match(r"^[A-Za-z]:", relative), "Absolute evidence path")
    path = root.joinpath(relative).resolve()
    require(path.is_relative_to(root.resolve()), "Evidence path escapes invocation root")
    require(path.is_file(), f"Missing evidence file: {relative}")
    return path


def bound_file(root, reference):
    require(isinstance(reference, dict), "Missing file/hash binding")
    path = owned(root, reference["path"])
    require(re.fullmatch(r"[0-9a-fA-F]{64}", reference["sha256"]) is not None, "Invalid SHA256")
    require(digest(path) == reference["sha256"].lower(), f"Evidence hash mismatch: {reference['path']}")
    return path


def verify_source_roster(plan, expected, repo):
    """Reconcile the artifact roster against the independently checked-out source."""
    source = owned(repo, expected["source"])
    require(digest(source) == expected["sourceSha256"].lower(), "External source hash mismatch")
    if source.suffix.lower() == '.csproj':
        project_roster = {expected["source"]}
    else:
        require(source.suffix.lower() == '.slnf', "Unsupported external source roster")
        shard = read_json(source)["solution"]
        solution = (source.parent / shard["path"].replace('\\', '/')).resolve()
        projects = [(solution.parent / value.replace('\\', '/')).resolve() for value in shard["projects"]]
        require(projects and len(projects) == len(set(projects)) and all(path.is_relative_to(repo.resolve()) for path in projects),
                "Invalid external project roster")
        project_roster = {path.relative_to(repo.resolve()).as_posix() for path in projects}
    invoked = {row["project"] for row in plan["invocations"]}
    non_tests = plan["nonTestProjects"]
    require(all(row["evaluatedIsTestProject"] != "true" for row in non_tests), "Test project excluded as helper")
    helpers = {row["project"] for row in non_tests}
    require(not invoked & helpers and invoked | helpers == project_roster, "Planned project roster differs from external source")


def verify(plan_path: Path, expected: dict):
    root = plan_path.parent.resolve()
    plan = read_json(plan_path)
    require(plan.get("schemaVersion") == 1, "Unsupported plan schema")
    for key in CONTEXT:
        require(isinstance(expected.get(key), str) and (expected[key] or key == "filter"), f"Missing external context: {key}")
        require(plan["context"].get(key) == expected[key], f"Unexpected invocation context: {key}")
    require(re.fullmatch(r"[0-9a-f]{40}", expected["candidateSha"]) is not None, "Invalid candidate SHA")
    invocations = plan["invocations"]
    require(isinstance(invocations, list) and invocations, "Empty planned invocation roster")
    roster = read_json(bound_file(root, plan["evaluatedRoster"]))
    require(roster.get("schemaVersion") == 1 and roster.get("context") == plan["context"], "Evaluated roster context mismatch")
    rows = roster["entries"]
    require(isinstance(rows, list) and rows, "Empty evaluated project/TFM roster")
    evaluated, helpers = {}, set()
    evaluated_keys = set()
    for row in rows:
        key = (row["project"], row["targetFramework"])
        require(all(isinstance(value, str) and value for value in key) and key not in evaluated_keys,
                "Invalid/duplicate evaluated project/TFM")
        evaluated_keys.add(key)
        require(row["evaluatedIsTestProject"] in ("true", "false", ""), "Unknown evaluated test-project classification")
        if row["evaluatedIsTestProject"] == "true":
            evaluated[key] = row
        else:
            helpers.add(row["project"])
    expected_keys = {(row["project"], row["targetFramework"]) for row in invocations}
    require(expected_keys == set(evaluated) and len(expected_keys) == len(invocations),
            "Invocation plan does not exactly cover independently evaluated test project/TFM roster")
    helpers -= {key[0] for key in evaluated}
    require({row["project"] for row in plan["nonTestProjects"]} == helpers,
            "Helper disposition differs from independently evaluated roster")
    invocation_ids, identities, used_trx, run_ids = set(), set(), set(), set()
    total = 0
    plan_hash = digest(plan_path)
    for invocation in invocations:
        invocation_id = invocation["id"]
        require(isinstance(invocation_id, str) and invocation_id, "Empty invocation ID")
        require(invocation_id not in invocation_ids, "Duplicate planned invocation")
        invocation_ids.add(invocation_id)
        census_path = bound_file(root, invocation["census"])
        census = read_json(census_path)
        require(census.get("schemaVersion") == 1, "Unsupported census schema")
        require(census.get("purpose") == "execution", "Source-presence census cannot certify executed test identities")
        context = census["context"]
        require(census["filter"] == expected["filter"], "Discovery filter differs from planned execution")
        for key in CONTEXT:
            require(context.get(key) == expected[key], f"Census context mismatch: {key}")
        for key in ("targetFramework", "targetPlatform"):
            require(isinstance(invocation.get(key), str) and invocation[key], f"Missing planned {key}")
            require(context.get(key) == invocation[key], f"Census {key} mismatch")
        require(census["assembly"] == invocation["assembly"], "Census assembly mismatch")
        row = evaluated[(invocation["project"], invocation["targetFramework"])]
        require(row["targetPlatform"] == invocation["targetPlatform"] and row["assembly"] == invocation["assembly"],
                "Invocation platform/assembly differs from evaluated roster")
        settings_path = bound_file(root, row["effectiveSettings"])
        require(digest(settings_path) == census["runSettingsSha256"].lower(), "Census settings differ from evaluated roster")
        identity = (invocation["project"], invocation["targetFramework"], invocation["targetPlatform"])
        require(identity not in identities, "Duplicate project/TFM/platform invocation")
        identities.add(identity)
        tests = census["tests"]
        require(isinstance(tests, list) and census["discoveredCount"] == len(tests), "Census count mismatch")
        expected_ids = {row["id"] for row in tests}
        require(len(expected_ids) == len(tests), "Duplicate independently discovered ID")
        require(all(re.fullmatch(r"[0-9a-fA-F-]{36}", value) for value in expected_ids), "Invalid discovered ID")
        receipt_path = owned(root, invocation["receipt"])
        receipt = read_json(receipt_path)
        require(receipt.get("schemaVersion") == 1, "Unsupported receipt schema")
        require(receipt.get("planSha256") == plan_hash, "Execution receipt belongs to another plan")
        require(receipt.get("invocationId") == invocation_id, "Execution receipt belongs to another invocation")
        require(receipt.get("exitCode") == 0 and type(receipt["exitCode"]) is int, "Test runner did not succeed")
        require(receipt.get("censusSha256", "").lower() == digest(census_path), "Execution census binding mismatch")
        for field in ("assemblySha256", "adapterSha256", "runSettingsSha256"):
            require(re.fullmatch(r"[0-9a-fA-F]{64}", census[field]) is not None, f"Invalid {field}")
            require(receipt["before"][field] == census[field] == receipt["after"][field], f"Execution inputs changed: {field}")
        require(census["inputBundle"] and receipt["before"]["inputBundle"] == census["inputBundle"] == receipt["after"]["inputBundle"],
                "Execution dependency/configuration/data bundle changed")
        observed = set()
        trx_refs = receipt["trx"]
        require(isinstance(trx_refs, list), "Invalid TRX roster")
        if not expected_ids:
            # A known filtered zero is evidence of exclusion, never a positive executed count.
            unfiltered = read_json(bound_file(root, invocation["unfilteredCensus"]))
            require(unfiltered.get("purpose") == "source-presence" and unfiltered["filter"] == "" and unfiltered["discoveredCount"] > 0,
                    "Empty filtered source lacks positive unfiltered discovery")
            require(unfiltered["context"] == context and unfiltered["assemblySha256"] == census["assemblySha256"],
                    "Unfiltered census belongs to another source/context")
            require(not trx_refs and receipt.get("disposition") == "filtered-zero", "Empty source execution is ambiguous")
        else:
            require(trx_refs and receipt.get("disposition") == "executed", "Required invocation did not execute")
        for reference in trx_refs:
            path = bound_file(root, reference)
            require(path not in used_trx, "TRX reused by multiple invocations")
            used_trx.add(path)
            xml_bytes = path.read_bytes()
            require(b"<!DOCTYPE" not in xml_bytes.upper() and b"<!ENTITY" not in xml_bytes.upper(), "Unsupported XML entities")
            tree = ET.fromstring(xml_bytes)
            require(tree.tag == f"{{{NS['t']}}}TestRun", "Unsupported TRX namespace/root")
            run_id = tree.get("id")
            require(run_id and run_id not in run_ids, "Duplicate/missing TRX run ID")
            run_ids.add(run_id)
            definitions = tree.findall("t:TestDefinitions/t:UnitTest", NS)
            definition_ids = set()
            for definition in definitions:
                test_id = definition.get("id")
                require(test_id not in definition_ids, "Duplicate TRX definition")
                definition_ids.add(test_id)
                storage = definition.get("storage", "").replace("\\", "/").split("/")[-1]
                require(storage.lower() == census["assembly"].lower(), "TRX contains an unexpected assembly")
            results = tree.findall("t:Results/t:UnitTestResult", NS)
            require(len(tree.findall(".//t:UnitTestResult", NS)) == len(results), "Unsupported deferred/nested results")
            for result in results:
                test_id = result.get("testId")
                require(test_id in definition_ids and test_id not in observed, "Orphan/duplicate execution ID")
                require(result.get("outcome") == "Passed", "Required test did not pass")
                observed.add(test_id)
        require(observed == expected_ids, f"Independent test identity mismatch for {invocation_id}: "
                f"missing={sorted(expected_ids-observed)}, unexpected={sorted(observed-expected_ids)}")
        total += len(observed)
    require(total > 0, "Required job executed zero independently expected tests")
    require({p.resolve() for p in root.rglob('*.trx')} == used_trx, "Unclaimed TRX artifacts in invocation evidence")
    return {"invocations": len(invocations), "executed": total, "planSha256": plan_hash}


def verify_unit_artifacts(directory, repo, candidate, run_id, attempt, test_filter):
    # Reuse the release gate's fail-closed workflow inventory, then bind every unit job
    # artifact to its declared source. Observed artifacts never define the expected roster.
    spec = importlib.util.spec_from_file_location("release_gate", Path(__file__).with_name("release-test-verdict-gate.py"))
    release = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(release)
    workflow = (repo / ".github/workflows/ci.yml").read_text(encoding="utf-8-sig")
    release.verify_workflow(workflow)
    sections = release.job_sections(workflow)
    expected = []
    for job in ("unit-tests", "unit-tests-async-risk"):
        body = sections[job].split("    strategy:", 1)[1].split("    steps:", 1)[0]
        rows = re.findall(r'^          - \{ name: ([a-z0-9-]+), filter: "([^"]+)"(?:, extra-run-settings: "[^"]*")? \}$', body, re.M)
        require([row[0] for row in rows] == list(release.JOBS[job][1]), "Unsupported unit matrix source shape")
        for name, source in rows:
            suffix = ("async-risk-" if job.endswith("async-risk") else "") + name
            expected.append((f"test-results-unit-{suffix}", job, f"unit-tests-{suffix}", "Linux", source))
    for os_name, platform in (("Windows", "windows"), ("macOS", "macos")):
        for suffix, source in (("", "eng/ci/shards/UnitTests-Deterministic.slnf"), ("-async-risk", "eng/ci/shards/UnitTests-AsyncRisk.slnf")):
            job = f"unit-tests-{platform}{suffix}"
            require(f"          solution-filter: {source}\n" in sections[job], "Platform unit source changed")
            shard = f"unit-tests-{platform}-{'async-risk' if suffix else 'deterministic'}"
            expected.append((shard.replace("unit-tests-", "test-results-unit-", 1), job, shard, os_name, source))
    require({p.name for p in directory.iterdir() if p.is_dir()} == {row[0] for row in expected},
            "Missing or unexpected unit artifact context")
    reports = []
    for artifact, job, shard, os_name, source in expected:
        artifact_root = directory / artifact
        plans = list(artifact_root.rglob("plan.json"))
        require(len(plans) == 1, f"Expected exactly one plan for {artifact}")
        source_path = repo / source
        context = dict(candidateSha=candidate, runId=run_id, runAttempt=attempt, job=job,
                       shard=shard, os=os_name, provider="in-process", source=source,
                       sourceSha256=digest(source_path).upper(), filter=test_filter)
        plan = read_json(plans[0])
        verify_source_roster(plan, context, repo)
        reports.append(verify(plans[0], context))
        require({p.resolve() for p in artifact_root.rglob("*.trx")} ==
                {p.resolve() for p in plans[0].parent.rglob("*.trx")}, "Unbound results outside invocation plan")
    return {"jobs": len(reports), "executed": sum(row["executed"] for row in reports)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--expected-context', type=Path)
    parser.add_argument('--unit-artifacts', type=Path)
    parser.add_argument('--repo-root', type=Path, default=Path.cwd())
    parser.add_argument('--candidate')
    parser.add_argument('--run-id')
    parser.add_argument('--attempt')
    parser.add_argument('--filter', default='')
    args = parser.parse_args()
    try:
        if args.unit_artifacts:
            require(not args.plan and not args.expected_context and args.candidate and args.run_id and args.attempt,
                    "Unit aggregation needs externally expected candidate/run/attempt")
            report = verify_unit_artifacts(args.unit_artifacts, args.repo_root, args.candidate, args.run_id, args.attempt, args.filter)
        else:
            require(args.plan and args.expected_context, "Provide a plan and expected context")
            expected = read_json(args.expected_context)
            verify_source_roster(read_json(args.plan), expected, args.repo_root)
            report = verify(args.plan, expected)
        # The denominator was already computed and already guarded (require(total > 0, ...)); it was
        # simply never labelled. Both branches report the population they walked -- planned
        # invocations or unit jobs -- plus the tests actually executed within it, so a verdict
        # reached over an empty roster cannot read like one earned over a real roster.
        # stderr, because stdout carries the JSON report a caller parses.
        population = report.get('invocations', report.get('jobs', 0))
        label = 'planned invocation(s)' if 'invocations' in report else 'unit job(s)'
        print(f"EXAMINED: {population} {label}, {report['executed']} executed test(s)",
              file=sys.stderr)
        print(json.dumps(report, sort_keys=True))
    except (ValueError, KeyError, TypeError, OSError, ET.ParseError) as error:
        parser.exit(2, f"REFUSE: {error}\n")


if __name__ == '__main__':
    main()
