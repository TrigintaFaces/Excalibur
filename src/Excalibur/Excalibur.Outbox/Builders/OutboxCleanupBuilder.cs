// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Internal implementation of the outbox cleanup builder.
/// </summary>
internal sealed class OutboxCleanupBuilder : IOutboxCleanupBuilder
{
	private readonly OutboxConfiguration _config;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxCleanupBuilder"/> class.
	/// </summary>
	/// <param name="config">The outbox configuration to modify.</param>
	public OutboxCleanupBuilder(OutboxConfiguration config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc/>
	public IOutboxCleanupBuilder EnableAutoCleanup(bool enable = true)
	{
		_config.EnableAutomaticCleanup = enable;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxCleanupBuilder RetentionPeriod(TimeSpan period)
	{
		if (period <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(period), period, "Retention period must be positive.");
		}

		_config.MessageRetentionPeriod = period;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxCleanupBuilder CleanupInterval(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), interval, "Cleanup interval must be positive.");
		}

		_config.CleanupInterval = interval;
		return this;
	}
}
