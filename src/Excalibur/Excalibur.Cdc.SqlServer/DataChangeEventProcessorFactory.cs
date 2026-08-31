// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Factory class for creating instances of <see cref="DataChangeEventProcessor" />.
/// </summary>
public sealed class DataChangeEventProcessorFactory : IDataChangeEventProcessorFactory
{
	private readonly IServiceProvider _serviceProvider;

	private readonly IDataAccessPolicyFactory _policyFactory;

	private readonly IHostApplicationLifetime _appLifetime;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataChangeEventProcessorFactory" /> class.
	/// </summary>
	/// <param name="serviceProvider"> The service provider used for resolving dependencies. </param>
	/// <param name="appLifetime">
	/// An instance of <see cref="IHostApplicationLifetime" /> that allows the application to perform actions during the application's
	/// lifecycle events, such as startup, shutdown, or when the application is stopping. This parameter is used to gracefully manage
	/// tasks that need to respond to application lifecycle events.
	/// </param>
	/// <param name="policyFactory"> The factory for creating data access policies. </param>
	public DataChangeEventProcessorFactory(
		IServiceProvider serviceProvider,
		IHostApplicationLifetime appLifetime,
		IDataAccessPolicyFactory policyFactory)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(policyFactory);

		_serviceProvider = serviceProvider;
		_appLifetime = appLifetime;
		_policyFactory = policyFactory;
	}

	/// <summary>
	/// Creates an instance of <see cref="DataChangeEventProcessor" />.
	/// </summary>
	/// <param name="dbConfig"> The database configuration used for CDC processing. </param>
	/// <param name="cdcRepository"> The CDC repository for querying change data. </param>
	/// <param name="stateStoreConnectionFactory"> Supplies a connection per CDC-state operation. </param>
	/// <returns> A configured <see cref="IDataChangeEventProcessor" /> instance. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="dbConfig" />, <paramref name="cdcRepository" />, or <paramref name="stateStoreConnectionFactory" /> is <c>
	/// null </c>.
	/// </exception>
	public IDataChangeEventProcessor Create(IDatabaseOptions dbConfig, CdcRepository cdcRepository, Func<IDbConnection> stateStoreConnectionFactory)
	{
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcRepository);
		ArgumentNullException.ThrowIfNull(stateStoreConnectionFactory);

		var logger = _serviceProvider.GetRequiredService<ILogger<DataChangeEventProcessor>>();
		var timeProvider = _serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
		var stateStoreOptions = _serviceProvider.GetService<IOptions<SqlServerCdcStateStoreOptions>>();
		var fatalErrorOptions = _serviceProvider.GetService<IOptions<CdcFatalErrorOptions<DataChangeEvent>>>();
		var idempotencyFilter = _serviceProvider.GetService<ICdcIdempotencyFilter>();

		return new DataChangeEventProcessor(
				_appLifetime,
				dbConfig,
				cdcRepository,
				stateStoreConnectionFactory,
				stateStoreOptions,
				_serviceProvider,
				_policyFactory,
				timeProvider,
				logger,
				fatalErrorOptions,
				idempotencyFilter);
	}
}
