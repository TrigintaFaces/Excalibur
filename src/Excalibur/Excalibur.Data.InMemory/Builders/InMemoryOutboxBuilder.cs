// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Outbox;

namespace Excalibur.Data.InMemory;

/// <summary>
/// Internal implementation of the in-memory outbox builder.
/// </summary>
internal sealed class InMemoryOutboxBuilder : IInMemoryOutboxBuilder
{
	private readonly InMemoryOutboxOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryOutboxBuilder"/> class.
	/// </summary>
	/// <param name="options">The in-memory outbox options to configure.</param>
	public InMemoryOutboxBuilder(InMemoryOutboxOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IInMemoryOutboxBuilder MaxMessages(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		_options.MaxMessages = count;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryOutboxBuilder RetentionPeriod(TimeSpan period)
	{
		if (period <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(period), period, "Retention period must be positive.");
		}

		_options.DefaultRetentionPeriod = period;
		return this;
	}
}
