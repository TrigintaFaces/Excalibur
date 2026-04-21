// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.DataProcessing.Processing;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Internal implementation of the data processing builder.
/// </summary>
internal sealed class DataProcessingBuilder : IDataProcessingBuilder
{
	internal DataProcessingBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
	}

	internal IServiceCollection Services { get; }

	internal Func<IDbConnection>? SimpleConnectionFactory { get; private set; }
	internal Func<IServiceProvider, Func<IDbConnection>>? DependencyAwareConnectionFactory { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal bool BackgroundProcessingEnabled { get; private set; }
	internal Action<DataProcessingHostedServiceOptions>? BackgroundProcessingConfigure { get; private set; }

	/// <inheritdoc/>
	public IDataProcessingBuilder ConnectionFactory(Func<IDbConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		SimpleConnectionFactory = connectionFactory;
		DependencyAwareConnectionFactory = null;
		return this;
	}

	/// <inheritdoc/>
	public IDataProcessingBuilder ConnectionFactory(Func<IServiceProvider, Func<IDbConnection>> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		DependencyAwareConnectionFactory = connectionFactory;
		SimpleConnectionFactory = null;
		return this;
	}

	/// <inheritdoc/>
	public IDataProcessingBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		return this;
	}

	/// <inheritdoc/>
	public IDataProcessingBuilder AddProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>()
		where TProcessor : class, IDataProcessor
	{
		Services.AddScoped<TProcessor>();
		Services.AddScoped<IDataProcessor, TProcessor>();
		return this;
	}

	/// <inheritdoc/>
	public IDataProcessingBuilder AddRecordHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TRecord>()
		where THandler : class, IRecordHandler<TRecord>
	{
		Services.AddScoped<IRecordHandler<TRecord>, THandler>();
		DataProcessingServiceCollectionExtensions.RecordHandlerFactories.TryAdd(
			typeof(TRecord), sp => sp.GetRequiredService<THandler>());
		return this;
	}

	/// <inheritdoc/>
	public IDataProcessingBuilder EnableBackgroundProcessing(
		Action<DataProcessingHostedServiceOptions>? configure = null)
	{
		BackgroundProcessingEnabled = true;
		BackgroundProcessingConfigure = configure;
		return this;
	}
}
