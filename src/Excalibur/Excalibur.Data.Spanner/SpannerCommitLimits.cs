// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Spanner;

/// <summary>
/// Hard per-commit limits imposed by Google Cloud Spanner. The <c>Excalibur.*.Spanner</c> stores chunk
/// large writes so that a single <c>Commit</c> never exceeds these bounds.
/// </summary>
/// <remarks>
/// Spanner rejects a commit that mutates more than <see cref="MaxMutationsPerCommit"/> cells or whose
/// serialized size exceeds <see cref="MaxCommitSizeBytes"/>. A "mutation" is counted per column written per
/// row (plus one per secondary-index entry touched), so a store must budget columns × rows, not just rows.
/// </remarks>
public static class SpannerCommitLimits
{
	/// <summary>The maximum number of mutations (column writes + index entries) permitted in one commit.</summary>
	/// <value>80,000 — the Spanner-documented per-commit mutation ceiling.</value>
	public const int MaxMutationsPerCommit = 80_000;

	/// <summary>The maximum serialized size, in bytes, permitted in one commit.</summary>
	/// <value>104,857,600 (100 MiB) — the Spanner-documented per-commit size ceiling.</value>
	public const long MaxCommitSizeBytes = 100L * 1024 * 1024;
}
