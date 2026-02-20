// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

namespace MultiBusSample;

/// <summary>
/// Handles <see cref="KafkaPingEvent" /> messages.
/// </summary>
public sealed class KafkaPingHandler : IEventHandler<KafkaPingEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(KafkaPingEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);
		Console.WriteLine($"Kafka received: {eventMessage.Text}");
		return Task.CompletedTask;
	}
}
