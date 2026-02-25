import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const roots = [
  'docs',
  'src',
  '../docs/package-readme-templates',
];
const explicitFiles = [
  'docusaurus.config.ts',
];

const extensions = new Set(['.md', '.mdx', '.ts', '.tsx', '.js', '.jsx']);

const lineChecks = [
  /\]\(\/docs\/next(?=[\/#\)"'])/,
  /\bto=["']\/docs\/next(?=[\/#"'])/,
  /\bhref=["']\/docs\/next(?=[\/#"'])/,
  /^\[[^\]]+\]:\s*\/docs\/next(?=[\/#\s]|$)/,
];

function walk(dir, collector) {
  if (!statSync(dir).isDirectory()) {
    return;
  }

  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'node_modules' || entry.name === 'build' || entry.name === '.docusaurus') {
        continue;
      }

      walk(fullPath, collector);
      continue;
    }

    const extension = entry.name.slice(entry.name.lastIndexOf('.'));
    if (extensions.has(extension)) {
      collector.push(fullPath);
    }
  }
}

const files = [];
for (const root of roots) {
  try {
    walk(root, files);
  } catch {
    // Skip missing roots to keep script robust in partial checkouts.
  }
}

for (const file of explicitFiles) {
  try {
    if (statSync(file).isFile()) {
      files.push(file);
    }
  } catch {
    // Skip missing files in partial checkouts.
  }
}

const violations = [];
for (const file of files) {
  const content = readFileSync(file, 'utf8');
  const lines = content.split(/\r?\n/);

  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    if (line.includes('allow-docs-next-link')) {
      continue;
    }

    for (const check of lineChecks) {
      if (check.test(line)) {
        violations.push({ file, line: index + 1, text: line.trim() });
        break;
      }
    }
  }
}

if (violations.length > 0) {
  console.error('Found hardcoded /docs/next links. Use /docs/... (canonical) or relative links.');
  console.error('If this is intentional, add allow-docs-next-link on that line.');

  for (const violation of violations) {
    console.error(`- ${relative(process.cwd(), violation.file)}:${violation.line}`);
    console.error(`  ${violation.text}`);
  }

  process.exit(1);
}

console.log('Version-safe link validation passed (no hardcoded /docs/next links).');
