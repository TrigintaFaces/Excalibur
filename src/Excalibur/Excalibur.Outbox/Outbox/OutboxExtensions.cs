// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Extension methods for outbox configuration and operations. Provides helper methods for configuring outbox behavior and processing options.
/// </summary>
public static class OutboxExtensions
{
	/// <summary>
	/// Gets the message batch size for outbox processing operations. Determines how many messages are processed together in a single batch
	/// for optimal performance and resource utilization.
	/// </summary>
	/// <param name="options"> The outbox options configuration. </param>
	/// <returns> The number of messages to process in each batch (default: 100). </returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Extension method parameter required for public API, reserved for future configuration options")]
	public static int MessageBatchSize(this OutboxOptions options) =>

		// Default batch size
		100;
}
