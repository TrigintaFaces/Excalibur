#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
# Exit 0: proven pass; 1: failed run/job; 2: incomplete or unverifiable evidence.
set -euo pipefail
gate_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ "${1:-}" = "--self-test" ]; then
    exec bash "$gate_dir/release-test-verdict-gate.test.sh"
fi
exec python3 "$gate_dir/release-test-verdict-gate.py" "$@"
