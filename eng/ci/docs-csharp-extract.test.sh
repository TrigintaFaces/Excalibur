#!/usr/bin/env bash
# Markdown selection controls. C# semantic controls live in DocumentationApiRuleShould.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
python3 - "$SCRIPT_DIR/docs-csharp-extract.py" <<'PY'
import importlib.util, pathlib, tempfile, subprocess, sys, json
spec=importlib.util.spec_from_file_location("extract",sys.argv[1]); m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
with tempfile.TemporaryDirectory() as tmp:
    root=pathlib.Path(tmp); (root/'docs').mkdir(); doc=root/'docs'/'test.md'
    doc.write_text('# Title\n```csharp\nMissing x;\n```\n~~~cs runnable\nnew Real();\n~~~\n```cs runnable ignore\nFake();\n```\n```cs\n// no-compile\nFake();\n```\n',encoding='utf-8')
    blocks=m.extract_blocks(tmp,tmp)
    assert len(blocks)==4 and [b['tier'] for b in blocks]==['resolve','compile','ignore','ignore']
    assert blocks[0]['startLine']==2 and blocks[0]['endLine']==4
    git=lambda *args:subprocess.run(['git','-C',tmp,*args],check=True,capture_output=True)
    git('init');git('add','.');git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-m','fixture')
    assert m.changed_lines(tmp,'HEAD')==({},set())
    doc.write_text(doc.read_text().replace('Missing x;','Missing y;'))
    lines,new=m.changed_lines(tmp,'HEAD');assert lines=={'docs/test.md':{3}} and not new
    request=json.loads(subprocess.check_output([sys.executable,sys.argv[1],'--repo',tmp,'--request','--base','HEAD']))
    assert [b['selected'] for b in request]==[True,False,False,False]
    (root/'docs'/'new.md').write_text('```cs\nNew();\n```\n')
    assert 'docs/new.md' in m.changed_lines(tmp,'HEAD')[1]
    result=subprocess.run([sys.executable,sys.argv[1],'--repo',tmp,'--request','--base','not-a-real-base'],capture_output=True)
    assert result.returncode==2 and b'REFUSE' in result.stderr
    # Editing prose must not select any unchanged C# fence.
    git('add','.');git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-m','second')
    doc.write_text(doc.read_text().replace('# Title','# Changed title'))
    request=json.loads(subprocess.check_output([sys.executable,sys.argv[1],'--repo',tmp,'--request','--base','HEAD']))
    assert not any(b['selected'] for b in request)
    # Generated copies cannot inflate the source population or supply example context.
    for name in ('artifacts', 'graphify-out'):
        (root/name).mkdir()
        (root/name/'README.md').write_text('```cs\nGeneratedCopy();\n```\n')
    (root/'docs'/'artifacts').mkdir()
    (root/'docs'/'artifacts'/'guide.md').write_text('```cs\nSourceDocument();\n```\n')
    population=m.extract_blocks(tmp,tmp)
    assert not any('GeneratedCopy' in b['code'] for b in population)
    assert any('SourceDocument' in b['code'] for b in population)
    (root/'README.md').write_text('```cs\nRootReadme();\n```\n')
    (root/'src'/'package').mkdir(parents=True)
    (root/'src'/'package'/'README.md').write_text('```cs\nPackageReadme();\n```\n')
    (root/'docs'/'artifacts'/'README.md').write_text('```cs\nNestedReadme();\n```\n')
    population=m.extract_blocks(tmp,tmp)
    for expected in ('RootReadme', 'PackageReadme', 'NestedReadme'):
        assert any(expected in b['code'] for b in population)
    scoped=m.extract_blocks(str(root/'docs'),tmp)
    assert any('NestedReadme' in b['code'] for b in scoped)
    assert m.extract_blocks(str(root/'artifacts'),tmp)==[]
print('PASS: fence classification, ignore precedence, changed hunks, new files, prose-only edits, missing-base refusal')
PY
