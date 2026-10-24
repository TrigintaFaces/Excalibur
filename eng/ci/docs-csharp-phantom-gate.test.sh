#!/usr/bin/env bash
# Scope proof is independent of C# binding; compiler positive/negative controls run in Boundary.Tests.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "$SCRIPT_DIR/docs-csharp-extract.test.sh"
