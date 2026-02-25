// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authentication;

/// <summary>
/// Represents the outcome of token validation.
/// </summary>
/// <param name="Succeeded">True when validation succeeded.</param>
/// <param name="Principal">The authenticated principal, when <paramref name="Succeeded"/> is true.</param>
/// <param name="FailureReason">Optional failure reason.</param>
public sealed record AuthenticationResult(bool Succeeded, AuthenticatedPrincipal? Principal, string? FailureReason = null);
