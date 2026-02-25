// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// An action being evaluated.
/// </summary>
/// <param name="Name"> Canonical action name (e.g., "Read", "Write", "Execute"). </param>
/// <param name="Attributes"> Optional action attributes. </param>
public sealed record AuthorizationAction(
	string Name,
	IReadOnlyDictionary<string, string>? Attributes);
