// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Internal implementation of the in-memory CDC builder.
/// </summary>
internal sealed class InMemoryCdcBuilder : IInMemoryCdcBuilder
{
	private readonly InMemoryCdcOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCdcBuilder"/> class.
	/// </summary>
	/// <param name="options">The in-memory CDC options to configure.</param>
	public InMemoryCdcBuilder(InMemoryCdcOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IInMemoryCdcBuilder ProcessorId(string processorId)
	{
		if (string.IsNullOrWhiteSpace(processorId))
		{
			throw new ArgumentException("Processor ID cannot be null or whitespace.", nameof(processorId));
		}

		_options.ProcessorId = processorId;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryCdcBuilder BatchSize(int size)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
		_options.BatchSize = size;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryCdcBuilder AutoFlush(bool autoFlush = true)
	{
		_options.AutoFlush = autoFlush;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryCdcBuilder PreserveHistory(bool preserveHistory = true)
	{
		_options.PreserveHistory = preserveHistory;
		return this;
	}
}
