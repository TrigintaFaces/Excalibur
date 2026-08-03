#!/usr/bin/env bash
# The repository's build entry point. CI runs this; contributors run this.
#
#   ./build.sh                                  restore + build
#   ./build.sh --test
#   ./build.sh --test --project eng/ci/shards/UnitTests-Core.slnf
#   ./build.sh --build --configuration Debug
#   ./build.sh --pack
#
# Thin by design: the logic lives in eng/build.ps1 so the Windows and Unix entry points cannot
# drift. pwsh is already a build dependency of this repo.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

command -v pwsh >/dev/null 2>&1 || {
    echo "pwsh (PowerShell 7+) is required: https://learn.microsoft.com/powershell/scripting/install/installing-powershell" >&2
    exit 2
}

# --verb -> -Verb. Values pass through untouched, so an argument like a --filter expression
# containing '-' is not mangled.
args=()
expecting_value=0
for a in "$@"; do
    if [ "$expecting_value" = "1" ]; then
        args+=("$a"); expecting_value=0; continue
    fi
    case "$a" in
        --project|--configuration|--test-filter|--verbosity|--properties)
            name="${a#--}"
            # kebab-case -> PascalCase for the pwsh parameter name
            pascal="$(echo "$name" | awk -F- '{for(i=1;i<=NF;i++) printf toupper(substr($i,1,1)) substr($i,2); print ""}')"
            args+=("-$pascal"); expecting_value=1 ;;
        --*)
            name="${a#--}"
            pascal="$(echo "$name" | awk -F- '{for(i=1;i<=NF;i++) printf toupper(substr($i,1,1)) substr($i,2); print ""}')"
            args+=("-$pascal") ;;
        *)
            args+=("$a") ;;
    esac
done

exec pwsh -NoProfile -NonInteractive -File "$root/eng/build.ps1" "${args[@]}"
