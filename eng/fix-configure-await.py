#!/usr/bin/env python3
"""Add missing ConfigureAwait(false) to await expressions in C# library code.

Handles:
  - await expr;  -> await expr.ConfigureAwait(false);
  - await using var x = expr;  -> await using var x = expr.ConfigureAwait(false);
  - Multi-line await where the statement ends with ; on a later line

Skips:
  - Lines already containing ConfigureAwait
  - await Task.Yield()  (cannot ConfigureAwait)
  - Comments (// and ///)
  - String literals containing "await"
  - Files in obj/, SourceGenerators/, *.g.cs
"""

import os
import re
import sys


def should_skip_line(line):
    """Check if line should be skipped."""
    stripped = line.strip()
    if not stripped:
        return True
    if stripped.startswith('//') or stripped.startswith('///'):
        return True
    if stripped.startswith('/*') or stripped.startswith('*'):
        return True
    if 'ConfigureAwait' in line:
        return True
    if '"await' in line:
        return True
    if 'GetAwaiter' in line:
        return True
    if 'Task.Yield()' in line:
        return True
    # Skip await using var - ConfigureAwait changes the variable type to
    # ConfiguredAsyncDisposable which breaks subsequent method calls
    if 'await using' in line:
        return True
    return False


def fix_single_line_await(line):
    """Fix a single-line await that ends with ;"""
    stripped = line.rstrip()

    # await foreach (var x in expr)  -- rare on single line
    m = re.match(r'^(.*await\s+foreach\s*\(.*\s+in\s+)(.*?)(\))(\s*$)', stripped)
    if m:
        return m.group(1) + m.group(2) + '.ConfigureAwait(false)' + m.group(3) + '\n'

    # Regular await expr;
    if 'await ' in line and stripped.endswith(';'):
        return stripped[:-1] + '.ConfigureAwait(false);\n'

    return None  # Not a single-line case


def process_file(filepath, dry_run=False):
    """Process a single file, adding ConfigureAwait(false) where missing."""
    with open(filepath, 'r', encoding='utf-8-sig', errors='replace') as f:
        lines = f.readlines()

    changes = []
    new_lines = list(lines)
    i = 0

    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        if should_skip_line(line) or 'await ' not in line:
            i += 1
            continue

        # Check if this is a single-line await ending with ;
        if stripped.endswith(';'):
            indent = line[:len(line) - len(line.lstrip())]
            fixed = fix_single_line_await(line)
            if fixed:
                new_lines[i] = indent + fixed.lstrip()
                changes.append((i + 1, stripped[:100]))
            i += 1
            continue

        # Multi-line: find where the statement ends (;)
        # Look ahead for the line ending with ;
        found_end = False
        for j in range(i + 1, min(i + 20, len(lines))):
            next_stripped = lines[j].strip()

            # If we find ConfigureAwait before ;, already handled
            if 'ConfigureAwait' in next_stripped:
                found_end = True
                break

            if next_stripped.endswith(';'):
                # This is the end of the multi-line statement
                # Check the line doesn't already have ConfigureAwait
                if 'ConfigureAwait' not in next_stripped:
                    indent_j = lines[j][:len(lines[j]) - len(lines[j].lstrip())]
                    # Insert .ConfigureAwait(false) before the ;
                    fixed_end = next_stripped[:-1] + '.ConfigureAwait(false);'
                    new_lines[j] = indent_j + fixed_end + '\n'
                    changes.append((i + 1, stripped[:100]))
                found_end = True
                break

            # If we hit a { or // it's a different construct
            if next_stripped.startswith('{') or next_stripped.startswith('//'):
                break

        i += 1

    if changes and not dry_run:
        with open(filepath, 'w', encoding='utf-8', newline='') as f:
            f.writelines(new_lines)

    return changes


def main():
    dry_run = '--dry-run' in sys.argv
    src_dirs = ['src/Dispatch', 'src/Excalibur']

    total_changes = 0
    total_files = 0

    for src_dir in src_dirs:
        if not os.path.isdir(src_dir):
            print(f'  Skipping {src_dir} (not found)')
            continue

        for root, dirs, files in os.walk(src_dir):
            dirs[:] = [d for d in dirs if d != 'obj' and 'SourceGenerators' not in d]
            for f in sorted(files):
                if not f.endswith('.cs') or f.endswith('.g.cs'):
                    continue
                filepath = os.path.join(root, f)
                changes = process_file(filepath, dry_run)
                if changes:
                    total_files += 1
                    total_changes += len(changes)
                    mode = '[DRY RUN] ' if dry_run else ''
                    print(f'{mode}{filepath}: {len(changes)} fixes')

    print(f'\n{"[DRY RUN] " if dry_run else ""}Total: {total_changes} fixes across {total_files} files')


if __name__ == '__main__':
    main()
