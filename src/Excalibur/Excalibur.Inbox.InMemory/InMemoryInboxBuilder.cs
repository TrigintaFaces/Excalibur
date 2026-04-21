// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Inbox.InMemory;

/// <summary>
/// Internal implementation of the in-memory inbox builder.
/// </summary>
internal sealed class InMemoryInboxBuilder : IInMemoryInboxBuilder
{
	private readonly InMemoryInboxOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryInboxBuilder"/> class.
	/// </summary>
	/// <param name="options">The in-memory inbox options to configure.</param>
	public InMemoryInboxBuilder(InMemoryInboxOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IInMemoryInboxBuilder MaxEntries(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		_options.MaxEntries = count;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryInboxBuilder RetentionPeriod(TimeSpan period)
	{
		if (period <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(period), period, "Retention period must be positive.");
		}

		_options.RetentionPeriod = period;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryInboxBuilder EnableAutomaticCleanup(bool enable = true)
	{
		_options.EnableAutomaticCleanup = enable;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryInboxBuilder CleanupInterval(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), interval, "Cleanup interval must be positive.");
		}

		_options.CleanupInterval = interval;
		return this;
	}
}
