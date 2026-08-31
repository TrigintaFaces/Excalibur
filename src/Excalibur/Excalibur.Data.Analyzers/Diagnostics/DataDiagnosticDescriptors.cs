// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Data.Analyzers;

/// <summary>
/// Central diagnostic descriptors for the relational data-request analyzers.
/// </summary>
/// <remarks>
/// <para>
/// Diagnostic ID range: EXDATA001-EXDATA099.
/// </para>
/// <para>
/// <strong>What these analyzers deliberately do not do.</strong> Neither rule asks a request where its
/// tenant term is, and no rule fires because a term is absent. A rule of the form "every relational
/// statement must carry a tenant term" is how this framework produced its worst tenancy defect: a uniform
/// consistency pass added a term to seven statements including three already addressed by a primary key,
/// after which terminal marks matched nothing, messages were never marked sent, leases expired, and
/// messages were delivered again. A statement already addressed by a unique key cannot admit a foreign row,
/// so a tenant term on it is a filter whose only reachable output is a false negative.
/// </para>
/// <para>
/// An analyzer firing on absence is that sweep running forever, on every request anyone writes. These rules
/// therefore fire only on <em>positive</em> evidence of an inconsistency the compiler can see: a request
/// that accepted a tenant partition and then discarded it, and a justification that says nothing. A request
/// that never accepted a tenant is not a subject of either rule and needs no annotation to stay silent.
/// </para>
/// </remarks>
internal static class DataDiagnosticDescriptors
{
	/// <summary>Category for relational tenancy diagnostics.</summary>
	private const string TenancyCategory = "Excalibur.Data.Tenancy";

	/// <summary>
	/// EXDATA001: the justification on a no-tenant-term declaration is empty.
	/// </summary>
	/// <remarks>
	/// The factory this attribute replaces rejected a blank reason at run time. An attribute argument is a
	/// compile-time constant and no constructor can reject it, so without this rule the invariant would have
	/// been lost in the move — and a blank justification turns the index the attribute exists to produce back
	/// into the unexplained omission it exists to distinguish from an oversight.
	/// </remarks>
	public static readonly DiagnosticDescriptor NoTenantTermJustificationIsEmpty = new(
		id: "EXDATA001",
		title: "A declared absence of a tenant term must state its justification",
		messageFormat: "'{0}' declares that it carries no tenant term but supplies no justification. State why {1} applies to this statement.",
		category: TenancyCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A request may decline a tenant term, but it may not do so silently. The justification is what separates a considered decision from an oversight, and it is the only part of the declaration a reviewer cannot reconstruct from the code.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXDATA001");

	/// <summary>
	/// EXDATA002: a data request accepted a tenant partition and never used it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the discarded-context defect the framework has shipped twice: a resolved partition handed to a
	/// component that dropped it, after which every read compared rows against a constant sentinel and an
	/// operator's backlog alert reported empty while the store filled.
	/// </para>
	/// <para>
	/// The rule fires only when the parameter is referenced <em>nowhere at all</em> in the constructor. Any
	/// use — binding it, converting it, storing it, passing it to a base constructor or a helper — silences
	/// it. It does not attempt to prove that the value reaches the outgoing parameters, because a proof that
	/// fails is indistinguishable from a defect, and a false positive here fails a build.
	/// </para>
	/// </remarks>
	public static readonly DiagnosticDescriptor TenantPartitionParameterIsDiscarded = new(
		id: "EXDATA002",
		title: "A data request must not accept a tenant partition and discard it",
		messageFormat: "'{0}' accepts the tenant partition '{1}' and never uses it. Bind it into the request's parameters, or remove the parameter — a request that carries no tenant term takes no tenant.",
		category: TenancyCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A partition parameter that is accepted and then dropped reads at every call site exactly like one that is honoured. The caller resolved a tenant, passed it in, and the statement was built without it; nothing in the signature records that. Either the value belongs in the statement, or the parameter does not belong in the signature.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXDATA002");
}
