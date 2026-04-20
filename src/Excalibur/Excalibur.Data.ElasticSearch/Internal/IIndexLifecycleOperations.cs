// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/>
/// ILM and rollover endpoints used by <see cref="IndexLifecycleManager"/>.
/// Exposes <b>use-case</b> operations (manage policies + drive phase
/// transitions + rollover indices + inspect lifecycle status) — not SDK
/// factory shape — so tests can substitute the SDK without depending on
/// which <c>IndexLifecycleManagement</c> / <c>Indices</c> / <c>Transport</c>
/// overloads remain virtual in a given SDK minor version. Not a consumer-facing
/// abstraction; do not make this public.
/// </summary>
/// <remarks>
/// Third of the 6 γ seams per COMPASS S798 msg 1746 ruling + S799 msg 1799
/// naming precedent. <c>Operations</c> suffix (matching COMPASS's exemplar
/// <c>ISchemaEvolutionOperations</c>) describes the consumer's domain role —
/// the complete ILM/rollover workflow — rather than an SDK sub-client mirror.
/// Size: 5 methods, at the ADR-142 §D7 hard cap (S799 F1 lesson).
/// </remarks>
internal interface IIndexLifecycleOperations
{
	/// <summary>
	/// Creates or replaces an ILM policy with the given name.
	/// </summary>
	Task<LifecycleOperationResult> PutPolicyAsync(
		string policyName,
		IndexLifecyclePolicy policy,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes the named ILM policy. The outcome distinguishes
	/// <c>NotFound</c> (idempotent no-op) from <c>Failed</c> (actual error).
	/// </summary>
	Task<LifecyclePolicyDeleteOutcome> DeletePolicyAsync(
		string policyName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Rolls an index alias to a new backing index when any rollover
	/// condition is met.
	/// </summary>
	Task<LifecycleRolloverResult> RolloverAsync(
		string aliasName,
		RolloverConditions conditions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Returns the ILM status (phase, policy, age) for indices matching the
	/// given pattern. Hides the raw transport <c>/_ilm/explain</c> endpoint
	/// behind a domain-shaped DTO list.
	/// </summary>
	Task<IReadOnlyList<IndexLifecycleStatus>> GetStatusAsync(
		string indexPattern,
		CancellationToken cancellationToken);

	/// <summary>
	/// Advances an index from its current ILM phase to the next. Uses
	/// <c>_ilm/move</c> under the hood; adapter constructs the current/next
	/// step descriptors from the domain phase names.
	/// </summary>
	Task<LifecycleOperationResult> MoveToPhaseAsync(
		string indexName,
		string fromPhase,
		string toPhase,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of an <see cref="IIndexLifecycleOperations"/> write operation
/// other than delete. Domain shape — no SDK types cross the seam.
/// </summary>
internal readonly record struct LifecycleOperationResult(
	bool Success,
	string? ErrorDetails);

/// <summary>
/// Result of <see cref="IIndexLifecycleOperations.RolloverAsync"/>.
/// </summary>
internal readonly record struct LifecycleRolloverResult(
	bool Success,
	bool RolledOver,
	string? OldIndex,
	string? NewIndex,
	string? ErrorDetails);

/// <summary>
/// Outcome of <see cref="IIndexLifecycleOperations.DeletePolicyAsync"/>.
/// Distinguishes idempotent NotFound from real failures so the consumer can
/// log the two paths separately (matches pre-seam behavior).
/// </summary>
internal enum LifecyclePolicyDeleteOutcome
{
	/// <summary>Policy existed and was removed.</summary>
	Deleted,

	/// <summary>Policy did not exist (idempotent no-op).</summary>
	NotFound,

	/// <summary>Operation failed for a reason other than not-found.</summary>
	Failed,
}
