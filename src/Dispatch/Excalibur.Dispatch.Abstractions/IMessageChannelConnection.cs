// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Defines the connection lifecycle contract for a message channel.
/// </summary>
public interface IMessageChannelConnection
{
	/// <summary>
	/// Gets the channel name or identifier.
	/// </summary>
	/// <value> The logical identifier of the channel. </value>
	string ChannelName { get; }

	/// <summary>
	/// Gets a value indicating whether the adapter is currently connected.
	/// </summary>
	/// <value> <see langword="true" /> when the adapter maintains an active connection; otherwise, <see langword="false" />. </value>
	bool IsConnected { get; }

	/// <summary>
	/// Connects to the channel.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous connect operation. </returns>
	Task ConnectAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Disconnects from the channel.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task that represents the asynchronous disconnect operation. </returns>
	Task DisconnectAsync(CancellationToken cancellationToken);
}
