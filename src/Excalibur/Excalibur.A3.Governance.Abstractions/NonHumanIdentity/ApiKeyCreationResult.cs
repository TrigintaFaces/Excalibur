// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Result of creating or rotating an API key. Contains the plaintext key
/// which is only returned once -- subsequent access returns metadata only.
/// </summary>
/// <param name="KeyId">The unique identifier for the key.</param>
/// <param name="ApiKey">The plaintext API key (returned only at creation/rotation).</param>
/// <param name="ExpiresAt">When the key expires.</param>
public sealed record ApiKeyCreationResult(
	string KeyId,
	string ApiKey,
	DateTimeOffset ExpiresAt);
