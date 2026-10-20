// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>Immutable payload identifying one retryable atomic revocation operation.</summary>
public sealed record GrantRevocationRequest
{
	/// <summary>Initializes a validated revocation request.</summary>
	/// <param name="operationId">Stable operation identifier reused for retries.</param>
	/// <param name="userId">Exact subject identifier.</param>
	/// <param name="scope">Explicit revocation scope.</param>
	/// <param name="revokedBy">Actor initiating the operation.</param>
	public GrantRevocationRequest(Guid operationId, string userId, GrantRevocationScope scope, string revokedBy)
	{
		if (operationId == Guid.Empty)
		{
			throw new ArgumentException("A stable operation identifier is required.", nameof(operationId));
		}
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);
		if (!string.Equals(userId, userId.Trim(), StringComparison.Ordinal))
		{
			throw new ArgumentException("Identifiers must not contain leading or trailing whitespace.", nameof(userId));
		}
		ArgumentNullException.ThrowIfNull(scope);
		ArgumentException.ThrowIfNullOrWhiteSpace(revokedBy);
		OperationId = operationId;
		UserId = userId;
		Scope = scope;
		RevokedBy = revokedBy;
	}
	/// <summary>Gets the stable operation identifier.</summary>
	public Guid OperationId { get; }
	/// <summary>Gets the exact subject identifier.</summary>
	public string UserId { get; }
	/// <summary>Gets the explicit scope.</summary>
	public GrantRevocationScope Scope { get; }
	/// <summary>Gets the initiating actor.</summary>
	public string RevokedBy { get; }
}
