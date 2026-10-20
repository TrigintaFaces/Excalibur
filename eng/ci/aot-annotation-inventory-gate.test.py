"""Bounded artifact-only controls for the built syntax scanner. Never builds or edits a baseline."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--dotnet', default='dotnet')
parser.add_argument('--repo',default=str(Path(__file__).resolve().parents[2]))
parser.add_argument('--scanner-dll')
parser.add_argument('--fixtures')
args = parser.parse_args()
repo=Path(args.repo).resolve()
dll=Path(args.scanner_dll).resolve() if args.scanner_dll else repo/'tools/AotAnnotationInventory/bin/Release/net10.0/AotAnnotationInventory.dll'
candidate=dll.parents[3]
fixtures=Path(args.fixtures).resolve() if args.fixtures else repo/'eng/ci/aot-inventory-fixtures'
output = Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=False)
results = []
pins = {}
for path in [candidate / 'Program.cs', candidate / 'AotAnnotationInventory.csproj', dll, Path(__file__)]:
    pins[str(path)] = hashlib.sha256(path.read_bytes()).hexdigest()

def fixture(name):
    path = fixtures / name
    raw = path.read_bytes()
    pins[str(path)] = hashlib.sha256(raw).hexdigest()
    return raw.decode('utf-8-sig')

def execute(name, sources, expected_exit, checker=None, symbols=None, manifest=True, setup=None):
    directory = output / name
    directory.mkdir()
    source = directory / 'src'
    source.mkdir()
    for relative, text in sources.items():
        path = source / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')
    configuration = directory / 'configuration.json'
    configuration.write_text(json.dumps({'project':'fixture.csproj', 'configuration':'Release',
        'targetFramework':'net10.0', 'langVersion':'preview', 'populationMode':'fixtureDirectory', 'symbols':symbols or []}), encoding='utf-8')
    command = [args.dotnet, str(dll), '--source', str(source), '--base', str(source)]
    if manifest:
        command += ['--configuration-manifest', str(configuration)]
    if setup:
        setup(source, configuration, command)
    source_pins = {str(p.relative_to(directory)):hashlib.sha256(p.read_bytes()).hexdigest()
                   for p in source.rglob('*') if p.is_file() and not p.is_symlink()}
    receipt = {'name':name, 'command':command, 'expectedExit':expected_exit,
               'sourceSha256':source_pins, 'manifestSha256':hashlib.sha256(configuration.read_bytes()).hexdigest()}
    try:
        process = subprocess.run(command, capture_output=True, timeout=30, check=False)
        (directory / 'stdout.json').write_bytes(process.stdout)
        (directory / 'stderr.log').write_bytes(process.stderr)
        receipt['actualExit'] = process.returncode
        report = json.loads(process.stdout.decode('utf-8-sig'))
        assert process.returncode == expected_exit, f'exit {process.returncode} != {expected_exit}'
        assert report['complete'] is (expected_exit == 0), 'incorrect completeness claim'
        if expected_exit == 0:
            assert report['diagnostics'] == [], report['diagnostics']
            assert report['configurationManifestSha256'].lower() == receipt['manifestSha256']
        else:
            assert report['diagnostics'], 'refusal omitted diagnostics'
        if checker:
            checker(report)
        receipt['status'] = 'passed'
    except subprocess.TimeoutExpired as error:
        (directory / 'stdout.json').write_bytes(error.stdout or b'')
        (directory / 'stderr.log').write_bytes(error.stderr or b'')
        receipt.update(status='failed', error='subprocess exceeded 30 seconds')
    except Exception as error:
        receipt.update(status='failed', error=f'{type(error).__name__}: {error}')
    (directory / 'receipt.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    results.append(receipt)
    return receipt

def exact(expected, texts):
    def check(report):
        actual = {entry['identity']:entry for entry in report['entries']}
        assert len(report['entries']) == len(expected), 'entry count differs or duplicate identities emitted'
        assert set(actual) == set(expected), {'missing':sorted(set(expected)-set(actual)), 'extra':sorted(set(actual)-set(expected))}
        for identity, markers in expected.items():
            wanted = []
            for path, marker in markers:
                # Scanner contract is attribute NAME start (1-based), rather than attribute-list bracket.
                matches = [(i+1, len(line)-len(line[line.index('[')+1:].lstrip())+1)
                           for i,line in enumerate(texts[path].splitlines()) if marker in line]
                assert matches, f'bad control marker {marker}'
                wanted.extend((path,line,column) for line,column in matches)
            got = [(s['path'],s['line'],s['column']) for s in actual[identity]['sources']]
            assert sorted(got) == sorted(set(wanted)), (identity, got, wanted)
    return check

positive = fixture('positive.cs.txt')
prefix = 'C.cs::Fixture.'
expected = {
    prefix+'Outer`1.Nested`1::RequiresDynamicCode':[('C.cs','("type")')],
    prefix+'Outer`1.Nested`1.Read`1(in V,string)::RequiresDynamicCode':[('C.cs','("wrapped")')],
    prefix+'Outer`1.Nested`1.Read(int)::RequiresUnreferencedCode':[('C.cs','("overload")')],
    prefix+'Outer`1.Nested`1.#ctor(int)::RequiresDynamicCode':[('C.cs','("constructor")')],
    prefix+'Outer`1.Nested`1.Callback`1(ref W)::RequiresDynamicCode':[('C.cs','("delegate")')],
    prefix+'Outer`1.Partial()::RequiresDynamicCode':[('C.cs','("partial duplicate")')],
    prefix+'IContract.Public()::RequiresDynamicCode':[('C.cs','("implicit public")')],
    prefix+'IContract.Default()::RequiresDynamicCode':[('C.cs','("default public")')]
}
execute('positive-exact', {'C.cs':positive}, 0, exact(expected, {'C.cs':positive}))
generic_parameter = '''using System.Diagnostics.CodeAnalysis;
namespace Fixture;
public static class GenericRegistration
{
    [RequiresDynamicCode("attributed generic parameter")]
    public static void Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() { }
}
'''
execute('attributed-generic-parameter', {'Generic.cs':generic_parameter}, 0, exact({
    'Generic.cs::Fixture.GenericRegistration.Register`1()::RequiresDynamicCode':
        [('Generic.cs','("attributed generic parameter")')]
}, {'Generic.cs':generic_parameter}))
conditional = dict(expected)
conditional[prefix+'Conditional.Selected()::RequiresDynamicCode'] = [('C.cs','("only the explicit conditional manifest includes this")')]
execute('conditional-exact-diff', {'C.cs':positive}, 0, exact(conditional, {'C.cs':positive}), symbols=['ANNOTATED_ARM'])

alias_sources = {'Global.cs':fixture('global-alias.cs.txt'), 'Use.cs':fixture('global-alias-use.cs.txt')}
execute('cross-file-global-alias', alias_sources, 0, exact({
    'Use.cs::AliasFixture.Consumer.Call()::RequiresUnreferencedCode':[('Use.cs','("global alias declared in a separate source file")')]
}, alias_sources))
aliases = '''using Direct = global::System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute;
using CA = global::System.Diagnostics.CodeAnalysis;
namespace Aliases;
public class Consumer {
 [Direct("direct")]
 public void Direct() { }
 [CA.RequiresUnreferencedCodeAttribute("namespace")]
 public void Namespace() { }
 [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("global-qualified")]
 public void Qualified() { }
}
'''
execute('aliases-exact', {'C.cs':aliases}, 0, exact({
    'C.cs::Aliases.Consumer.Direct()::RequiresDynamicCode':[('C.cs','("direct")')],
    'C.cs::Aliases.Consumer.Namespace()::RequiresUnreferencedCode':[('C.cs','("namespace")')],
    'C.cs::Aliases.Consumer.Qualified()::RequiresDynamicCode':[('C.cs','("global-qualified")')]
}, {'C.cs':aliases}))
dam = '''using System;
using System.Diagnostics.CodeAnalysis;
namespace Dam;
public interface IContract {
 [RequiresDynamicCode(
    "wrapped-interface")]
 void Resolve([DynamicallyAccessedMembers(
    DynamicallyAccessedMemberTypes.PublicConstructors)] Type type);
}
'''
execute('dam-multiline-interface', {'C.cs':dam}, 0, exact({
    'C.cs::Dam.IContract.Resolve(Type)::RequiresDynamicCode':[('C.cs','[RequiresDynamicCode(')]
}, {'C.cs':dam}))
def positioned_parse_refusal(report):
    assert any(d['Code'].startswith('CS') and d['Line'] >= 1 and d['Column'] >= 1
               and d['Path'] == 'Broken.cs' for d in report['diagnostics']), 'missing positioned Roslyn parse error'
execute('parse-refusal', {'Broken.cs':fixture('malformed-refuse.cs.txt')}, 2, positioned_parse_refusal)
execute('shadow-refusal', {'C.cs':fixture('shadow-refuse.cs.txt')}, 2)
conflict = '''using Warning = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;
public class Consumer { [Warning("conflict")] public void Call() { } }
'''
execute('alias-conflict-refusal', {
    'Global.cs':'global using Warning = System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute;',
    'C.cs':conflict}, 2)
execute('unsupported-alias-refusal', {
    'Global.cs':'global using Pair = (int, int);',
    'C.cs':'public class C { [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("x")] public void M() { } }'}, 2)
execute('missing-manifest', {'C.cs':positive}, 2, manifest=False)
def missing_language(source, configuration, command):
    manifest = json.loads(configuration.read_text(encoding='utf-8'))
    del manifest['langVersion']
    configuration.write_text(json.dumps(manifest), encoding='utf-8')
execute('missing-language-refusal', {'C.cs':positive}, 2, setup=missing_language)
def invalid_language(source, configuration, command):
    manifest = json.loads(configuration.read_text(encoding='utf-8'))
    manifest['langVersion'] = 'not-a-csharp-version'
    configuration.write_text(json.dumps(manifest), encoding='utf-8')
execute('invalid-language-refusal', {'C.cs':positive}, 2, setup=invalid_language)
execute('empty-source-refusal', {}, 2)
def evaluated_zero(source, configuration, command):
    manifest = json.loads(configuration.read_text(encoding='utf-8'))
    manifest.update(populationMode='evaluatedCompileItems', sourceFiles=[],
        sourcePopulationScope='Synthetic control of explicitly evaluated zero Compile items',
        zeroDeclarationDisposition='evaluated-source-free')
    configuration.write_text(json.dumps(manifest), encoding='utf-8')
def exact_zero(report):
    assert report['entries'] == []
    assert report['zeroDeclarationDisposition'] == 'evaluated-source-free'
execute('explicit-evaluated-source-free', {}, 0, checker=exact_zero, setup=evaluated_zero)
def evaluated_zero_missing_disposition(source, configuration, command):
    evaluated_zero(source, configuration, command)
    manifest = json.loads(configuration.read_text(encoding='utf-8'))
    del manifest['zeroDeclarationDisposition']
    configuration.write_text(json.dumps(manifest), encoding='utf-8')
execute('evaluated-empty-without-disposition-refusal', {}, 2, setup=evaluated_zero_missing_disposition)
def false_zero(source, configuration, command):
    evaluated_zero(source, configuration, command)
    manifest = json.loads(configuration.read_text(encoding='utf-8'))
    manifest['sourceFiles'] = ['C.cs']
    configuration.write_text(json.dumps(manifest), encoding='utf-8')
execute('false-zero-with-source-refusal', {'C.cs':positive}, 2, setup=false_zero)
def missing_root(source, configuration, command):
    source.rmdir()
execute('missing-root-refusal', {}, 2, setup=missing_root)

# An OS that cannot create symlinks must report unavailable, never a measured pass.
link_directory = output / 'symlink-source-refusal'
link_directory.mkdir()
try:
    target = link_directory / 'real'
    target.mkdir()
    (target / 'C.cs').write_text(positive, encoding='utf-8')
    link = link_directory / 'link'
    link.symlink_to(target, target_is_directory=True)
except (OSError, NotImplementedError) as error:
    results.append({'name':'symlink-source-refusal','status':'unavailable','reason':str(error)})
else:
    def replace_root(source, configuration, command):
        command[command.index('--source')+1] = str(link)
        command[command.index('--base')+1] = str(link)
    execute('symlink-root-actual', {}, 2, setup=replace_root)

for name, old_hash in pins.items():
    assert hashlib.sha256(Path(name).read_bytes()).hexdigest() == old_hash, f'control input changed: {name}'
summary = {'scope':'candidate scanner controls only; not adapter or repository coverage',
           'inputSha256':pins, 'results':results,
           'failed':sum(r['status']=='failed' for r in results),
           'unavailable':sum(r['status']=='unavailable' for r in results)}
(output / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
raise SystemExit(1 if summary['failed'] else 0)
