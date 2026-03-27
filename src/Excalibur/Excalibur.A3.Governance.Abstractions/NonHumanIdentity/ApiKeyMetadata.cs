// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Metadata about an existing API key (never contains the plaintext key).
/// </summary>
/// <param name="KeyId">The unique key identifier.</param>
/// <param name="PrincipalId">The principal that owns this key.</param>
/// <param name="PrincipalType">The type of the owning principal.</param>
/// <param name="CreatedAt">When the key was created.</param>
/// <param name="ExpiresAt">When the key expires.</param>
/// <param name="RevokedAt">When the key was revoked, or <see langword="null"/> if active.</param>
/// <param name="Scopes">The scopes this key grants.</param>
/// <param name="Description">Optional description.</param>
public sealed record ApiKeyMetadata(
	string KeyId,
	string PrincipalId,
	PrincipalType PrincipalType,
	DateTimeOffset CreatedAt,
	DateTimeOffset ExpiresAt,
	DateTimeOffset? RevokedAt,
	IReadOnlyList<string> Scopes,
	string? Description);
