// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// A subject (actor) for authorization decisions.
/// </summary>
/// <param name="ActorId"> Stable identifier for the actor (user/service). </param>
/// <param name="TenantId"> Optional tenant identifier for multi-tenant scenarios. </param>
/// <param name="Attributes"> Optional subject attributes (e.g., roles, groups, claims). </param>
public sealed record AuthorizationSubject(
	string ActorId,
	string? TenantId,
	IReadOnlyDictionary<string, string>? Attributes);
