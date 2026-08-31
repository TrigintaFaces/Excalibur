#!/usr/bin/env bash
# Config-binding completeness gate for Excalibur.Compliance.
#
# Builds the assembly with the config-binding source generator's output emitted to disk, then hands
# the generated binder to config-binder-completeness-gate.py, which fails the gate if any first-party
# type's generated BindCore assigns fewer properties than it declares as settable -- the signature of
# an `init`-only property the generator can never populate (see the .py file's docstring for why).
#
# Usage: eng/ci/config-binder-completeness-gate.sh
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
compliance_csproj="$repo_root/src/Excalibur/Excalibur.Compliance/Excalibur.Compliance.csproj"
generated_subdir="obj/_ConfigBinderGateInspect"
generated_dir="$repo_root/src/Excalibur/Excalibur.Compliance/$generated_subdir"

cleanup() { rm -rf "$generated_dir"; }
trap cleanup EXIT

rm -rf "$generated_dir"

"$repo_root/eng/ci/build-lock.sh" "$repo_root/.dotnet/dotnet" build "$compliance_csproj" \
    --no-incremental -nodeReuse:false \
    -p:EmitCompilerGeneratedFiles=true \
    -p:CompilerGeneratedFilesOutputPath="$generated_subdir"

generated_file="$(find "$generated_dir" -iname 'BindingExtensions.g.cs' | head -1)"
if [ -z "$generated_file" ]; then
    echo "config-binder-completeness-gate: BindingExtensions.g.cs not found -- generator not enabled?" >&2
    exit 2
fi

python3 "$repo_root/eng/ci/config-binder-completeness-gate.py" "$repo_root" "$generated_file"
