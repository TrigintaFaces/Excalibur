// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.DataProcessing.Requests;
using Excalibur.Dispatch.Abstractions.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Implements <see cref="IDataOrchestrationManager" /> for managing data tasks and delegating processing to registered processors.
/// </summary>
public sealed partial class DataOrchestrationManager : IDataOrchestrationManager
{
	private readonly Func<IDbConnection> _connectionFactory;

	private readonly IDataProcessorRegistry _processorRegistry;

	private readonly IServiceProvider _serviceProvider;

	private readonly IOptions<DataProcessingConfiguration> _configuration;

	private readonly ILogger<DataOrchestrationManager> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataOrchestrationManager" /> class.
	/// </summary>
	/// <param name="connectionFactory"> A factory that creates database connections for data task operations. </param>
	/// <param name="processorRegistry"> A registry for resolving processors for record types. </param>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="configuration"> Configuration options for data processing. </param>
	/// <param name="logger"> Logger for logging messages and errors. </param>
	public DataOrchestrationManager(
		Func<IDbConnection> connectionFactory,
		IDataProcessorRegistry processorRegistry,
		IServiceProvider serviceProvider,
		IOptions<DataProcessingConfiguration> configuration,
		ILogger<DataOrchestrationManager> logger)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(processorRegistry);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
		_processorRegistry = processorRegistry;
		_serviceProvider = serviceProvider;
		_configuration = configuration;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<Guid> AddDataTaskForRecordTypeAsync(string recordType, CancellationToken cancellationToken)
	{
		var dataTaskId = Uuid7Extensions.GenerateGuid();
		var req = new InsertDataTask(
			dataTaskId,
			recordType,
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		using var connection = _connectionFactory();
		_ = await connection.Ready().ResolveAsync(req).ConfigureAwait(false);

		return dataTaskId;
	}

	/// <inheritdoc />
	public async ValueTask ProcessDataTasksAsync(CancellationToken cancellationToken)
	{
		var req = new SelectPendingDataTasks(
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		List<DataTaskRequest> requests;
		using (var connection = _connectionFactory())
		{
			requests = (await connection.Ready().ResolveAsync(req).ConfigureAwait(false)).ToList();
		}

		if (requests.Count == 0)
		{
			await ValueTask.CompletedTask.ConfigureAwait(false);
			return;
		}

		await new ValueTask(ProcessRequestsAsync(requests, cancellationToken)).ConfigureAwait(false);
	}

	private async Task ProcessRequestsAsync(IList<DataTaskRequest> dataTaskRequests, CancellationToken cancellationToken)
	{
		foreach (var request in dataTaskRequests)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!_processorRegistry.TryGetFactory(request.RecordType, out var factory))
			{
				request.Attempts++;
				await UpdateAttemptsAsync(request.DataTaskId, request.Attempts, cancellationToken).ConfigureAwait(false);

				LogProcessorNotFound(request.RecordType);
			}
			else
			{
				try
				{
					using var scope = _serviceProvider.CreateScope();
					var dataProcessor = factory(scope.ServiceProvider);
					_ = await dataProcessor.RunAsync(
						request.CompletedCount,
						(complete, cancellation) =>
							UpdateCompletedCountAsync(request.DataTaskId, complete, cancellation),
						cancellationToken).ConfigureAwait(false);

					await dataProcessor.DisposeAsync().ConfigureAwait(false);

					await DeleteRequestAsync(request.DataTaskId, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					request.Attempts++;
					await UpdateAttemptsAsync(request.DataTaskId, request.Attempts, cancellationToken).ConfigureAwait(false);

					LogProcessingDataTaskError(request.RecordType, request.Attempts, ex);

					throw;
				}
			}
		}
	}

	private async Task UpdateAttemptsAsync(Guid dataTaskId, int attempts, CancellationToken cancellationToken)
	{
		var req = new UpdateDataTaskAttempts(
			dataTaskId,
			attempts,
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		using var connection = _connectionFactory();
		_ = await connection.Ready().ResolveAsync(req).ConfigureAwait(false);
	}

	private async Task UpdateCompletedCountAsync(Guid dataTaskId, long complete, CancellationToken cancellationToken)
	{
		var req = new UpdateDataTaskCompletedCount(
			dataTaskId,
			complete,
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		using var connection = _connectionFactory();
		var affected = await connection.Ready().ResolveAsync(req).ConfigureAwait(false);
		if (affected == 0)
		{
			LogUpdateCompletedCountMismatch(dataTaskId);
		}
	}

	private async Task DeleteRequestAsync(Guid dataTaskId, CancellationToken cancellationToken)
	{
		var req = new DeleteDataTask(
			dataTaskId,
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		using var connection = _connectionFactory();
		_ = await connection.Ready().ResolveAsync(req).ConfigureAwait(false);
	}
}
