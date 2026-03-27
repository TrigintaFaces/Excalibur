// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Result of validating an API key.
/// </summary>
/// <param name="IsValid">Whether the key is valid.</param>
/// <param name="KeyId">The key identifier, if found.</param>
/// <param name="PrincipalId">The principal that owns the key, if valid.</param>
/// <param name="PrincipalType">The type of the owning principal, if valid.</param>
/// <param name="Scopes">The scopes of the key, if valid.</param>
/// <param name="FailureReason">Reason for validation failure, if not valid.</param>
public sealed record ApiKeyValidationResult(
	bool IsValid,
	string? KeyId,
	string? PrincipalId,
	PrincipalType? PrincipalType,
	IReadOnlyList<string>? Scopes,
	string? FailureReason);
