#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
# Workflow values arrive as data, never as generated shell source. Validate before publishing
# outputs. Keep the repository's X.Y.Z[-label[.N]] release grammar; branch builds may use MinVer.
set -euo pipefail
refuse() { echo "::error::REFUSE: $1" >&2; exit 1; }
[ "$#" -eq 1 ] || refuse 'Expected official or release mode.'
case "$1" in official|release) mode="$1" ;; *) refuse 'Unsupported version resolution mode.' ;; esac
version=''
prerelease='false'
case "${GITHUB_EVENT_NAME:-}" in
    workflow_dispatch)
        version="${DISPATCH_VERSION:-}"
        prerelease="${DISPATCH_PRERELEASE:-false}"
        case "$prerelease" in true|false) ;; *) refuse 'Prerelease input must be true or false.' ;; esac
        # Official builds selected at a version tag retain that tag's version, as before.
        if [ "$mode" = official ] && [[ "${GITHUB_REF:-}" = refs/tags/v* ]]; then
            version="${GITHUB_REF#refs/tags/v}"
            [ -n "$version" ] || refuse 'Version tag is empty.'
            [[ "$version" != *-* ]] || prerelease='true'
        fi
        ;;
    push)
        case "${GITHUB_REF:-}" in
            refs/tags/v*)
                version="${GITHUB_REF#refs/tags/v}"
                [ -n "$version" ] || refuse 'Version tag is empty.'
                [[ "$version" != *-* ]] || prerelease='true'
                ;;
            refs/heads/*) [ "$mode" = official ] || refuse 'Release push must identify a version tag.' ;;
            *) refuse 'Unsupported source ref.' ;;
        esac
        ;;
    *) refuse 'Unsupported version resolution event.' ;;
esac
if [ -z "$version" ]; then
    [ "$mode" = official ] || refuse 'A release version is required.'
    echo 'No explicit version: MinVer supplies the branch build version.'
else
    # The anchored allowlist also excludes whitespace, shell syntax and workflow-output newlines.
    [[ "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-([a-zA-Z0-9-]+)(\.(0|[1-9][0-9]*))?)?$ ]] ||
        refuse 'Expected X.Y.Z or X.Y.Z-label[.N], with no numeric leading zeros.'
    label="${BASH_REMATCH[5]:-}"
    if [[ "$label" =~ ^[0-9]+$ ]] && [[ "$label" =~ ^0[0-9]+$ ]]; then
        refuse 'Numeric prerelease identifiers cannot have leading zeros.'
    fi
    printf 'Validated explicit version: %s\n' "$version"
fi
[ -n "${GITHUB_OUTPUT:-}" ] || refuse 'Missing workflow output destination.'
printf 'version=%s\nprerelease=%s\n' "$version" "$prerelease" >> "$GITHUB_OUTPUT"
