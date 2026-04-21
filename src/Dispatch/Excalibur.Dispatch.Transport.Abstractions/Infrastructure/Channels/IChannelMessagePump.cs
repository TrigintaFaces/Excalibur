// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines the contract for a message pump that processes messages from channels.
/// </summary>
public interface IChannelMessagePump : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether the pump is currently running.
	/// </summary>
	/// <value>
	/// A value indicating whether the pump is currently running.
	/// </value>
	bool IsRunning { get; }

	/// <summary>
	/// Starts the message pump.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops the message pump.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StopAsync(CancellationToken cancellationToken);
}
