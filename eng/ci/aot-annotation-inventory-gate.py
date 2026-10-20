#!/usr/bin/env python3
"""Candidate strict configuration-scoped declaration inventory gate. Never rewrites its baseline.

This is lexical annotation governance, not compiled API equivalence or AOT certification.
All commands retain raw output and have wall-clock bounds. Timeouts stop the direct subprocess;
this wrapper does not claim descendant cleanup. Caller owns build/test coordination.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import stat
from pathlib import Path
import subprocess
import sys
import tempfile

class Refusal(Exception):
    pass

def is_reparse(path):
    return path.is_symlink() or bool(getattr(path.lstat(),'st_file_attributes',0) & getattr(stat,'FILE_ATTRIBUTE_REPARSE_POINT',0x400))

def compare_inventory(current, baseline):
    if not isinstance(current,list) or not current or any(not isinstance(i,str) or not i for i in current) or len(set(current))!=len(current):
        raise Refusal('Observed inventory is empty/malformed/duplicated')
    if not isinstance(baseline,dict) or baseline.get('schemaVersion')!=1 or not isinstance(baseline.get('identities'),list) or not baseline['identities']:
        raise Refusal('Baseline must be a nonempty schemaVersion1 identities array')
    recorded=baseline['identities']
    if any(not isinstance(i,str) or not i for i in recorded) or len(set(recorded))!=len(recorded):
        raise Refusal('Baseline identities malformed/duplicated')
    additions,removals=sorted(set(current)-set(recorded)),sorted(set(recorded)-set(current))
    return {'exitCode':1 if additions else 0,'additions':additions,'removals':removals}

def comparison_controls():
    results=[]
    for name,current,baseline,wanted in [
        ('equal',['a'],{'schemaVersion':1,'identities':['a']},0),
        ('addition',['a','b'],{'schemaVersion':1,'identities':['a']},1),
        ('missing-current',[],{'schemaVersion':1,'identities':['a']},2),
        ('malformed-baseline',['a'],{'schemaVersion':1,'identities':'a'},2),
        ('duplicate-baseline',['a'],{'schemaVersion':1,'identities':['a','a']},2),
        ('removal-allowed',['a'],{'schemaVersion':1,'identities':['a','b']},0)]:
        try:
            result=compare_inventory(current,baseline)
            actual=result['exitCode']
            if name=='removal-allowed' and result['removals']!=['b']:
                actual=1
        except Refusal:
            actual=2
        results.append({'name':name,'expected':wanted,'actual':actual,'passed':actual==wanted})
    return results

def shipping_projects(filter_path,repo):
    data=read_json(filter_path)['solution']
    solution=(filter_path.parent/str(data['path']).replace('\\','/')).resolve()
    projects=data['projects']
    if not isinstance(projects,list) or not projects or any(not isinstance(p,str) or not p for p in projects):
        raise Refusal('ShippingOnly project census is empty/malformed')
    paths=sorted({within(solution.parent/p.replace('\\','/'),repo).as_posix() for p in projects})
    if any(not (repo/p).is_file() for p in paths):
        raise Refusal('ShippingOnly resolves to missing project files')
    return paths

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def read_json(path):
    try:
        return json.loads(path.read_bytes())
    except (OSError, ValueError, UnicodeError) as error:
        raise Refusal(f'Invalid/unreadable JSON {path}: {error}') from error

def within(path, root):
    try:
        return path.resolve().relative_to(root.resolve())
    except ValueError as error:
        raise Refusal(f'Path escapes repository: {path}') from error

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', default=str(Path(__file__).resolve().parents[2]))
    parser.add_argument('--output')
    parser.add_argument('--project', default='tools/AotAnnotationInventory/AotAnnotationInventory.csproj')
    parser.add_argument('--prepare-script', default='eng/ci/prepare-aot-inventory.ps1')
    parser.add_argument('--merge-script', default='tools/AotAnnotationInventory/Merge-ConfiguredInventories.py')
    parser.add_argument('--baseline', default='eng/ci/aot-annotation-inventory.json')
    parser.add_argument('--shipping-filter', default='eng/ci/shards/ShippingOnly.slnf')
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--pwsh', default='pwsh')
    parser.add_argument('--configuration', default='Release')
    parser.add_argument('--write-candidate')
    parser.add_argument('--self-test', action='store_true')
    parser.add_argument('--self-test-script', default='eng/ci/aot-annotation-inventory-gate.test.py')
    parser.add_argument('--check-docs', action='store_true')
    parser.add_argument('--docs-helper', default='eng/ci/aot-inventory-docs.py')
    parser.add_argument('--docs', default='docs-site/docs/advanced/aot-compatibility.md')
    parser.add_argument('--suppression-baseline', default='eng/ci/aot-suppression-baseline.json')
    parser.add_argument('--regenerate', action='store_true')
    parser.add_argument('--fixtures', default='eng/ci/aot-inventory-fixtures')
    args = parser.parse_args()
    repo = Path(args.repo).resolve()
    output = Path(args.output).resolve() if args.output else Path(tempfile.mkdtemp(prefix='aot-inventory-'))
    if args.output:
        output.mkdir(parents=True, exist_ok=False)
    receipt = {'scope':'Explicit declaration-level lexical annotations in every governed source project/TFM; '
                        'SDK symbols and evaluated language version; excludes target-added/generated Compile items; '
                        'not compiled API equivalence or general AOT certification',
               'evidenceDirectory':str(output), 'commands':[], 'status':'running'}

    def command(label, argv, timeout):
        stdout, stderr = output / f'{label}.stdout.log', output / f'{label}.stderr.log'
        item = {'label':label, 'argv':argv, 'timeoutSeconds':timeout,
                'stdout':str(stdout), 'stderr':str(stderr)}
        receipt['commands'].append(item)
        try:
            with stdout.open('xb') as out, stderr.open('xb') as err:
                process = subprocess.run(argv, cwd=repo, stdout=out, stderr=err, timeout=timeout, check=False)
            item['exitCode'] = process.returncode
        except subprocess.TimeoutExpired as error:
            item['timedOut'] = True
            raise Refusal(f'{label} exceeded {timeout}s; inspect retained evidence and process ownership') from error
        except OSError as error:
            raise Refusal(f'{label} could not start: {error}') from error
        if process.returncode != 0:
            raise Refusal(f'{label} exited {process.returncode}; inspect {stderr}')
        return stdout

    project = repo / args.project
    prepare = repo / args.prepare_script
    merge = repo / args.merge_script
    baseline = repo / args.baseline
    shipping_filter = repo / args.shipping_filter
    selftest = repo / args.self_test_script
    docs_helper = repo / args.docs_helper
    docs_path = repo / args.docs
    suppression_path = repo / args.suppression_baseline
    candidate_output = Path(args.write_candidate).resolve() if args.write_candidate else None
    fixed = [Path(__file__).resolve(), docs_helper, docs_path, suppression_path,
             repo/'global.json', repo/'Directory.Build.props', repo/'Directory.Build.targets', repo/'Directory.Packages.props']
    if not args.check_docs:
        fixed.extend([project,prepare,merge,shipping_filter])
    if not candidate_output and not args.self_test:
        fixed.append(baseline)
    if args.self_test:
        fixed.append(selftest)

    def fingerprint():
        paths = set()
        for root in (repo/'src', repo/'tools', repo/'eng'):
            if not root.is_dir():
                # Artifact review can override the entire helper location before tools/ exists.
                # Production still requires the actual selected project/helper files via fixed inputs.
                if root == repo/'tools':
                    continue
                raise Refusal(f'Missing governed directory: {root}')
            for directory, dirs, files in os.walk(root, followlinks=False):
                for name in list(dirs):
                    child = Path(directory)/name
                    if name in ('bin','obj','node_modules','__pycache__'):
                        dirs.remove(name)
                    elif is_reparse(child):
                        raise Refusal(f'Linked governed directory: {child}')
                for name in files:
                    path = Path(directory)/name
                    if is_reparse(path):
                        raise Refusal(f'Linked governed file: {path}')
                    paths.add(path)
        # Configurable artifact helper paths must also be pinned even outside production tools/eng.
        paths.update(project.parent.glob('*.cs'))
        paths.update(project.parent.glob('*.csproj'))
        paths.update(fixed)
        try:
            return {str(p.resolve()):digest(p) for p in sorted(paths) if p.is_file()}
        except OSError as error:
            raise Refusal(f'Fingerprint could not read input: {error}') from error

    before = None
    exit_code = 2
    try:
        if args.regenerate:
            raise Refusal('--regenerate is disabled: use --write-candidate <fresh-path>, review the diff, and promote deliberately')
        if args.self_test and candidate_output:
            raise Refusal('--self-test and --write-candidate are separate operations')
        if candidate_output and (candidate_output == baseline.resolve() or candidate_output.exists()):
            raise Refusal('Candidate output must be fresh and must never equal the governed baseline')
        for path in fixed:
            if not path.is_file():
                raise Refusal(f'Required input missing: {path}')
        before = fingerprint()
        (output/'source-before.json').write_text(json.dumps(before,indent=2),encoding='utf-8')
        spec = importlib.util.spec_from_file_location('aot_inventory_docs',docs_helper)
        docs_module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(docs_module)
        def enforce_docs(identities):
            result = docs_module.check_docs(identities,docs_path,suppression_path)
            receipt['docsCheck'] = result
            if result['exitCode'] == 2:
                raise Refusal('Docs table evidence is incomplete/unreadable; inspect docsCheck diagnostics')
            return result['exitCode']
        def build_helper():
            command('helper-build',[args.dotnet,'build',str(project),'-c','Release','-p:RunAnalyzers=false'],180)
            built=project.parent/'bin/Release/net10.0/AotAnnotationInventory.dll'
            if not built.is_file():
                raise Refusal('Helper build produced no expected scanner DLL')
            receipt['scannerDllSha256']=digest(built)
            return built
        if args.self_test:
            comparison_results=comparison_controls()
            receipt['comparisonSelfTests']=comparison_results
            if not all(item['passed'] for item in comparison_results):
                raise Refusal('Default comparison function planted controls failed')
            shipping=shipping_projects(shipping_filter,repo)
            receipt['shippingPathSelfTest']={'count':len(shipping),'allFilesExist':True}
            # Fixed census control requested for this release. A deliberate project change updates this control.
            if len(shipping)!=194:
                raise Refusal('Release shipping-path control expected194 valid projects')
            synthetic=output/'synthetic-filter'
            synthetic.mkdir()
            (synthetic/'project.csproj').write_text('<Project />',encoding='utf-8')
            (synthetic/'filter').mkdir()
            synthetic_filter=synthetic/'filter'/'test.slnf'
            synthetic_filter.write_text(json.dumps({'solution':{'path':'..\\solution.sln','projects':['.\\project.csproj']}}),encoding='utf-8')
            if shipping_projects(synthetic_filter,synthetic)!=['project.csproj']:
                raise Refusal('Synthetic POSIX/backslash path normalization control failed')
            docs_controls = docs_module.self_test()
            receipt['docsSelfTests'] = docs_controls
            if docs_controls['exitCode'] != 0:
                raise Refusal('Documentation planted controls failed')
            dll=build_helper()
            command('self-test', [sys.executable,str(selftest),'--output',str(output/'self-test'),
                '--repo',str(repo),'--scanner-dll',str(dll),'--fixtures',str(repo/args.fixtures),'--dotnet',args.dotnet], 600)
            fixture_report = read_json(output/'self-test/positive-exact/stdout.json')
            fixture_ids = [entry['identity'] for entry in fixture_report['entries']]
            planted_interface = 'C.cs::Fixture.IContract.Public()::RequiresDynamicCode'
            if planted_interface not in fixture_ids:
                raise Refusal('Actual scanner omitted the planted implicit-public interface member')
            interface_comparison = compare_inventory(fixture_ids, {'schemaVersion':1,
                'identities':[identity for identity in fixture_ids if identity != planted_interface]})
            receipt['interfaceAdditionSelfTest'] = interface_comparison
            if interface_comparison['exitCode'] != 1 or interface_comparison['additions'] != [planted_interface]:
                raise Refusal('Actual scanner interface addition did not fail the production comparator')
            receipt['status'] = 'self-tests-passed-only'
            exit_code = 0
        elif args.check_docs:
            if candidate_output:
                raise Refusal('--check-docs and --write-candidate are separate operations')
            recorded = read_json(baseline)
            if recorded.get('schemaVersion') != 1 or not isinstance(recorded.get('identities'),list):
                raise Refusal('Documentation baseline has invalid schema')
            exit_code = enforce_docs(recorded['identities'])
            receipt['status'] = 'docs-table-failed' if exit_code else 'docs-table-agrees-only-no-source-scan'
        else:
            shipping=shipping_projects(shipping_filter,repo)
            shipping_list = output/'shipping-projects.txt'
            shipping_list.write_text('\n'.join(shipping)+'\n',encoding='utf-8')
            dll=build_helper()
            manifests = output/'manifests'
            command('prepare',[args.pwsh,'-NoProfile','-File',str(prepare),'-RepositoryRoot',str(repo),
                '-ShippingProjectList',str(shipping_list),'-OutputDirectory',str(manifests),
                '-Configuration',args.configuration],1200)
            index_path = manifests/'scope-index.json'
            index = read_json(index_path)
            if not index.get('sourceCoverageComplete') or index.get('uncoveredSourceFiles') or not index.get('scopes'):
                raise Refusal('All-src scope incomplete or residual source files require review')
            index_hash = digest(index_path)
            report_paths = []
            seen = set()
            for number, scope in enumerate(index['scopes']):
                manifest = Path(scope['manifest']).resolve()
                if manifest in seen or not manifest.is_file() or digest(manifest).upper() != scope['sha256'].upper():
                    raise Refusal('Changed, duplicated or missing evaluated manifest')
                seen.add(manifest)
                report_path = command(f'scan-{number:04}',[args.dotnet,str(dll),'--source',str(repo/'src'),
                    '--base',str(repo),'--configuration-manifest',str(manifest)],30)
                report = read_json(report_path)
                if not report.get('complete') or report.get('diagnostics'):
                    raise Refusal('Scanner refused a configuration; no inventory comparison permitted')
                report_paths.append(str(report_path))
            report_list = output/'report-list.json'
            report_list.write_text(json.dumps(report_paths,indent=2),encoding='utf-8')
            merged = output/'merged.json'
            command('merge',[sys.executable,str(merge),'--scope-index',str(index_path),
                '--report-list',str(report_list),'--output',str(merged)],120)
            if digest(index_path) != index_hash:
                raise Refusal('Scope index changed between enumeration and merge')
            population = read_json(merged)
            if not population.get('complete') or not population.get('entries'):
                raise Refusal('Merged inventory is empty/incomplete; not a measured pass')
            identities = [entry['identity'] for entry in population['entries']]
            if any(not isinstance(i,str) or not i for i in identities) or len(set(identities)) != len(identities):
                raise Refusal('Merged identities malformed/duplicated')
            current = set(identities)
            docs_exit = enforce_docs(sorted(current))
            receipt['currentCount'] = len(current)
            receipt['scopeIndexSha256'] = digest(index_path)
            receipt['mergedSha256'] = digest(merged)
            if candidate_output:
                receipt['status'] = 'candidate-only-not-baseline-pass'
                # Defer writing until the final source fence succeeds.
                receipt['candidateOutput'] = str(candidate_output)
                exit_code = docs_exit
                if docs_exit:
                    receipt['status'] = 'docs-table-failed-candidate-not-written'
            else:
                recorded = read_json(baseline)
                comparison=compare_inventory(sorted(current),recorded)
                additions,removals=comparison['additions'],comparison['removals']
                receipt.update(additions=additions, removals=removals, baselineSha256=digest(baseline))
                exit_code = max(1 if additions else 0,docs_exit)
                receipt['status'] = 'new-annotations-failed' if additions else ('docs-table-failed' if docs_exit else 'matched-no-new-annotations-and-docs-agree')
    except (Refusal,OSError,ValueError,KeyError,TypeError) as error:
        receipt.update(status='REFUSE',error=str(error))
        exit_code = 2
    finally:
        if 'scannerDllSha256' in receipt:
            try:
                after_dll = digest(project.parent/'bin/Release/net10.0/AotAnnotationInventory.dll')
                receipt['scannerDllSha256After'] = after_dll
                if after_dll != receipt['scannerDllSha256']:
                    receipt.update(status='REFUSE',error='Scanner binary changed during verification')
                    exit_code = 2
            except OSError as error:
                receipt.update(status='REFUSE',error=f'Scanner binary final pin failed: {error}')
                exit_code = 2
        if before is not None:
            try:
                after = fingerprint()
                (output/'source-after.json').write_text(json.dumps(after,indent=2),encoding='utf-8')
                receipt['sourceUnchanged'] = before == after
                if before != after:
                    receipt.update(status='REFUSE',error='Governed source/config/tool population changed during verification')
                    exit_code = 2
            except (Refusal,OSError) as error:
                receipt.update(status='REFUSE',error=f'Final fingerprint failed: {error}')
                exit_code = 2
        if exit_code == 0 and candidate_output and receipt.get('sourceUnchanged'):
            try:
                with candidate_output.open('x',encoding='utf-8') as stream:
                    json.dump({'schemaVersion':1,'scope':receipt['scope'],'status':'review-candidate-only',
                               'identities':sorted(current)},stream,indent=2)
                    stream.write('\n')
            except OSError as error:
                receipt.update(status='REFUSE',error=f'Candidate could not be written: {error}')
                exit_code = 2
        (output/'gate-receipt.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
    print(f"{receipt['status']}: {output}")
    return exit_code

if __name__ == '__main__':
    raise SystemExit(main())
