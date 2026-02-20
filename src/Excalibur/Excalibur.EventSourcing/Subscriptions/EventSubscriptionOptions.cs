// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Represents the starting position for an event subscription.
/// </summary>
public enum SubscriptionStartPosition
{
	/// <summary>
	/// Start from the beginning of the stream.
	/// </summary>
	Beginning,

	/// <summary>
	/// Start from the current end of the stream (new events only).
	/// </summary>
	End,

	/// <summary>
	/// Start from a specific position.
	/// </summary>
	Position
}

/// <summary>
/// Configuration options for event subscriptions.
/// </summary>
public sealed class EventSubscriptionOptions
{
	/// <summary>
	/// Gets or sets the starting position for the subscription.
	/// </summary>
	/// <value>The start position. Default is <see cref="SubscriptionStartPosition.End"/>.</value>
	public SubscriptionStartPosition StartPosition { get; set; } = SubscriptionStartPosition.End;

	/// <summary>
	/// Gets or sets the specific position to start from when <see cref="StartPosition"/> is <see cref="SubscriptionStartPosition.Position"/>.
	/// </summary>
	/// <value>The start position value. Default is 0.</value>
	public long StartPositionValue { get; set; }

	/// <summary>
	/// Gets or sets the buffer size for event batching.
	/// </summary>
	/// <value>The buffer size. Default is 100.</value>
	[Range(1, 10000)]
	public int BufferSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum batch size for event delivery.
	/// </summary>
	/// <value>The maximum batch size. Default is 50.</value>
	[Range(1, 10000)]
	public int MaxBatchSize { get; set; } = 50;

	/// <summary>
	/// Gets or sets the polling interval for checking new events.
	/// </summary>
	/// <value>The polling interval. Default is 1 second.</value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);
}
