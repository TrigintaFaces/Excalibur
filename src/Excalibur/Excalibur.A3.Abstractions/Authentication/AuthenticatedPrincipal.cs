// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authentication;

/// <summary>
/// Provider-neutral representation of an authenticated principal.
/// </summary>
/// <param name="SubjectId"> Stable subject identifier. </param>
/// <param name="TenantId"> Optional tenant identifier. </param>
/// <param name="Claims"> Optional claims as key/value pairs. </param>
public sealed record AuthenticatedPrincipal(string SubjectId, string? TenantId, IReadOnlyDictionary<string, string>? Claims);
