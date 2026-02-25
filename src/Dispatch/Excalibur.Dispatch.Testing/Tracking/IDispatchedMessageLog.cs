// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Testing.Tracking;

/// <summary>
/// Read-only log of messages dispatched through the test harness.
/// Provides querying and filtering capabilities for test assertions.
/// </summary>
public interface IDispatchedMessageLog
{
	/// <summary>
	/// Gets all dispatched messages in chronological order.
	/// </summary>
	IReadOnlyList<DispatchedMessage> All { get; }

	/// <summary>
	/// Gets all dispatched messages of a specific message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type to filter by.</typeparam>
	/// <returns>Messages matching the specified type.</returns>
	IReadOnlyList<DispatchedMessage> Select<TMessage>() where TMessage : IDispatchMessage;

	/// <summary>
	/// Gets whether any message of the specified type was dispatched.
	/// </summary>
	/// <typeparam name="TMessage">The message type to check for.</typeparam>
	/// <returns><see langword="true"/> if at least one message of the specified type was dispatched.</returns>
	bool Any<TMessage>() where TMessage : IDispatchMessage;

	/// <summary>
	/// Gets the total number of dispatched messages.
	/// </summary>
	int Count { get; }

	/// <summary>
	/// Clears all recorded messages.
	/// </summary>
	void Clear();
}
