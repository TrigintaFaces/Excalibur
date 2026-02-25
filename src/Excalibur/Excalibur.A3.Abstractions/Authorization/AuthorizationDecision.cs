// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// The decision returned by an <see cref="IAuthorizationEvaluator"/>.
/// </summary>
/// <param name="Effect">Permit, Deny, or Indeterminate.</param>
/// <param name="Reason">Optional human-readable reason.</param>
public sealed record AuthorizationDecision(AuthorizationEffect Effect, string? Reason = null);
