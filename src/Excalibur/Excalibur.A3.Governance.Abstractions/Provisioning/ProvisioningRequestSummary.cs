// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>
/// Represents the persisted summary of a provisioning request for store operations.
/// </summary>
/// <param name="RequestId">Unique identifier for the request.</param>
/// <param name="UserId">The user requesting the grant.</param>
/// <param name="GrantScope">The scope/qualifier of the requested grant.</param>
/// <param name="GrantType">The type of the requested grant.</param>
/// <param name="Status">The current status of the request.</param>
/// <param name="IdempotencyKey">Unique key for duplicate prevention.</param>
/// <param name="RiskScore">The assessed risk score (0-100).</param>
/// <param name="RequestedBy">The identity that submitted the request.</param>
/// <param name="CreatedAt">When the request was created.</param>
/// <param name="ApprovalSteps">The approval steps associated with this request.</param>
/// <param name="TenantId">Optional tenant scope for the grant.</param>
/// <param name="RequestedExpiry">Optional JIT expiry time for temporary access.</param>
public sealed record ProvisioningRequestSummary(
	string RequestId,
	string UserId,
	string GrantScope,
	string GrantType,
	ProvisioningRequestStatus Status,
	string IdempotencyKey,
	int RiskScore,
	string RequestedBy,
	DateTimeOffset CreatedAt,
	IReadOnlyList<ApprovalStep> ApprovalSteps,
	string? TenantId = null,
	DateTimeOffset? RequestedExpiry = null);
