// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Cdc;

/// <summary>
/// Internal implementation of the CDC builder.
/// </summary>
internal sealed class CdcBuilder : ICdcBuilder
{
	private readonly CdcOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The CDC options to configure.</param>
	public CdcBuilder(IServiceCollection services, CdcOptions options)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IServiceCollection Services { get; }

	/// <inheritdoc/>
	public ICdcBuilder TrackTable(string tableName, Action<ICdcTableBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(configure);

		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentException("Table name cannot be empty or whitespace.", nameof(tableName));
		}

		var tableOptions = new CdcTableTrackingOptions { TableName = tableName };
		var builder = new CdcTableBuilder(tableOptions);
		configure(builder);

		_options.TrackedTables.Add(tableOptions);

		return this;
	}

	/// <inheritdoc/>
	public ICdcBuilder TrackTable<TEntity>(Action<ICdcTableBuilder>? configure = null) where TEntity : class
	{
		// Infer table name from entity type (simple convention: pluralize type name)
		var tableName = $"dbo.{typeof(TEntity).Name}s";

		if (configure is null)
		{
			var tableOptions = new CdcTableTrackingOptions { TableName = tableName };
			_options.TrackedTables.Add(tableOptions);
		}
		else
		{
			return TrackTable(tableName, configure);
		}

		return this;
	}

	/// <inheritdoc/>
	public ICdcBuilder WithRecovery(Action<ICdcRecoveryBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new CdcRecoveryBuilder(_options);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public ICdcBuilder EnableBackgroundProcessing(bool enable = true)
	{
		_options.EnableBackgroundProcessing = enable;
		return this;
	}
}
