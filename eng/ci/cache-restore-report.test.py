#!/usr/bin/env python3
"""Population and reconciliation controls; never substitute artifact count for population."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

import yaml

spec = importlib.util.spec_from_file_location('cache_report', Path(__file__).with_name('cache-restore-report.py'))
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)
ROOT = Path(__file__).resolve().parents[2]


def workflow():
    return {'jobs': {'tests': {'name': 'Tests (${{ matrix.shard.name }})',
        'strategy': {'matrix': {'shard': [{'name': 'a', 'filter': 'a.slnf'}, {'name': 'b', 'filter': 'b.slnf'}]}},
        'steps': [{'uses': './.github/actions/build-and-test', 'with': {
            'solution-filter': '${{ matrix.shard.filter }}', 'telemetry-matrix': '${{ toJSON(matrix) }}'}}]}}}


def rows(data=None):
    return report.expected_invocations(yaml.safe_dump(data or workflow()))


def fixture():
    expected = rows()
    context = dict(repository='owner/repo', run_id=123, attempt=2,
                   source_sha='a'*40, workflow_sha='b'*40, event_sha='c'*40)
    jobs, observations = [], []
    for i, row in enumerate(expected):
        jobs.append(dict(id=i+1, run_id=123, run_attempt=2, head_sha='a'*40, name=row['job_name'],
                         status='completed', conclusion='success', steps=[{'name': 'restore'}]))
        observations.append(dict(context, **{k: row[k] for k in ('job_key', 'matrix_context', 'action', 'solution_filter')},
            schema=1, kind='cache-restore', invocation_id=f'{i+1:032x}',
            outcome='success', conclusion='success', raw_cache_hit='true', restore_state='exact', save_state='not-observed'))
    return expected, jobs, observations, context


class PopulationTests(unittest.TestCase):
    def test_current_workflow_population_has_explicit_context(self):
        expected = report.expected_invocations((ROOT / '.github/workflows/ci.yml').read_text(encoding='utf-8'))
        self.assertGreater(len(expected), 1)
        self.assertTrue(any(row['matrix_context'] for row in expected))
        self.assertTrue(any(not row['matrix_context'] for row in expected))

    def test_literal_matrix_expands_and_filters_render(self):
        self.assertEqual(['a.slnf', 'b.slnf'], [row['solution_filter'] for row in rows()])
        data = workflow()
        data['jobs']['tests']['strategy']['matrix']['os'] = ['linux', 'windows']
        data['jobs']['tests']['name'] += ' ${{ matrix.os }}'
        self.assertEqual(4, len(rows(data)))

    def test_unsupported_expansion_refuses_instead_of_omitting(self):
        for matrix in ('${{ fromJSON(needs.plan.outputs.matrix) }}', {}, {'shard': []},
                       {'include': [{'shard': 'a'}]}, {'shard': ['${{ vars.shard }}']}):
            data = workflow()
            data['jobs']['tests']['strategy']['matrix'] = matrix
            with self.subTest(matrix=matrix), self.assertRaises(ValueError):
                rows(data)

    def test_missing_context_conditional_and_repeated_calls_refuse(self):
        for change in ('context', 'condition', 'repeat', 'name', 'expression'):
            data = workflow()
            job = data['jobs']['tests']
            call = job['steps'][0]
            if change == 'context':
                del call['with']['telemetry-matrix']
            elif change == 'condition':
                call['if'] = 'success()'
            elif change == 'repeat':
                job['steps'].append(copy.deepcopy(call))
            elif change == 'name':
                job['name'] = 'Tests'
            else:
                job['name'] = '${{ github.job }}'
            with self.subTest(change=change), self.assertRaises(ValueError):
                rows(data)

    def test_duplicate_yaml_keys_and_duplicate_matrix_rows_refuse(self):
        with self.assertRaises(ValueError):
            report.expected_invocations('jobs: {}\njobs: {}')
        data = workflow()
        data['jobs']['tests']['strategy']['matrix']['shard'] *= 2
        with self.assertRaises(ValueError):
            rows(data)

    def test_equivalent_action_paths_cannot_silently_shrink_population(self):
        for alias in ('./.github/actions/build-and-test/', './.github/actions/./build-and-test',
                      './.github/actions/other/../build-and-test', '.github/actions/build-and-test',
                      './.github/actions/BUILD-AND-TEST', '.\\.github\\actions\\build-and-test'):
            for same_job in (True, False):
                data = workflow()
                if same_job:
                    call = copy.deepcopy(data['jobs']['tests']['steps'][0])
                    call['uses'] = alias
                    data['jobs']['tests']['steps'].append(call)
                else:
                    data['jobs']['other'] = copy.deepcopy(data['jobs']['tests'])
                    data['jobs']['other']['steps'][0]['uses'] = alias
                with self.subTest(alias=alias, same_job=same_job), self.assertRaises(ValueError):
                    rows(data)

    def test_exact_and_non_exact_rate(self):
        expected, jobs, observations, context = fixture()
        observations[1].update(raw_cache_hit='false', restore_state='non-exact')
        result = report.reconcile(expected, jobs, observations, context)
        self.assertEqual(50, result['exact_restore_rate'])
        self.assertEqual(2, result['resolved_denominator'])

    def test_missing_observation_cannot_improve_rate(self):
        expected, jobs, observations, context = fixture()
        result = report.reconcile(expected, jobs, observations[:1], context)
        self.assertIsNone(result['exact_restore_rate'])
        self.assertEqual(1, result['missing_observations'])
        self.assertEqual(2, result['resolved_denominator'])

    def test_empty_and_soft_failed_restore_stay_unknown(self):
        for mutation in ({'raw_cache_hit': '', 'restore_state': 'unknown'},
                         {'outcome': 'failure', 'restore_state': 'unknown'}):
            expected, jobs, observations, context = fixture()
            observations[1].update(mutation)
            result = report.reconcile(expected, jobs, observations, context)
            self.assertIsNone(result['exact_restore_rate'])
            self.assertEqual(1, result['unknown_restores'])

    def test_pending_and_missing_job_do_not_claim_denominator_or_bounds(self):
        for missing in (True, False):
            expected, jobs, observations, context = fixture()
            if missing:
                jobs = jobs[:1]
            else:
                jobs[1]['status'] = 'in_progress'
            result = report.reconcile(expected, jobs, observations[:1], context)
            self.assertIsNone(result['resolved_denominator'])
            self.assertIsNone(result['exact_restore_rate'])
            self.assertNotIn('lower_bound', result)

    def test_skipped_job_exclusion_needs_empty_steps_and_no_observation(self):
        expected, jobs, observations, context = fixture()
        jobs[1].update(conclusion='skipped', steps=[])
        result = report.reconcile(expected, jobs, observations[:1], context)
        self.assertEqual(100, result['exact_restore_rate'])
        self.assertEqual(1, result['excluded_skipped_jobs'])
        with self.assertRaises(ValueError):
            report.reconcile(expected, jobs, observations, context)
        jobs[1]['steps'] = [{'name': 'restore', 'conclusion': 'cancelled'}]
        with self.assertRaises(ValueError):
            report.reconcile(expected, jobs, observations[:1], context)

    def test_all_skipped_is_not_zero_or_perfect(self):
        expected, jobs, _, context = fixture()
        for job in jobs:
            job.update(conclusion='skipped', steps=[])
        result = report.reconcile(expected, jobs, [], context)
        self.assertIsNone(result['exact_restore_rate'])
        self.assertEqual(0, result['resolved_denominator'])

    def test_skipped_timing_must_not_contradict_no_execution(self):
        instant = '2026-10-03T00:00:00Z'
        for start, end in [(instant, '2026-10-03T00:00:01Z'), (instant, None),
                           (None, instant), ('bad', 'bad'), ('', ''),
                           ('2026-10-03T00:00:00', '2026-10-03T00:00:00')]:
            expected, jobs, observations, context = fixture()
            jobs[1].update(conclusion='skipped', steps=[], started_at=start, completed_at=end)
            with self.subTest(start=start, end=end), self.assertRaises(ValueError):
                report.reconcile(expected, jobs, observations[:1], context)
        for start, end in [(None, None), (instant, instant)]:
            expected, jobs, observations, context = fixture()
            jobs[1].update(conclusion='skipped', steps=[], started_at=start, completed_at=end)
            self.assertEqual(1, report.reconcile(expected, jobs, observations[:1], context)['excluded_skipped_jobs'])

    def test_wrong_provenance_and_contradictory_results_refuse(self):
        for key, value in [('repository', 'other/repo'), ('run_id', 124), ('attempt', 1),
                           ('source_sha', 'd'*40), ('workflow_sha', 'd'*40), ('event_sha', 'd'*40),
                           ('matrix_context', {}), ('solution_filter', 'wrong.slnf'),
                           ('restore_state', 'non-exact'), ('save_state', 'saved')]:
            expected, jobs, observations, context = fixture()
            observations[0][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                report.reconcile(expected, jobs, observations, context)

    def test_duplicate_artifact_identity_row_or_api_job_refuses(self):
        for kind in ('invocation', 'row', 'job-id', 'job-name'):
            expected, jobs, observations, context = fixture()
            if kind == 'invocation':
                observations[1]['invocation_id'] = observations[0]['invocation_id']
            elif kind == 'row':
                extra = dict(observations[0], invocation_id='f'*32)
                observations.append(extra)
            else:
                extra = dict(jobs[0])
                if kind == 'job-name':
                    extra['id'] = 77
                jobs.append(extra)
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                report.reconcile(expected, jobs, observations, context)

    def test_malformed_context_or_completed_job_cannot_score(self):
        for key in ('source_sha', 'workflow_sha', 'event_sha', 'repository', 'run_id', 'attempt'):
            expected, jobs, observations, context = fixture()
            context[key] = None
            with self.subTest(key=key), self.assertRaises(ValueError):
                report.reconcile(expected, jobs, observations, context)
        for key, value in [('conclusion', None), ('conclusion', 'unknown'), ('id', 0), ('id', True), ('run_attempt', True)]:
            expected, jobs, observations, context = fixture()
            jobs[0][key] = value
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                report.reconcile(expected, jobs, observations, context)


if __name__ == '__main__':
    unittest.main()
