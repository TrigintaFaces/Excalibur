// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Configuration options for the outbox pattern.
/// </summary>
public sealed class OutboxOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable the outbox pattern.
	/// </summary>
	/// <value>The current <see cref="Enabled"/> value.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum messages to process per batch.
	/// </summary>
	/// <value> Default is 100. </value>
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the interval in milliseconds between publishing operations.
	/// </summary>
	/// <value> Default is 1000ms (1 second). </value>
	public int PublishIntervalMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <value> Default is 3. </value>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the maximum retention for sent messages.
	/// </summary>
	/// <value>
	/// The maximum retention for sent messages.
	/// </value>
	public TimeSpan SentMessageRetention { get; set; } = TimeSpan.FromDays(1);

	/// <summary>
	/// Gets or sets a value indicating whether to use in-memory storage (light mode).
	/// </summary>
	/// <value>The current <see cref="UseInMemoryStorage"/> value.</value>
	public bool UseInMemoryStorage { get; set; }
}
