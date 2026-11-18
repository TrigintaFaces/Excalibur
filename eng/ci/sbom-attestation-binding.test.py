#!/usr/bin/env python3
"""Execute actual current admission security steps with offline test-owned gh and asset hooks.

Synthetic gh output proves orchestration/policy only, never Sigstore cryptography.
No workflow dispatch, publication, credentials or network access occurs.
"""
import copy
import hashlib
import json
import os
from pathlib import Path
import shlex
import shutil
import signal
import subprocess
import sys
sys.dont_write_bytecode = True
import tempfile
import unittest
import yaml

REPO = Path(__file__).resolve().parents[2]
if '--repo' in sys.argv:
    index = sys.argv.index('--repo')
    REPO = Path(sys.argv[index + 1]).resolve()
    del sys.argv[index:index + 2]
SLSA = 'https://slsa.dev/provenance/v1'
CYCLONE = 'https://cyclonedx.org/bom'
SHA = 'a' * 40
REPOSITORY = 'fixture-owner/fixture-repository'
RUN = '12345'
ATTEMPT = '2'


class ActualSbomAdmissionControls(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='sbom-binding-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.packages = self.root / 'packages'
        self.packages.mkdir()
        self.sbom = self.root / 'sbom'
        self.sbom.mkdir()
        self.package = self.packages / 'Fixture.1.0.0.nupkg'
        self.package.write_bytes(b'fixed package bytes; this slice does not claim nuspec validation')
        self.bom = self.sbom / 'bom.json'
        self.original_bom = {'bomFormat': 'CycloneDX', 'specVersion': '1.6', 'serialNumber': 'urn:uuid:fixture',
                             'version': 1, 'components': [{'name': 'Original', 'version': '1.0.0'}]}
        self.bom.write_text(json.dumps(self.original_bom), encoding='utf-8')
        self.original_digest = self.digest(self.bom)
        self.write_manifest()
        shutil.copytree(self.packages, self.root / 'all-packages')
        workflow = yaml.safe_load((REPO / '.github/workflows/release.yml').read_text(encoding='utf-8'))
        self.jobs = workflow['jobs']
        self.bin = self.root / 'bin'
        self.bin.mkdir()
        self.gh_calls = self.root / 'gh-calls.jsonl'
        self.make_fake_gh()
        helper = REPO / 'eng/ci/verify-sbom-binding.py'
        if helper.exists():
            destination = self.root / 'eng/ci'
            destination.mkdir(parents=True)
            shutil.copyfile(helper, destination / helper.name)
        self.env = os.environ.copy()
        self.env.update(FIXTURE_EXPECTED_GH=str(self.bin / 'gh'), PATH=str(self.bin) + os.pathsep + self.env['PATH'], GH_TOKEN='offline-fixture-no-token',
                        GITHUB_TOKEN='offline-fixture-no-token', GITHUB_REPOSITORY=REPOSITORY,
                        GITHUB_SERVER_URL='https://github.com', PROMOTION_SHA=SHA, EXPECTED_SOURCE_SHA=SHA,
                        RESOLVED_RUN_ID=RUN, RESOLVED_RUN_ATTEMPT=ATTEMPT, EXPECTED_RUN_ID=RUN,
                        EXPECTED_RUN_ATTEMPT=ATTEMPT)
        self.logs = []
        self.preflight_count = 0

    @staticmethod
    def digest(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()

    def write_manifest(self, include_bom=False):
        value = f'{self.digest(self.package)}  {self.package.name}\n'
        if include_bom:
            value += f'{self.digest(self.bom)}  ../sbom/bom.json\n'
        (self.packages / 'SHA256SUMS.txt').write_bytes(value.encode('utf-8'))

    def make_fake_gh(self):
        subjects = [{'name': self.package.name, 'digest': {'sha256': self.digest(self.package)}}]
        certificate = {'issuer': 'https://token.actions.githubusercontent.com',
                       'sourceRepositoryURI': 'https://github.com/' + REPOSITORY,
                       'sourceRepositoryDigest': SHA,
                       'subjectAlternativeName': f'https://github.com/{REPOSITORY}/.github/workflows/official-build.yml@refs/heads/main',
                       'buildSignerURI': f'https://github.com/{REPOSITORY}/.github/workflows/official-build.yml@refs/heads/main',
                       'buildConfigURI': f'https://github.com/{REPOSITORY}/.github/workflows/official-build.yml@refs/heads/main',
                       'runInvocationURI': f'https://github.com/{REPOSITORY}/actions/runs/{RUN}/attempts/{ATTEMPT}'}
        def result(predicate_type, actual_subjects, predicate):
            return [{'verificationResult': {'statement': {'_type': 'https://in-toto.io/Statement/v1',
                     'subject': actual_subjects, 'predicateType': predicate_type, 'predicate': predicate},
                     'signature': {'certificate': certificate}}, 'attestation': {'fixture': 'NOT CRYPTOGRAPHIC EVIDENCE'}}]
        raw = copy.deepcopy(subjects) + [{'name': 'bom.json', 'digest': {'sha256': self.original_digest}}]
        receipts = self.root / 'fixed-trusted-fixture.json'
        receipts.write_text(json.dumps({SLSA: result(SLSA, raw, {'buildDefinition': {'fixture': 'synthetic'}}),
                                        CYCLONE: result(CYCLONE, subjects, self.original_bom)}), encoding='utf-8')
        python = self.bin / 'fake-gh.py'
        python.write_bytes(("import json,sys\nfrom pathlib import Path\na=sys.argv[1:]\n"
                          "if a==['--version']: print('gh version 2.97.0 (offline fixture)');sys.exit(0)\n"
                          "if a[:2]!=['attestation','verify']: sys.exit(9)\n"
                          f"with Path({str(self.gh_calls)!r}).open('a',encoding='utf-8') as f: f.write(json.dumps(a)+'\\n')\n"
                          f"p=a[a.index('--predicate-type')+1] if '--predicate-type' in a else {SLSA!r}\n"
                          f"print(json.dumps(json.loads(Path({str(receipts)!r}).read_text(encoding='utf-8'))[p]))\n").encode('utf-8'))
        executable = self.bin / 'gh'
        executable.write_bytes(('#!/usr/bin/env bash\nexec ' + shlex.quote(sys.executable.replace('\\', '/')) + ' ' +
                              shlex.quote(str(python).replace('\\', '/')) + ' "$@"\n').encode('utf-8'))
        executable.chmod(0o755)

    def run_step(self, step, ordinal):
        # The code under test is the actual checked-in workflow run block, not a copied imagined future check.
        body = step['run']
        self.assertNotIn('${{', body, 'wire trusted workflow context via env; do not evaluate arbitrary Actions expressions')
        script = self.root / f'step-{ordinal}.sh'
        expected_gh = str(self.bin / 'gh').replace('\\', '/')
        # Prove interception INSIDE the same Bash process/env before running ANY actual admission command.
        # A missing/wrong fake is setup failure, never a successful security refusal.
        preflight = (
            'set -euo pipefail\n'
            'actual_gh="$(command -v gh)"\n'
            'expected_gh=' + shlex.quote(expected_gh) + '\n'
            'if command -v cygpath >/dev/null 2>&1; then actual_gh="$(cygpath -am "$actual_gh")"; expected_gh="$(cygpath -am "$expected_gh")"; fi\n'
            'if [ "$actual_gh" != "$expected_gh" ]; then printf "SETUP-REFUSAL wrong-gh-path:%s\\n" "$actual_gh"; exit 97; fi\n'
            'actual_version="$(gh --version)"\n'
            'if [ "$actual_version" != "gh version 2.97.0 (offline fixture)" ]; then printf "SETUP-REFUSAL wrong-gh-version:%s\\n" "$actual_version"; exit 97; fi\n'
            'printf "SETUP-OFFLINE-GH-VERIFIED\\n"\n'
        )
        if (REPO / 'eng/ci/verify-sbom-binding.py').exists():
            # Production GH capture is native Python/Linux, not Bash command lookup. Prove THAT path
            # before invoking a future production helper; never reach the real gh with a fake token.
            probe = ("import os,pathlib,shutil,subprocess; "
                     "p=pathlib.Path(shutil.which('gh') or 'MISSING').resolve(); "
                     "assert p==pathlib.Path(os.environ['FIXTURE_EXPECTED_GH']).resolve(), 'SETUP wrong native gh'; "
                     "v=subprocess.run([str(p),'--version'],capture_output=True,check=True,timeout=5); "
                     "assert v.stdout.decode().strip()=='gh version 2.97.0 (offline fixture)', 'SETUP native gh version'; "
                     "print('SETUP-NATIVE-OFFLINE-GH-VERIFIED')")
            preflight += 'python3 -c ' + shlex.quote(probe) + '\n'
        script.write_bytes((preflight + body).encode('utf-8'))
        cwd = self.root / step.get('working-directory', '.')
        log = self.root / f'step-{ordinal}.log'
        bash = shutil.which('bash')
        self.assertIsNotNone(bash, 'actual Bash is required, never skip this executable admission slice')
        argv = [bash, str(script).replace('\\', '/')]
        if os.name == 'nt':
            sys.path.insert(0, str(REPO / 'eng/ci'))
            from lib.pre_release_windows_job import run_owned_windows
            result = run_owned_windows(argv, cwd, self.env, 15, log)
            self.assertEqual(result.get('cleanup'), 'owned Job Object has zero active processes')
            self.assertFalse(result.get('timed_out'))
            code = result['exit_code']
        else:
            with log.open('wb') as output:
                process = subprocess.Popen(argv, cwd=cwd, env=self.env, stdout=output, stderr=subprocess.STDOUT,
                                           stdin=subprocess.DEVNULL, start_new_session=True)
                try:
                    code = process.wait(timeout=15)
                finally:
                    try: os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError: pass
                    process.wait(timeout=5)
        output = log.read_text(encoding='utf-8', errors='replace')
        self.assertEqual(output.splitlines().count('SETUP-OFFLINE-GH-VERIFIED'), 1,
                         'SETUP: offline gh interception was not proved before actual admission; ' + output)
        self.assertNotIn('SETUP-REFUSAL', output, 'SETUP error is not an expected tamper refusal')
        if (REPO / 'eng/ci/verify-sbom-binding.py').exists():
            self.assertEqual(output.splitlines().count('SETUP-NATIVE-OFFLINE-GH-VERIFIED'), 1, 'SETUP native Python interception not proved; ' + output)
        self.preflight_count += 1
        self.logs.append((step['name'], output))
        return code

    def boundary(self, job, hook):
        names = {'Verify the package hashes against the official manifest',
                 'Verify build provenance for the promoted packages',
                 'Verify exact SBOM attestation binding',
                 'Reverify exact SBOM binding before release assets'}
        checks = [step for step in self.jobs[job]['steps'] if step.get('name') in names]
        if job == 'build-packages':
            self.assertGreaterEqual(len(checks), 2, 'current hash and provenance checks must genuinely execute')
        for index, step in enumerate(checks, start=len(self.logs)):
            if self.run_step(step, index) != 0:
                return False
        (self.root / hook).write_text('TEST-OWNED CONSUMER, NOT A REAL UPLOAD', encoding='utf-8')
        return True

    def test_exact_original_bom_reaches_current_admission_consumer(self):
        self.assertTrue(self.boundary('build-packages', 'admission-hook'), self.logs)
        calls = [json.loads(row) for row in self.gh_calls.read_text(encoding='utf-8').splitlines()]
        self.assertTrue(any(self.package.name in ' '.join(call) for call in calls))
        self.assertTrue((self.root / 'admission-hook').exists())
        self.assertGreaterEqual(self.preflight_count, 2)
        self.assertTrue(self.boundary('create-release', 'asset-hook'), self.logs)
        self.assertTrue((self.root / 'asset-hook').exists())

    def test_changed_bom_is_refused_before_admission_consumer(self):
        self.bom.write_text('{"bomFormat":"CycloneDX","specVersion":"1.6","serialNumber":"urn:uuid:substitute","components":[]}', encoding='utf-8')
        self.assertFalse(self.boundary('build-packages', 'admission-hook'), self.logs)
        self.assertFalse((self.root / 'admission-hook').exists())

    def test_changed_bom_and_rewritten_manifest_are_still_refused(self):
        self.bom.write_text('{"bomFormat":"CycloneDX","specVersion":"1.6","serialNumber":"urn:uuid:substitute","components":[]}', encoding='utf-8')
        self.write_manifest(include_bom=True)
        self.assertFalse(self.boundary('build-packages', 'admission-hook'), self.logs)
        self.assertFalse((self.root / 'admission-hook').exists())

    def test_raw_byte_only_bom_change_is_refused(self):
        self.bom.write_bytes(self.bom.read_bytes() + b'\n')
        self.assertFalse(self.boundary('build-packages', 'admission-hook'), self.logs)
        self.assertFalse((self.root / 'admission-hook').exists())

    def test_replacement_after_first_admission_is_refused_before_asset_hook(self):
        self.assertTrue(self.boundary('build-packages', 'admission-hook'), self.logs)
        self.bom.write_text('{"bomFormat":"CycloneDX","specVersion":"1.6","serialNumber":"urn:uuid:late-substitute","components":[]}', encoding='utf-8')
        self.assertFalse(self.boundary('create-release', 'asset-hook'), self.logs)
        self.assertFalse((self.root / 'asset-hook').exists())

    def test_unsigned_extra_artifacts_are_refused_at_both_admissions(self):
        for job, hook in (('build-packages', 'extra-admission-hook'), ('create-release', 'extra-asset-hook')):
            for kind in ('json', 'xml', 'nested', 'symlink'):
                with self.subTest(job=job, kind=kind):
                    extra = self.bom.parent / ('nested' if kind == 'nested' else 'unsigned.' + kind)
                    if kind == 'nested': extra.mkdir()
                    elif kind == 'symlink':
                        try: extra.symlink_to(self.bom)
                        except OSError as error: self.fail('SETUP symlink mutation unavailable: ' + str(error))
                    else: extra.write_bytes(b'unsigned attachment')
                    try:
                        self.assertFalse(self.boundary(job, hook), self.logs)
                        self.assertFalse((self.root / hook).exists())
                    finally:
                        if kind == 'nested': extra.rmdir()
                        else: extra.unlink()


if __name__ == '__main__':
    unittest.main()
