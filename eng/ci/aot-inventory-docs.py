"""Preserved consumer-warning table policy, backed by annotations AND silent suppressions."""
import json
from pathlib import Path
import re
import tempfile

ROW = re.compile(r"^\|\s*`([A-Za-z0-9_.]+)`([^|]*)\|([^|]*)\|", re.M)
STATUSES={'Clean','Warns you',"Doesn't warn you",'Not compatible','N/A'}
ROLES={'','(bundled generator)','(bundled analyzer)'}

def check_docs(identities, docs_path, suppression_path):
    try:
        if not identities or any(not isinstance(i,str) or not i for i in identities):
            return {'exitCode':2,'diagnostics':['REFUSE: annotation evidence empty/malformed']}
        annotated = set()
        for identity in identities:
            parts = identity.split('::',1)[0].split('/')
            if len(parts)>2 and parts[0]=='src':
                annotated.add(parts[2])
        suppression_bytes = Path(suppression_path).read_bytes()
        # Preserve the old baseline's source-path extraction while additionally validating JSON.
        json.loads(suppression_bytes)
        suppressed = set(re.findall(r'src/[A-Za-z0-9_.]+/([A-Za-z0-9_.]+)/',suppression_bytes.decode('utf-8-sig')))
        rows = ROW.findall(Path(docs_path).read_text(encoding='utf-8-sig'))
    except (OSError,ValueError,UnicodeError) as error:
        return {'exitCode':2,'diagnostics':[f'REFUSE: documentation evidence unreadable: {error}']}
    if not rows or not annotated or not suppressed:
        return {'exitCode':2,'diagnostics':[f'REFUSE: empty docs rows ({len(rows)}), annotated packages ({len(annotated)}) or suppressed packages ({len(suppressed)})']}
    wrong = []
    seen=set()
    primary=set()
    parsed=[]
    for package,role,raw_status in rows:
        role=role.strip()
        status=raw_status.strip()
        if status.startswith('**') and status.endswith('**'):
            status=status[2:-2]
        if status not in STATUSES:
            wrong.append(f'{package}: unknown matrix status {raw_status.strip()!r}')
        if role not in ROLES:
            wrong.append(f'{package}: unknown matrix role {role!r}')
        key=(package,role)
        if key in seen:
            wrong.append(f'{package} {role}: duplicate matrix row')
        seen.add(key)
        if not role:
            primary.add(package)
        elif status!='N/A':
            wrong.append(f'{package} {role}: bundled compiler tooling role must be N/A')
        parsed.append({'package':package,'role':role,'status':status})
        if role:
            continue
        if status=='Clean' and package in annotated:
            wrong.append(f'{package}: Clean contradicts annotated declarations that warn consumers')
        elif status=='Clean' and package in suppressed:
            wrong.append(f'{package}: Clean contradicts baselined silent AOT suppressions')
        elif status=='Warns you' and package not in annotated:
            wrong.append(f'{package}: Warns you has no annotated declaration evidence')
        elif status=="Doesn't warn you" and package not in suppressed:
            wrong.append(f'{package}: Does not warn has no baselined suppression evidence')
    expected=annotated | suppressed
    for missing in sorted(expected-primary):
        wrong.append(f'{missing}: required governed package row is missing')
    return {'exitCode':1 if wrong else 0,'diagnostics':wrong,'rows':len(rows),
            'annotatedPackages':sorted(annotated),'suppressedPackages':sorted(suppressed),
            'expectedPackages':sorted(expected),'parsedRows':parsed,
            'scope':'Inventory evidence coverage: every annotated or suppressed package requires a runtime matrix row. Five statuses parsed; not whole shipping catalog completeness or compatibility certification. Not compatible metadata and tooling classification require their own evidence.'}

def self_test():
    results = []
    with tempfile.TemporaryDirectory(prefix='aot-docs-controls-') as temporary:
        root = Path(temporary)
        docs, suppression = root/'docs.md', root/'suppression.json'
        identities=['src/Dispatch/Pkg.Annotated/C.cs::N.C.Call()::RequiresDynamicCode']
        suppression.write_text(json.dumps({'suppressions':[{'file':'src/Excalibur/Pkg.Silent/C.cs','rule':'IL2026'}]}),encoding='utf-8')
        agree="## table\n| `Pkg.Annotated` | Warns you | |\n| `Pkg.Silent` | Doesn't warn you | |\n| `Pkg.Clean` | Clean | |\n"
        for name,text,expected in [
            ('agree',agree,0),
            ('clean-over-annotated',agree.replace('Warns you','Clean'),1),
            ('clean-over-suppressed',agree.replace("Doesn't warn you",'Clean'),1),
            ('warns-over-clean',agree.replace('`Pkg.Clean` | Clean','`Pkg.Clean` | Warns you'),1),
            ('silent-over-clean',agree.replace('`Pkg.Clean` | Clean',"`Pkg.Clean` | Doesn't warn you"),1),
            ('missing-annotated-row',agree.replace('| `Pkg.Annotated` | Warns you | |\n',''),1),
            ('missing-suppressed-row',agree.replace("| `Pkg.Silent` | Doesn't warn you | |\n",''),1),
            ('unknown-status',agree.replace('Warns you','Warnz you'),1),
            ('duplicate-package',agree+'| `Pkg.Annotated` | Warns you | |\n',1),
            ('bold-not-compatible',agree+'| `Pkg.Incompatible` | **Not compatible** | |\n',0),
            ('tooling-role',agree+'| `Pkg.Annotated` (bundled generator) | **N/A** | |\n',0),
            ('role-does-not-cover-runtime',agree.replace('| `Pkg.Annotated` | Warns you | |\n',
                '| `Pkg.Annotated` (bundled generator) | N/A | |\n'),1),
            ('empty-docs','',2)]:
            docs.write_text(text,encoding='utf-8')
            actual=check_docs(identities,docs,suppression)['exitCode']
            results.append({'name':name,'expected':expected,'actual':actual,'passed':actual==expected})
        docs.write_text(agree,encoding='utf-8')
        for name,actual in [('missing-docs',check_docs(identities,root/'missing.md',suppression)['exitCode']),
                            ('empty-annotations',check_docs([],docs,suppression)['exitCode'])]:
            results.append({'name':name,'expected':2,'actual':actual,'passed':actual==2})
        suppression.write_text('{"suppressions":[]}',encoding='utf-8')
        actual=check_docs(identities,docs,suppression)['exitCode']
        results.append({'name':'empty-suppressions','expected':2,'actual':actual,'passed':actual==2})
    return {'exitCode':0 if all(r['passed'] for r in results) else 1,'results':results}
