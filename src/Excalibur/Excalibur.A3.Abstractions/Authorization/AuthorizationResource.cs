// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// A resource targeted by an action.
/// </summary>
/// <param name="Type"> Logical type of the resource (e.g., "Order"). </param>
/// <param name="Id"> Stable identifier of the resource instance. </param>
/// <param name="Attributes"> Optional resource attributes (e.g., owner, labels). </param>
public sealed record AuthorizationResource(
	string Type,
	string Id,
	IReadOnlyDictionary<string, string>? Attributes);
