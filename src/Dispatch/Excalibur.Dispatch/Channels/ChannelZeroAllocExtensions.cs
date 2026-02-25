// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Extension methods for zero-allocation patterns.
/// </summary>
public static class ChannelZeroAllocExtensions
{
	// The PublishAsync and PublishAsyncSlow methods have been temporarily commented out because MessagePump<T> does not currently have
	// TryWrite or TryWriteAsync methods. These extension methods will need to be updated once MessagePump<T> is refactored to include the
	// necessary methods.

	// Original methods:
	// - PublishAsync<T>(this MessagePump<T> pump, T message)
	// - PublishAsyncSlow<T>(MessagePump<T> pump, T message)

	// These methods provided zero-allocation publish patterns with SpinWait optimization for high-throughput scenarios. They should be
	// re-enabled once the MessagePump<T> type has been updated with the required TryWrite/TryWriteAsync methods.
}
