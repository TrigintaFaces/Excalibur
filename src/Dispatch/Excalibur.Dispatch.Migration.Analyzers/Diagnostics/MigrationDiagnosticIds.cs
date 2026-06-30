// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Migration.Analyzers;

/// <summary>
/// Stable string identifiers for the migration tooling diagnostics (EXMIG range).
/// </summary>
/// <remarks>
/// These IDs are the cross-assembly contract between this analyzer package and the companion
/// <c>Excalibur.Dispatch.Migration.CodeFixes</c> package, whose code-fix providers declare them in
/// <c>FixableDiagnosticIds</c>. They are part of the published diagnostic surface (release-tracked in
/// <c>AnalyzerReleases.*.md</c>), hence intentionally <see langword="public"/>.
/// </remarks>
public static class MigrationDiagnosticIds
{
	/// <summary>
	/// EXMIG0001 — MediatR DI registration is mechanically portable to <c>AddMediatRCompat(...)</c>.
	/// </summary>
	public const string MediatRRegistrationPortable = "EXMIG0001";

	/// <summary>
	/// EXMIG0002 — a MediatR construct outside the shimmed published contract has no deterministic
	/// mechanical rewrite; a manual migration step is required (informational, never a silent skip).
	/// </summary>
	public const string NonDeterministicConstruct = "EXMIG0002";

	/// <summary>
	/// EXMIG0003 — a <c>using MediatR;</c> directive is swappable to the Excalibur.Dispatch compat
	/// namespace (<c>using Excalibur.Dispatch.Compat.MediatR;</c>).
	/// </summary>
	public const string MediatRUsingDirectiveSwappable = "EXMIG0003";

	/// <summary>
	/// EXMIG0004 — a handler's method signature differs from the compat shape (e.g. <c>HandleAsync</c>
	/// instead of <c>Handle</c>); a deterministic delta is auto-fixable, otherwise a manual step.
	/// </summary>
	public const string HandlerSignatureDelta = "EXMIG0004";
}
