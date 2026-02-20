// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the current status of a key escrow.
/// </summary>
public sealed record EscrowStatus
{
	/// <summary>
	/// Gets the unique identifier of the escrowed key.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the unique identifier of this escrow record.
	/// </summary>
	public required string EscrowId { get; init; }

	/// <summary>
	/// Gets the current state of the escrow.
	/// </summary>
	public required EscrowState State { get; init; }

	/// <summary>
	/// Gets the timestamp when the key was escrowed.
	/// </summary>
	public required DateTimeOffset EscrowedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the escrow expires, if applicable.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets the number of active (non-expired, non-used) recovery tokens.
	/// </summary>
	public int ActiveTokenCount { get; init; }

	/// <summary>
	/// Gets the number of recovery operations performed on this escrow.
	/// </summary>
	public int RecoveryAttempts { get; init; }

	/// <summary>
	/// Gets the timestamp of the last recovery attempt, if any.
	/// </summary>
	public DateTimeOffset? LastRecoveryAttempt { get; init; }

	/// <summary>
	/// Gets a value indicating whether the escrow is still valid for recovery.
	/// </summary>
	public bool IsRecoverable =>
		State == EscrowState.Active &&
		(ExpiresAt is null || DateTimeOffset.UtcNow < ExpiresAt);

	/// <summary>
	/// Gets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the purpose or scope of this escrow.
	/// </summary>
	public string? Purpose { get; init; }
}

/// <summary>
/// Represents the lifecycle state of a key escrow.
/// </summary>
public enum EscrowState
{
	/// <summary>
	/// The escrow is active and available for recovery.
	/// </summary>
	Active = 0,

	/// <summary>
	/// The escrow has been used for recovery and is now pending re-escrow.
	/// </summary>
	Recovered = 1,

	/// <summary>
	/// The escrow has expired and is no longer valid.
	/// </summary>
	Expired = 2,

	/// <summary>
	/// The escrow has been manually revoked.
	/// </summary>
	Revoked = 3
}
