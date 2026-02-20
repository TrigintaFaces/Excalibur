// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides data retention policy enforcement based on <see cref="PersonalDataAttribute.RetentionDays"/>.
/// </summary>
/// <remarks>
/// <para>
/// This service scans types annotated with <see cref="PersonalDataAttribute"/> that have
/// retention periods configured and enforces cleanup of data that has exceeded its
/// retention period.
/// </para>
/// </remarks>
public interface IRetentionEnforcementService
{
	/// <summary>
	/// Enforces retention policies by scanning for and cleaning up expired personal data.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the enforcement scan.</returns>
	Task<RetentionEnforcementResult> EnforceRetentionAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the currently configured retention policies.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A collection of active retention policies.</returns>
	Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync(
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a data retention policy derived from <see cref="PersonalDataAttribute"/> annotations.
/// </summary>
public sealed record RetentionPolicy
{
	/// <summary>
	/// Gets the type name that the policy applies to.
	/// </summary>
	public required string TypeName { get; init; }

	/// <summary>
	/// Gets the property name that the policy applies to.
	/// </summary>
	public required string PropertyName { get; init; }

	/// <summary>
	/// Gets the data category of the personal data.
	/// </summary>
	public PersonalDataCategory Category { get; init; }

	/// <summary>
	/// Gets the retention period in days.
	/// </summary>
	public int RetentionDays { get; init; }
}

/// <summary>
/// Result of a retention enforcement scan.
/// </summary>
public sealed record RetentionEnforcementResult
{
	/// <summary>
	/// Gets the number of policies evaluated.
	/// </summary>
	public int PoliciesEvaluated { get; init; }

	/// <summary>
	/// Gets the number of records cleaned up.
	/// </summary>
	public int RecordsCleaned { get; init; }

	/// <summary>
	/// Gets a value indicating whether this was a dry run.
	/// </summary>
	public bool IsDryRun { get; init; }

	/// <summary>
	/// Gets the timestamp when the scan completed.
	/// </summary>
	public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Configuration options for retention enforcement.
/// </summary>
public sealed class RetentionEnforcementOptions
{
	/// <summary>
	/// Gets or sets the interval between retention enforcement scans.
	/// Default: 24 hours.
	/// </summary>
	public TimeSpan ScanInterval { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets a value indicating whether to perform a dry run without deleting data.
	/// Default: false.
	/// </summary>
	public bool DryRun { get; set; }

	/// <summary>
	/// Gets or sets the batch size for processing records.
	/// Default: 100.
	/// </summary>
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether the enforcement service is enabled.
	/// Default: true.
	/// </summary>
	public bool Enabled { get; set; } = true;
}
