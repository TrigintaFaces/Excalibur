// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.Migration.Analyzers;

/// <summary>
/// Diagnostic descriptors for the MediatR/MassTransit migration tooling.
/// </summary>
/// <remarks>
/// <para>
/// Diagnostic ID range: <c>EXMIG0001</c>–<c>EXMIG00NN</c> (reserved for migration diagnostics).
/// These do NOT overlap with the analyzer guidance diagnostics (<c>DISP001</c>–<c>DISP099</c>),
/// LoggerMessage Event IDs (1–4999), or generator diagnostics (<c>HND001</c>+).
/// </para>
/// <para>
/// All migration diagnostics use category <c>Migration</c> and are release-tracked in
/// <c>AnalyzerReleases.Shipped.md</c> / <c>AnalyzerReleases.Unshipped.md</c>.
/// </para>
/// </remarks>
internal static class MigrationDiagnosticDescriptors
{
	/// <summary>
	/// Category for migration tooling diagnostics.
	/// </summary>
	internal const string MigrationCategory = "Migration";

	/// <summary>
	/// EXMIG0001: MediatR DI registration is mechanically portable.
	/// </summary>
	/// <remarks>
	/// Informational diagnostic reported when the analyzer encounters a <c>services.AddMediatR(...)</c>
	/// registration call, identifying it as mechanically portable to the Excalibur compat registration
	/// entry point <c>AddMediatRCompat(...)</c>. A code-fix performs the rewrite (bead <c>wfh6e3</c>).
	/// </remarks>
	public static readonly DiagnosticDescriptor MediatRRegistrationPortable = new(
		id: MigrationDiagnosticIds.MediatRRegistrationPortable,
		title: "MediatR registration is portable to Excalibur.Dispatch",
		messageFormat: "'{0}' can be mechanically migrated to 'AddMediatRCompat(...)' (Excalibur.Dispatch compat registration)",
		category: MigrationCategory,
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "MediatR DI registration calls map directly onto the Excalibur.Dispatch compat registration entry point. Applying the code-fix rewrites the call to 'AddMediatRCompat(...)', preserving assembly-scan arguments, as part of migrating off the now-commercial MediatR package.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXMIG0001");

	/// <summary>
	/// EXMIG0002: MediatR construct with no deterministic mechanical rewrite (manual step required).
	/// </summary>
	/// <remarks>
	/// Informational diagnostic reported when the analyzer encounters a MediatR construct outside the
	/// shimmed published contract — e.g. <c>IRequestPreProcessor</c>,
	/// <c>IRequestPostProcessor</c>, <c>IRequestExceptionHandler</c>, <c>IRequestExceptionAction</c>,
	/// or <c>IStreamPipelineBehavior</c>. These have no deterministic mechanical rewrite, so the
	/// diagnostic describes the manual migration step rather than silently skipping it (FR-14 / AC-10).
	/// </remarks>
	public static readonly DiagnosticDescriptor NonDeterministicConstruct = new(
		id: MigrationDiagnosticIds.NonDeterministicConstruct,
		title: "MediatR construct requires a manual migration step",
		messageFormat: "'{0}' implements MediatR '{1}', which is outside the Excalibur.Dispatch compat contract and has no automatic rewrite — see the migration guide for the manual step",
		category: MigrationCategory,
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Some MediatR constructs (pre/post processors, exception handlers/actions, stream pipeline behaviors) are not part of the Excalibur.Dispatch compat surface and cannot be mechanically rewritten. This informational diagnostic surfaces them so the remaining manual migration step is explicit rather than silently skipped.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXMIG0002");

	/// <summary>
	/// EXMIG0003: <c>using MediatR;</c> directive is swappable to the compat namespace.
	/// </summary>
	/// <remarks>
	/// Reported on a <c>using MediatR;</c> directive (FR-12 / AC-15). The companion code-fix swaps it to
	/// <c>using Excalibur.Dispatch.Compat.MediatR;</c>, idempotently and without producing duplicate or
	/// orphaned using directives (EC-7 / EC-8).
	/// </remarks>
	public static readonly DiagnosticDescriptor MediatRUsingDirectiveSwappable = new(
		id: MigrationDiagnosticIds.MediatRUsingDirectiveSwappable,
		title: "MediatR using directive is swappable to the Excalibur.Dispatch compat namespace",
		messageFormat: "'using {0};' can be swapped to 'using Excalibur.Dispatch.Compat.MediatR;' (Excalibur.Dispatch migration)",
		category: MigrationCategory,
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "A 'using MediatR;' directive can be mechanically swapped to the Excalibur.Dispatch compat namespace so the file's MediatR shapes resolve against the compat surface after migration.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXMIG0003");

	/// <summary>
	/// EXMIG0004: handler method signature differs from the compat handler shape.
	/// </summary>
	/// <remarks>
	/// Reported on a handler implementing a compat <c>IRequestHandler</c>/<c>INotificationHandler</c>
	/// whose handler method name differs from the compat shape's <c>Handle</c> (e.g. <c>HandleAsync</c>).
	/// A deterministic delta (method rename) is offered as a code-fix; a non-deterministic delta surfaces
	/// the manual change in the message (FR-13 / AC-16, no silent skip).
	/// </remarks>
	public static readonly DiagnosticDescriptor HandlerSignatureDelta = new(
		id: MigrationDiagnosticIds.HandlerSignatureDelta,
		title: "Handler signature differs from the Excalibur.Dispatch compat shape",
		messageFormat: "Handler '{0}' method '{1}' should be '{2}' to match the Excalibur.Dispatch compat handler shape",
		category: MigrationCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Excalibur.Dispatch compat handlers implement 'Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)'. A handler whose method name differs (e.g. 'HandleAsync') has a deterministic rename code-fix; other signature deltas are described for manual migration.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXMIG0004");
}
