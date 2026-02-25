// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Represents session state for Service Bus sessions.
/// </summary>
public sealed record ServiceBusSessionState(
	string SessionId,
	byte[]? State,
	DateTimeOffset LastAccessTime,
	Dictionary<string, object>? CustomProperties = null);
