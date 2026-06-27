// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Store-specific contributor that performs the actual deletion of personal data that has exceeded
/// its retention period. The framework's <see cref="IRetentionEnforcementService"/> discovers the
/// retention policies (from <see cref="PersonalDataAttribute.RetentionDays"/>) and orchestrates the
/// registered contributors; each contributor knows how to delete expired records from its own store.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors the erasure-side <c>IErasureContributor</c> seam: enforcement is policy-driven by the
/// framework, but the data-store integration is supplied by the consumer. Register one or more
/// contributors in DI so retention enforcement actually deletes data instead of being a no-op.
/// </para>
/// <para>
/// When no contributor is registered, <see cref="IRetentionEnforcementService"/> logs a warning and
/// reports zero records cleaned — it never reports success while deleting nothing.
/// </para>
/// </remarks>
public interface IRetentionContributor
{
	/// <summary>
	/// Gets the human-readable name of this contributor (used in diagnostics).
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Enforces the supplied retention policies against this contributor's store, deleting records
	/// whose retention period has elapsed relative to <see cref="RetentionContributorContext.AsOf"/>.
	/// </summary>
	/// <param name="context">The retention enforcement context (policies, dry-run flag, evaluation time).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The outcome, including the number of records cleaned.</returns>
	Task<RetentionContributorResult> EnforceAsync(
		RetentionContributorContext context,
		CancellationToken cancellationToken);
}

/// <summary>
/// Context supplied to an <see cref="IRetentionContributor"/> for a single enforcement pass.
/// </summary>
public sealed record RetentionContributorContext
{
	/// <summary>
	/// Gets the retention policies discovered from <see cref="PersonalDataAttribute"/> annotations.
	/// </summary>
	public required IReadOnlyList<RetentionPolicy> Policies { get; init; }

	/// <summary>
	/// Gets a value indicating whether this is a dry run (no data should be deleted).
	/// </summary>
	public required bool DryRun { get; init; }

	/// <summary>
	/// Gets the timestamp the enforcement pass is evaluated against. Records whose
	/// (last-modified + retention period) is at or before this instant are expired.
	/// </summary>
	public required DateTimeOffset AsOf { get; init; }
}

/// <summary>
/// Result of an <see cref="IRetentionContributor"/> enforcement pass.
/// </summary>
public sealed record RetentionContributorResult
{
	/// <summary>
	/// Gets a value indicating whether the contributor completed successfully.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the number of records cleaned (deleted) by this contributor.
	/// </summary>
	public int RecordsCleaned { get; init; }

	/// <summary>
	/// Gets the error message when <see cref="Success"/> is <see langword="false"/>.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Creates a successful result for the given number of cleaned records.
	/// </summary>
	/// <param name="recordsCleaned">The number of records cleaned.</param>
	/// <returns>A successful <see cref="RetentionContributorResult"/>.</returns>
	public static RetentionContributorResult Succeeded(int recordsCleaned) => new()
	{
		Success = true,
		RecordsCleaned = recordsCleaned,
	};

	/// <summary>
	/// Creates a failed result with the supplied error message.
	/// </summary>
	/// <param name="errorMessage">The error message.</param>
	/// <returns>A failed <see cref="RetentionContributorResult"/>.</returns>
	public static RetentionContributorResult Failed(string errorMessage) => new()
	{
		Success = false,
		ErrorMessage = errorMessage,
	};
}
