"""Candidate only. Require one complete report for every evaluated project/TFM manifest.
Union declaration identities while preserving every source and configuration occurrence.
Does not compare, promote or rewrite a baseline. Not executed by author.
"""
import argparse
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--scope-index', required=True)
parser.add_argument('--report', action='append', default=[])
parser.add_argument('--report-list')
parser.add_argument('--output', required=True)
args = parser.parse_args()
report_paths = list(args.report)
report_list_receipt = None
if args.report_list:
    report_list_path = Path(args.report_list)
    report_list_bytes = report_list_path.read_bytes()
    try:
        listed = json.loads(report_list_bytes)
    except (ValueError, UnicodeError) as error:
        raise SystemExit(f'REFUSE: malformed report list: {error}')
    if not isinstance(listed, list) or not listed or any(not isinstance(p, str) or not p.strip() for p in listed):
        raise SystemExit('REFUSE: report list must be a nonempty JSON array of nonempty path strings')
    report_paths.extend(listed)
    report_list_receipt = {'path':str(report_list_path.resolve()),
                           'sha256':hashlib.sha256(report_list_bytes).hexdigest()}
if not report_paths:
    raise SystemExit('REFUSE: no report paths supplied')
normalized_paths = [str(Path(p).resolve()) for p in report_paths]
if len({p.casefold() for p in normalized_paths}) != len(normalized_paths):
    raise SystemExit('REFUSE: duplicate report paths')
if any(not Path(p).is_file() for p in normalized_paths):
    raise SystemExit('REFUSE: missing/non-file report path')
index_path = Path(args.scope_index)
index_bytes = index_path.read_bytes()
index = json.loads(index_bytes)
if not index.get('sourceCoverageComplete') or index.get('uncoveredSourceFiles'):
    raise SystemExit('REFUSE: historical all-src population has uncovered files; inspect scope-index residual evidence, do not drop them')
expected = {}
for scope in index['scopes']:
    manifest_bytes = Path(scope['manifest']).read_bytes()
    digest = hashlib.sha256(manifest_bytes).hexdigest().upper()
    if digest != scope['sha256'].upper() or digest in expected:
        raise SystemExit('REFUSE: changed or duplicate configuration manifest')
    expected[digest] = json.loads(manifest_bytes)
if not expected:
    raise SystemExit('REFUSE: empty evaluated scope index')
seen = set()
entries = {}
report_receipts = []
for report_path in normalized_paths:
    raw = Path(report_path).read_bytes()
    report = json.loads(raw)
    digest = (report.get('configurationManifestSha256') or '').upper()
    if not report.get('complete') or report.get('diagnostics') or digest not in expected or digest in seen:
        raise SystemExit(f'REFUSE: incomplete, unexpected or duplicated report: {report_path}')
    manifest = expected[digest]
    for name in ('project', 'configuration', 'targetFramework', 'langVersion'):
        if report.get(name) != manifest.get(name):
            raise SystemExit(f'REFUSE: report scope mismatch: {name}')
    if sorted(report['preprocessorSymbols']) != sorted(manifest['symbols']):
        raise SystemExit('REFUSE: report symbol mismatch')
    if not manifest['sourceFiles']:
        if manifest.get('zeroDeclarationDisposition') != 'evaluated-source-free' or report.get('zeroDeclarationDisposition') != 'evaluated-source-free' or report['entries']:
            raise SystemExit('REFUSE: source-free scope lacks explicit matching zero-declaration disposition')
    seen.add(digest)
    report_receipts.append({'path': report_path, 'sha256': hashlib.sha256(raw).hexdigest(),
                            'configurationManifestSha256': digest})
    for entry in report['entries']:
        item = entries.setdefault(entry['identity'], {'identity': entry['identity'],
            'declaration': entry['declaration'], 'annotation': entry['annotation'], 'occurrences': []})
        item['occurrences'].append({'project': report['project'], 'configuration': report['configuration'],
            'targetFramework': report['targetFramework'], 'manifestSha256': digest, 'sources': entry['sources']})
if seen != set(expected):
    raise SystemExit(f'REFUSE: {len(set(expected) - seen)} evaluated configurations missing')
with Path(args.output).open('x', encoding='utf-8') as output:
    json.dump({'complete': True, 'scope': 'Union of exactly the evaluated project/TFM scopes; lexical declarations, not compiled API equivalence',
               'scopeIndexSha256': hashlib.sha256(index_bytes).hexdigest(), 'reportList':report_list_receipt, 'reports': report_receipts,
               'entries': [entries[key] for key in sorted(entries)]}, output, indent=2)
    output.write('\n')
