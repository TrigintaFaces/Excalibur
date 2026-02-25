// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;

namespace WebWorkerSample.Contracts;

/// <summary>
/// Command used to demonstrate message routing between hosts.
/// </summary>
[Activity("Ping", "Returns a pong message")]
public sealed class PingCommand : CommandBase<string>
{
	/// <summary>
	/// Gets or sets the ping text.
	/// </summary>
	public string Text { get; init; } = string.Empty;
}
