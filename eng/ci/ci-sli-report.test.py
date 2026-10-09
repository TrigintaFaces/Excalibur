#!/usr/bin/env python3
"""Population and evidence controls for the CI SLI report; no network access."""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import textwrap
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('sli', Path(__file__).with_name('ci-sli-report.py'))
sli = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sli)


def run(identity=1):
    return {'id': identity, 'event': 'pull_request', 'head_branch': 'feature',
            'repository': {'full_name': 'owner/repo'}, 'path': '.github/workflows/ci.yml',
            'status': 'completed', 'conclusion': 'success', 'run_attempt': 1, 'head_sha': 'a'*40,
            'created_at': '2026-01-01T00:00:00Z', 'run_started_at': '2026-01-01T00:01:00Z',
            'updated_at': '2026-01-01T00:10:00Z'}


def response(rows, code=0):
    return subprocess.CompletedProcess([], code, json.dumps(rows), 'synthetic error' if code else '')


class PopulationControls(unittest.TestCase):
    def classification(self, attempt=2, cause='infrastructure'):
        return {'repository': 'owner/repo', 'run_id': 1, 'head_sha': 'a'*40, 'attempt': attempt,
                'cause': cause, 'rationale': 'Synthetic reviewed fixture', 'evidence': 'https://example.invalid/evidence',
                'reviewer': 'fixture reviewer', 'reviewed_at': '2026-01-02T00:00:00Z'}

    def test_rerun_incidence_positive_negative_and_unknown(self):
        rows = [{**run(), 'run_attempt': 2}, run(2)]
        for cause, expected in [('infrastructure', 50), ('product', 0), ('operator', 0), ('unknown', None)]:
            result = sli.rerun_incidence('owner/repo', rows, {'records': [self.classification(cause=cause)]})
            self.assertEqual(expected, result['infrastructure_rerun_rate'])
        result = sli.rerun_incidence('owner/repo', rows, {'records': []})
        self.assertIsNone(result['infrastructure_rerun_rate'])
        self.assertEqual(1, result['unknown_runs'])

    def test_rerun_incidence_strict_all_transition_policy(self):
        result = sli.rerun_incidence('owner/repo', [{**run(), 'run_attempt': 3}], {'records': [self.classification()]})
        self.assertEqual(1, result['infrastructure_runs'])
        self.assertEqual(2, result['expected_transitions'])
        self.assertEqual(1, result['classified_transitions'])
        self.assertIsNone(result['infrastructure_rerun_rate'])

    def test_rerun_incidence_empty_pending_and_invalid_attempt(self):
        for rows in ([], [{**run(), 'status': 'in_progress'}]):
            self.assertIsNone(sli.rerun_incidence('owner/repo', rows, {'records': []})['infrastructure_rerun_rate'])
        self.assertEqual(0, sli.rerun_incidence('owner/repo', [run()], {'records': []})['infrastructure_rerun_rate'])
        for attempt in (None, True, 0, -1, '1'):
            with self.assertRaises(ValueError):
                sli.rerun_incidence('owner/repo', [{**run(), 'run_attempt': attempt}], {'records': []})

    def test_rerun_classification_stale_sha_and_future_attempt_refuse(self):
        for record in ({**self.classification(), 'head_sha': 'b'*40}, self.classification(attempt=3)):
            with self.assertRaises(ValueError):
                sli.rerun_incidence('owner/repo', [{**run(), 'run_attempt': 2}], {'records': [record]})

    def test_rerun_classification_file_requires_unique_provenance(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'causes.json'
            path.write_text(json.dumps({'version': 1, 'records': [self.classification()]}))
            loaded = sli.load_rerun_classifications(path)
            self.assertEqual(64, len(loaded['sha256']))
            for records in ([self.classification(), self.classification(cause='product')],
                            [{**self.classification(), 'attempt': True}],
                            [{**self.classification(), 'reviewer': ''}],
                            [{**self.classification(), 'evidence': ''}],
                            [{**self.classification(), 'reviewed_at': 'not a timestamp'}]):
                path.write_text(json.dumps({'version': 1, 'records': records}))
                with self.assertRaises(ValueError):
                    sli.load_rerun_classifications(path)

    def test_strict_percentage_boundary_is_not_at_most(self):
        objective = {'workflow': 'ci.yml', 'metric': 'infrastructure_rerun_rate', 'comparison': 'less-than', 'value': 2}
        self.assertEqual(sli.MISSED, sli.score(objective, {'ci.yml': {'completed': 50, 'infrastructure_rerun_rate': 2}})[0])
        self.assertEqual(sli.MET, sli.score(objective, {'ci.yml': {'completed': 50, 'infrastructure_rerun_rate': 0}})[0])

    def test_nightly_report_step_propagates_reporter_exit(self):
        workflow = (Path(__file__).resolve().parents[2] / '.github/workflows/nightly.yml').read_text(encoding='utf-8')
        job = workflow.split('\n  ci-sli-report:\n', 1)[1].split('\n  nightly-summary:', 1)[0]
        block = job.split('      - name: Report\n', 1)[1].split('      - name: Upload SLI measurement evidence', 1)[0]
        script = textwrap.dedent(block.split('        run: |\n', 1)[1])
        bash = str(Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/bin/bash.exe') if os.name == 'nt' else shutil.which('bash')
        self.assertTrue(bash and Path(bash).is_file())
        with tempfile.TemporaryDirectory() as directory:
            for code in (0, 1, 2):
                with self.subTest(code=code):
                    env = dict(os.environ, GITHUB_REPOSITORY='owner/repo',
                               GITHUB_STEP_SUMMARY=(Path(directory) / 'summary').as_posix())
                    prefix = f"python3() {{ echo synthetic-report; return {code}; }}\n"
                    result = subprocess.run([bash, '--noprofile', '--norc', '-c', prefix + script],
                                            cwd=directory, env=env, capture_output=True, text=True, timeout=20)
                    self.assertEqual(code, result.returncode, result.stderr)

    def test_runner_usage_overlap_and_touching_intervals(self):
        jobs = [self.job(11), {**self.job(12), 'started_at': '2026-01-01T00:08:30Z', 'completed_at': '2026-01-01T00:10:00Z'},
                {**self.job(13), 'started_at': '2026-01-01T00:10:00Z', 'completed_at': '2026-01-01T00:11:00Z'}]
        usage = sli.runner_usage({**run(), '_jobs': jobs})
        self.assertEqual(3.5, usage['runner_minutes'])
        self.assertEqual(3, usage['busy_wall_minutes'])
        self.assertEqual(2, usage['peak_jobs'])
        self.assertEqual(12, usage['jobs'][0]['id'])

    def test_runner_usage_missing_timing_never_becomes_zero(self):
        for data in ({**run(), '_jobs': [self.job(), {**self.job(12), 'completed_at': None}]},
                     {**run(), 'status': 'in_progress'}, {**run(), '_jobs': []}):
            with self.subTest(data=data):
                usage = sli.runner_usage(data)
                self.assertIsNone(usage['runner_minutes'])
                self.assertIsNone(usage['peak_jobs'])
                self.assertTrue(usage['missing'])

    def test_nested_zero_length_and_cancelled_intervals(self):
        jobs = [self.job(), {**self.job(12), 'started_at': '2026-01-01T00:08:15Z',
                            'completed_at': '2026-01-01T00:08:45Z', 'conclusion': 'cancelled'},
                {**self.job(13), 'started_at': '2026-01-01T00:09:00Z', 'completed_at': '2026-01-01T00:09:00Z'}]
        usage = sli.runner_usage({**run(), '_jobs': jobs})
        self.assertEqual(1.5, usage['runner_minutes'])
        self.assertEqual(1, usage['busy_wall_minutes'])
        self.assertEqual(2, usage['peak_jobs'])

    def test_reporter_failure_preserves_inputs_and_refused_state(self):
        with tempfile.TemporaryDirectory() as directory:
            path = str(Path(directory) / 'evidence.json')
            with patch.object(sli.sys, 'argv', ['report', '--repo', 'owner/repo', '--workflows', 'ci.yml',
                                               '--job-details', '--evidence-file', path]), \
                    patch.object(sli, 'API_DEADLINE', None), \
                    patch.object(sli, 'fetch_runs', return_value=[run()]), \
                    patch.object(sli, 'fetch_attempt_jobs', side_effect=ValueError('later page refused')), \
                    contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(sli.EXIT_REFUSE, sli.main())
            saved = json.loads(Path(path).read_text())
            self.assertEqual('refused', saved['status'])
            self.assertEqual(1, saved['populations'][0]['runs'][0]['id'])
            self.assertEqual('later page refused', saved['reason'])

    def test_reporter_malformed_attempt_refuses_with_input_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            path = str(Path(directory) / 'evidence.json')
            with patch.object(sli.sys, 'argv', ['report', '--repo', 'owner/repo', '--workflows', 'ci.yml',
                                               '--evidence-file', path]), \
                    patch.object(sli, 'API_DEADLINE', None), \
                    patch.object(sli, 'fetch_runs', return_value=[{**run(), 'run_attempt': '2'}]), \
                    contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(sli.EXIT_REFUSE, sli.main())
            saved = json.loads(Path(path).read_text())
            self.assertEqual('refused', saved['status'])
            self.assertEqual('2', saved['populations'][0]['runs'][0]['run_attempt'])
            self.assertIn('positive integer attempt', saved['reason'])

    def test_skipped_jobs_have_no_execution_occupancy(self):
        usage = sli.runner_usage({**run(), '_jobs': [{**self.job(), 'conclusion': 'skipped', 'started_at': None, 'completed_at': None, 'steps': []}]})
        self.assertEqual(1, usage['skipped_jobs'])
        self.assertEqual(0, usage['runner_minutes'])
        self.assertEqual(0, usage['peak_jobs'])

    def test_skipped_execution_conflict_prevents_totals(self):
        for job in ({**self.job(), 'conclusion': 'skipped'},
                    {**self.job(), 'conclusion': 'skipped', 'steps': [], 'started_at': 'bad'},
                    {**self.job(), 'conclusion': 'skipped', 'steps': [], 'completed_at': None}):
            usage = sli.runner_usage({**run(), '_jobs': [job]})
            self.assertIsNone(usage['runner_minutes'])
            self.assertTrue(usage['missing'])

    def test_skipped_cancelled_timed_out_or_running_steps_are_unknown(self):
        for status, conclusion in [('completed', 'cancelled'), ('completed', 'timed_out'),
                                   ('in_progress', None), ('queued', None)]:
            with self.subTest(status=status, conclusion=conclusion):
                job = {**self.job(), 'conclusion': 'skipped', 'started_at': None, 'completed_at': None,
                       'steps': [{'status': status, 'conclusion': conclusion}]}
                usage = sli.runner_usage({**run(), '_jobs': [job]})
                self.assertIsNone(usage['runner_minutes'])
                self.assertTrue(usage['missing'])

    def test_collection_deadline_bounds_api_and_refuses_before_call(self):
        with patch.object(sli, 'API_DEADLINE', 105), patch.object(sli.time, 'monotonic', return_value=100), \
                patch.object(sli.subprocess, 'run', return_value=response([])) as api:
            sli.api_call(['gh'])
            self.assertEqual(5, api.call_args.kwargs['timeout'])
        with patch.object(sli, 'API_DEADLINE', 99), patch.object(sli.time, 'monotonic', return_value=100), \
                patch.object(sli.subprocess, 'run') as api:
            with self.assertRaisesRegex(ValueError, 'deadline'):
                sli.api_call(['gh'])
            api.assert_not_called()

    def test_raw_evidence_retains_failure_and_job_inputs(self):
        with tempfile.TemporaryDirectory() as directory:
            path = str(Path(directory) / 'evidence.json')
            document = {'status': 'refused', 'reason': 'later page failure', 'runs': [{**run(), '_jobs': [self.job()]}]}
            sli.save_evidence(path, document)
            self.assertEqual(document, json.loads(Path(path).read_text()))

    def job(self, identity=11):
        return {'id': identity, 'run_id': 1, 'head_sha': 'a'*40, 'name': 'CI Summary',
                'status': 'completed', 'conclusion': 'success',
                'started_at': '2026-01-01T00:08:00Z', 'completed_at': '2026-01-01T00:09:00Z',
                'steps': [{'name': 'Verdict', 'status': 'completed', 'conclusion': 'success'}]}

    def test_jobs_are_attempt_specific_and_revalidated(self):
        with patch.object(sli.subprocess, 'run', side_effect=[response([self.job()]), response(run())]) as api:
            self.assertEqual(1, len(sli.fetch_attempt_jobs('owner/repo', run())))
            self.assertIn('repos/owner/repo/actions/runs/1/attempts/1/jobs', api.call_args_list[0].args[0])

    def test_concurrent_rerun_refuses(self):
        with patch.object(sli.subprocess, 'run', side_effect=[response([self.job()]), response({**run(), 'run_attempt': 2})]):
            with self.assertRaisesRegex(ValueError, 'changed'):
                sli.fetch_attempt_jobs('owner/repo', run())

    def test_jobs_wrong_run_sha_attempt_and_duplicate_refuse(self):
        for rows in ([{**self.job(), 'run_id': 2}], [{**self.job(), 'head_sha': 'b'*40}],
                     [{**self.job(), 'run_attempt': 2}], [self.job(), self.job()]):
            with self.subTest(rows=rows), patch.object(sli.subprocess, 'run', return_value=response(rows)):
                with self.assertRaises(ValueError):
                    sli.fetch_attempt_jobs('owner/repo', run())

    def test_job_timing_uses_completion_not_workflow_update(self):
        measurement = sli.summarise_job([{**run(), '_jobs': [self.job()]}], 'CI Summary', 'Verdict')
        self.assertEqual(9, measurement['job_p95_min'])
        self.assertEqual(1, measurement['successful_jobs'])

    def test_missing_or_unexecuted_verdict_keeps_denominator(self):
        for jobs in ([], [self.job(), self.job()], [{**self.job(), 'steps': []}],
                     [{**self.job(), 'conclusion': 'cancelled'}],
                     [{**self.job(), 'steps': [{'name': 'Verdict', 'status': 'completed', 'conclusion': 'skipped'}]}]):
            with self.subTest(jobs=jobs):
                runs = [{**run(i), '_jobs': [self.job()]} for i in range(20)] + [{**run(21), '_jobs': jobs}]
                measurement = sli.summarise_job(runs, 'CI Summary', 'Verdict')
                self.assertEqual(21, measurement['completed'])
                objective = {'workflow': 'ci.yml', 'metric': 'job_p95_min', 'job': 'CI Summary', 'value': 20}
                self.assertEqual(sli.UNMEASURABLE, sli.score(objective, {'ci.yml': {'job_measurements': {'CI Summary': measurement}}})[0])

    def test_failed_verdict_is_latency_evidence_not_success(self):
        job = self.job(); job['conclusion'] = 'failure'; job['steps'][0]['conclusion'] = 'failure'
        measurement = sli.summarise_job([{**run(), '_jobs': [job]}], 'CI Summary', 'Verdict')
        self.assertEqual(9, measurement['job_p95_min'])
        self.assertEqual(0, measurement['successful_jobs'])
        self.assertEqual(1, measurement['failed_jobs'])

    def test_active_runs_cannot_disappear_from_latency_population(self):
        rows = [{**run(i), '_jobs': [self.job()]} for i in range(20)]
        rows += [{**run(21), 'status': 'queued'}, {**run(22), 'status': 'in_progress'}]
        measurement = sli.summarise_job(rows, 'CI Summary', 'Verdict')
        self.assertEqual(22, measurement['population_size'])
        self.assertEqual(2, measurement['pending_runs'])
        self.assertEqual(22, len(measurement['observations']))
        objective = {'workflow': 'ci.yml', 'metric': 'job_p95_min', 'job': 'CI Summary', 'value': 20}
        self.assertEqual(sli.UNMEASURABLE, sli.score(objective, {'ci.yml': {'job_measurements': {'CI Summary': measurement}}})[0])

    def test_pr_query_has_event_and_no_main_head_filter(self):
        with patch.object(sli.subprocess, 'run', return_value=response([run()])) as api:
            self.assertEqual(1, len(sli.fetch_runs('owner/repo', 'ci.yml', 30, 'pull_request')))
            args = api.call_args.args[0]
            self.assertIn('event=pull_request', args)
            self.assertFalse(any(a.startswith('branch=') for a in args))

    def test_multiple_pages_and_exact_bound(self):
        with patch.object(sli.subprocess, 'run', side_effect=[response([run(i) for i in range(100)]),
                                                            response([run(i) for i in range(100, 200)])]) as api:
            self.assertEqual(150, len(sli.fetch_runs('owner/repo', 'ci.yml', 150, 'pull_request')))
            self.assertIn('page=2', api.call_args.args[0])

    def test_clean_exhaustion_is_valid(self):
        with patch.object(sli.subprocess, 'run', side_effect=[response([run(i) for i in range(100)]), response([])]):
            self.assertEqual(100, len(sli.fetch_runs('owner/repo', 'ci.yml', 150, 'pull_request')))

    def test_later_page_failure_cannot_score_partial_window(self):
        # api_call retries a non-zero exit, so the page must stay failed for every attempt; one
        # failing response would exhaust the mock mid-retry and raise StopIteration, not the refusal.
        failed = [response([], 1)] * 3
        with patch.object(sli.time, 'sleep'),              patch.object(sli.subprocess, 'run', side_effect=[response([run(i) for i in range(100)])] + failed):
            with self.assertRaisesRegex(ValueError, 'gh exited'):
                sli.fetch_runs('owner/repo', 'ci.yml', 150, 'pull_request')

    def test_duplicate_page_refuses(self):
        with patch.object(sli.subprocess, 'run', side_effect=[response([run(i) for i in range(100)]), response([run(1)])]):
            with self.assertRaisesRegex(ValueError, 'duplicate'):
                sli.fetch_runs('owner/repo', 'ci.yml', 150, 'pull_request')

    def test_wrong_population_refuses(self):
        for key, value in [('event', 'push'), ('head_branch', 'main'),
                           ('repository', {'full_name': 'other/repo'}), ('path', '.github/workflows/other.yml')]:
            with self.subTest(key=key):
                bad = run(); bad[key] = value
                with patch.object(sli.subprocess, 'run', return_value=response([bad])):
                    with self.assertRaises(ValueError):
                        sli.fetch_runs('owner/repo', 'ci.yml', 30, 'pull_request', 'feature')

    def test_limit_validation(self):
        for limit in (0, -1, 1001):
            with self.assertRaises(ValueError):
                sli.fetch_runs('owner/repo', 'ci.yml', limit)

    def test_scoped_objective_never_uses_unscoped_summary(self):
        objective = {'workflow': 'ci.yml', 'event': 'pull_request', 'metric': 'success_rate', 'value': 95}
        self.assertEqual(sli.UNMEASURABLE, sli.score(objective, {'ci.yml': sli.summarise([run()])})[0])

    def test_small_sample_cannot_meet_p95(self):
        objective = {'workflow': 'ci.yml', 'metric': 'p95_min', 'comparison': 'at-most', 'value': 20}
        self.assertEqual(sli.UNMEASURABLE, sli.score(objective, {'ci.yml': sli.summarise([run()])})[0])
        self.assertEqual(sli.MET, sli.score(objective, {'ci.yml': sli.summarise([run(i) for i in range(20)])})[0])

    def test_incomplete_or_malformed_timing_cannot_score(self):
        objective = {'workflow': 'ci.yml', 'metric': 'p95_min', 'comparison': 'at-most', 'value': 20}
        for stamp in (None, 123, 'bad', '2026-01-01T00:10:00', '2025-01-01T00:10:00Z'):
            with self.subTest(stamp=stamp):
                rows = [run(i) for i in range(21)]
                rows[-1]['updated_at'] = stamp
                self.assertEqual(sli.UNMEASURABLE, sli.score(objective, {'ci.yml': sli.summarise(rows)})[0])

    def test_invalid_policy_refuses(self):
        good = {'id': 'x', 'description': 'Reliability', 'workflow': 'ci.yml',
                'metric': 'success_rate', 'comparison': 'at-least', 'value': 95}
        with tempfile.TemporaryDirectory() as directory, contextlib.redirect_stderr(io.StringIO()):
            path = Path(directory) / 'policy.json'
            for key, value in [('comparison', 'at-leest'), ('value', float('nan')),
                               ('value', True), ('metric', 'unknown'), ('workflow', None), ('event', [])]:
                with self.subTest(key=key, value=value):
                    path.write_text(json.dumps({'objectives': [{**good, key: value}]}))
                    self.assertIsNone(sli.load_objectives(str(path)))
            path.write_text(json.dumps({'objectives': [good, good]}))
            self.assertIsNone(sli.load_objectives(str(path)))

    def test_repository_and_policy_changes_start_separate_trends(self):
        objective = {'id': 'pr', 'description': 'PR completion', 'workflow': 'ci.yml', 'event': 'pull_request',
                     'metric': 'p95_min', 'comparison': 'at-most', 'value': 20}
        with tempfile.TemporaryDirectory() as directory, contextlib.redirect_stdout(io.StringIO()):
            path = str(Path(directory) / 'history.jsonl')
            for repo, threshold in [('owner/repo', 20), ('other/repo', 20), ('owner/repo', 10)]:
                rows = sli.render_objectives([{**objective, 'value': threshold}], {})
                sli.append_history(path, rows, repo)
            with contextlib.redirect_stdout(io.StringIO()) as output:
                sli.render_trend(path)
            self.assertEqual(3, output.getvalue().count('| `pr`'))

    def test_history_retains_population_evidence_and_separates_legacy(self):
        objective = {'id': 'pr', 'description': 'PR completion', 'workflow': 'ci.yml', 'event': 'pull_request',
                     'metric': 'p95_min', 'comparison': 'at-most', 'value': 20}
        with tempfile.TemporaryDirectory() as directory, contextlib.redirect_stdout(io.StringIO()) as output:
            path = str(Path(directory) / 'history.jsonl')
            Path(path).write_text(json.dumps({'objective': 'pr', 'verdict': 'MET'}) + '\n')
            rows = sli.render_objectives([objective], {sli.population('ci.yml', 'pull_request'): sli.summarise([run()])})
            sli.append_history(path, rows, 'owner/repo')
            saved = json.loads(Path(path).read_text().splitlines()[-1])
            self.assertEqual('pull_request', saved['population']['event'])
            self.assertEqual([{'id': 1, 'attempt': 1}], saved['evidence']['run_identities'])
            sli.render_trend(path)
            self.assertIn('legacy-main-only', output.getvalue())
            self.assertIn('v2', output.getvalue())


if __name__ == '__main__':
    unittest.main(verbosity=2)
