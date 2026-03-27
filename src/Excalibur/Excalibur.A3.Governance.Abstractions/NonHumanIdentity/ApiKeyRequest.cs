// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Request to create a new API key for a principal.
/// </summary>
/// <param name="PrincipalId">The principal the key is issued to.</param>
/// <param name="PrincipalType">The type of principal.</param>
/// <param name="Scopes">The scopes/permissions this key grants.</param>
/// <param name="ExpiresAt">Optional expiration. If <see langword="null"/>, <see cref="ApiKeyOptions.DefaultExpirationDays"/> is used.</param>
/// <param name="Description">Optional human-readable description.</param>
public sealed record ApiKeyRequest(
	string PrincipalId,
	PrincipalType PrincipalType,
	IReadOnlyList<string> Scopes,
	DateTimeOffset? ExpiresAt,
	string? Description);
