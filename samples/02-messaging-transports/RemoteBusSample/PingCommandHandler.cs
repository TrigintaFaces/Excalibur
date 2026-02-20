// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

namespace RemoteBusSample;

public sealed class PingCommandHandler : IActionHandler<PingCommand, string>
{
	public Task<string> HandleAsync(PingCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		Console.WriteLine($"Handling command: {action.Text}");
		return Task.FromResult($"Pong {action.Text}");
	}
}
