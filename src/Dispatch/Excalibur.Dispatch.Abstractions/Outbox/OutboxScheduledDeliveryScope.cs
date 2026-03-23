// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Outbox;

/// <summary>
/// Ambient scope for passing scheduled delivery time from
/// <see cref="OutboxWriterExtensions.WriteScheduledAsync"/> to <see cref="IOutboxWriter"/> implementations.
/// </summary>
/// <remarks>
/// This uses <see cref="AsyncLocal{T}"/> to flow the scheduled delivery time through
/// the async call chain without adding an extra parameter to <see cref="IOutboxWriter.WriteAsync"/>.
/// The value is set immediately before the call and cleared in a finally block.
/// </remarks>
internal static class OutboxScheduledDeliveryScope
{
	private static readonly AsyncLocal<DateTimeOffset?> ScopedValue = new();

	/// <summary>
	/// Gets or sets the scheduled delivery time for the current async scope.
	/// </summary>
	internal static DateTimeOffset? Current
	{
		get => ScopedValue.Value;
		set => ScopedValue.Value = value;
	}
}
