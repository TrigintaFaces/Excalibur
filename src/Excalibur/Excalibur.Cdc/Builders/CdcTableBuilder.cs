// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Internal implementation of the CDC table builder.
/// </summary>
internal sealed class CdcTableBuilder : ICdcTableBuilder
{
	private readonly CdcTableTrackingOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcTableBuilder"/> class.
	/// </summary>
	/// <param name="options">The table tracking options to configure.</param>
	public CdcTableBuilder(CdcTableTrackingOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public ICdcTableBuilder MapInsert<TEvent>() where TEvent : class
	{
		_options.EventMappings[CdcChangeType.Insert] = typeof(TEvent);
		return this;
	}

	/// <inheritdoc/>
	public ICdcTableBuilder MapUpdate<TEvent>() where TEvent : class
	{
		_options.EventMappings[CdcChangeType.Update] = typeof(TEvent);
		return this;
	}

	/// <inheritdoc/>
	public ICdcTableBuilder MapDelete<TEvent>() where TEvent : class
	{
		_options.EventMappings[CdcChangeType.Delete] = typeof(TEvent);
		return this;
	}

	/// <inheritdoc/>
	public ICdcTableBuilder MapAll<TEvent>() where TEvent : class
	{
		var eventType = typeof(TEvent);
		_options.EventMappings[CdcChangeType.Insert] = eventType;
		_options.EventMappings[CdcChangeType.Update] = eventType;
		_options.EventMappings[CdcChangeType.Delete] = eventType;
		return this;
	}

	/// <inheritdoc/>
	public ICdcTableBuilder WithFilter(Func<CdcDataChange, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		_options.Filter = predicate;
		return this;
	}

	/// <inheritdoc/>
	public ICdcTableBuilder CaptureInstance(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("Capture instance name cannot be null or whitespace.", nameof(name));
		}

		_options.CaptureInstance = name;
		return this;
	}
}
