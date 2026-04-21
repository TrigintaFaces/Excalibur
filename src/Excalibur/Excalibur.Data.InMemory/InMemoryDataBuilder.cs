// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.InMemory;

/// <summary>
/// Internal implementation of the in-memory data builder.
/// </summary>
internal sealed class InMemoryDataBuilder : IInMemoryDataBuilder
{
	private readonly InMemoryProviderOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryDataBuilder"/> class.
	/// </summary>
	/// <param name="options">The in-memory provider options to configure.</param>
	public InMemoryDataBuilder(InMemoryProviderOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IInMemoryDataBuilder MaxItemsPerCollection(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
		_options.MaxItemsPerCollection = count;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryDataBuilder EnableDetailedLogging(bool enable = true)
	{
		_options.EnableDetailedLogging = enable;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryDataBuilder EnableMetrics(bool enable = true)
	{
		_options.EnableMetrics = enable;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryDataBuilder PersistToDisk(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		_options.PersistToDisk = true;
		_options.PersistenceFilePath = filePath;
		return this;
	}

	/// <inheritdoc/>
	public IInMemoryDataBuilder ReadOnly(bool readOnly = true)
	{
		_options.IsReadOnly = readOnly;
		return this;
	}
}
