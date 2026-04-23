// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.DataProcessing.Requests;
using Excalibur.Dispatch.Abstractions.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Implements <see cref="IDataOrchestrationManager" /> for managing data tasks and delegating processing to registered processors.
/// </summary>
/// <remarks>
/// <para>
/// This class follows the <strong>connection-per-operation</strong> pattern: each database operation
/// (insert, select, update, delete) creates a fresh <see cref="IDbConnection"/> from the injected
/// <c>Func&lt;IDbConnection&gt;</c> factory and disposes it immediately after the operation completes.
/// This avoids holding long-lived connections during potentially slow processor runs and ensures
/// connections are returned to the pool promptly.
/// </para>
/// <para>
/// The connection factory is registered as a keyed singleton using
/// <see cref="DataProcessingKeys.OrchestrationConnection"/> and injected via
/// <c>[FromKeyedServices]</c>.
/// </para>
/// </remarks>
public sealed partial class DataOrchestrationManager : IDataOrchestrationManager
{
	private readonly Func<IDbConnection> _connectionFactory;

	private readonly IDataProcessorRegistry _processorRegistry;

	private readonly IServiceProvider _serviceProvider;

	private readonly IOptions<DataProcessingOptions> _configuration;

	private readonly ILogger<DataOrchestrationManager> _logger;

	/// <summary>
	/// Lazily resolved resilience policy for wrapping database operations with retry and
	/// circuit breaker logic. When <see cref="IDataAccessPolicyFactory"/> is registered
	/// (e.g., via <c>Excalibur.Data.SqlServer</c>), all DB calls are wrapped in its
	/// comprehensive policy. When not registered, DB calls execute directly.
	/// </summary>
	private volatile IAsyncPolicy? _resiliencePolicy;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataOrchestrationManager" /> class.
	/// </summary>
	/// <param name="connectionFactory"> A factory that creates database connections for data task operations. </param>
	/// <param name="processorRegistry"> A registry for resolving processors for record types. </param>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="configuration"> Configuration options for data processing. </param>
	/// <param name="logger"> Logger for logging messages and errors. </param>
	public DataOrchestrationManager(
		[FromKeyedServices(DataProcessingKeys.OrchestrationConnection)] Func<IDbConnection> connectionFactory,
		IDataProcessorRegistry processorRegistry,
		IServiceProvider serviceProvider,
		IOptions<DataProcessingOptions> configuration,
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

	/// <summary>
	/// Gets the resilience policy, lazily resolving from DI on first access.
	/// Returns <see cref="Policy.NoOpAsync"/> when no <see cref="IDataAccessPolicyFactory"/>
	/// is registered, allowing direct execution without retry/circuit breaker overhead.
	/// </summary>
	private IAsyncPolicy ResiliencePolicy
	{
		get
		{
			if (_resiliencePolicy is not null)
			{
				return _resiliencePolicy;
			}

			var factory = _serviceProvider.GetService<IDataAccessPolicyFactory>();
			var policy = factory?.GetComprehensivePolicy() ?? Policy.NoOpAsync();
			_resiliencePolicy = policy;
			return policy;
		}
	}

	/// <inheritdoc />
	public async Task<Guid> AddDataTaskForRecordTypeAsync(string recordType, CancellationToken cancellationToken)
	{
		var dataTaskId = Uuid7Extensions.GenerateGuid();

		await ResiliencePolicy.ExecuteAsync(async () =>
		{
			var req = new InsertDataTask(
				dataTaskId,
				recordType,
				_configuration.Value,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			using var connection = _connectionFactory();
			_ = await connection.Ready().ResolveAsync(req).ConfigureAwait(false);
		}).ConfigureAwait(false);

		return dataTaskId;
	}

	/// <inheritdoc />
	public async ValueTask ProcessDataTasksAsync(CancellationToken cancellationToken)
	{
		List<DataTaskRequest> requests = [];
		await ResiliencePolicy.ExecuteAsync(async () =>
		{
			var req = new SelectPendingDataTasks(
				_configuration.Value,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			using var connection = _connectionFactory();
			requests = (await connection.Ready().ResolveAsync(req).ConfigureAwait(false)).ToList();
		}).ConfigureAwait(false);

		if (requests.Count == 0)
		{
			return;
		}

		await ProcessRequestsAsync(requests, cancellationToken).ConfigureAwait(false);
	}

	private async Task ProcessRequestsAsync(IList<DataTaskRequest> dataTaskRequests, CancellationToken cancellationToken)
	{
		foreach (var request in dataTaskRequests)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!_processorRegistry.TryGetFactory(request.RecordType, out var factory))
			{
				await TryUpdateAttemptsAsync(request.DataTaskId, request.Attempts + 1, cancellationToken).ConfigureAwait(false);

				LogProcessorNotFound(request.RecordType);
				continue;
			}

			try
			{
				// The scope owns the processor lifetime — DisposeAsync is called
				// automatically when the scope is disposed via IAsyncDisposable.
				await using var scope = _serviceProvider.CreateAsyncScope();
				var dataProcessor = factory(scope.ServiceProvider);

				// Task-scoped CTS: signals the processor to abort when the underlying
				// task row disappears (e.g., after a database restore). The processor
				// receives the linked token and stops cleanly.
				using var taskScopedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

				_ = await dataProcessor.RunAsync(
					request.CompletedCount,
					(complete, ct) =>
						UpdateCompletedCountAsync(request.DataTaskId, complete, taskScopedCts, ct),
					taskScopedCts.Token).ConfigureAwait(false);

				await TryDeleteRequestAsync(request.DataTaskId, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				// Task-scoped cancellation: the task row was deleted or replaced
				// (typically after a database restore). Log and move to the next task.
				LogDataTaskStale(request.DataTaskId, request.RecordType);
			}
			catch (Exception ex)
			{
				await TryUpdateAttemptsAsync(request.DataTaskId, request.Attempts + 1, cancellationToken).ConfigureAwait(false);

				LogProcessingDataTaskError(request.RecordType, request.Attempts + 1, ex);

				// Continue processing remaining tasks instead of aborting the batch
			}
		}
	}

	/// <summary>
	/// Updates the attempt count for a data task. Failures are logged but do not propagate —
	/// the database may be unavailable during restore, and crashing the loop would
	/// prevent processing of remaining tasks.
	/// </summary>
	private async Task TryUpdateAttemptsAsync(Guid dataTaskId, int attempts, CancellationToken cancellationToken)
	{
		try
		{
			await ResiliencePolicy.ExecuteAsync(async () =>
			{
				var req = new UpdateDataTaskAttempts(
					dataTaskId,
					attempts,
					_configuration.Value,
					DbTimeouts.RegularTimeoutSeconds,
					cancellationToken);

				using var connection = _connectionFactory();
				_ = await connection.Ready().ResolveAsync(req).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Host shutdown — let it propagate naturally on next cancellation check
		}
		catch (Exception ex)
		{
			// Database unavailable — log but don't crash the processing loop.
			// The attempt count stays honest: it only increments when we can persist it.
			LogUpdateAttemptsFailed(dataTaskId, ex);
		}
	}

	/// <summary>
	/// Updates the completed count and signals the processor to abort if the task row
	/// no longer exists (0 rows affected). This detects database restores and manual
	/// deletions, preventing the processor from doing wasted work against orphaned state.
	/// </summary>
	private async Task UpdateCompletedCountAsync(
		Guid dataTaskId,
		long complete,
		CancellationTokenSource taskScopedCts,
		CancellationToken cancellationToken)
	{
		var affected = 0;
		await ResiliencePolicy.ExecuteAsync(async () =>
		{
			var req = new UpdateDataTaskCompletedCount(
				dataTaskId,
				complete,
				_configuration.Value,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			using var connection = _connectionFactory();
			affected = await connection.Ready().ResolveAsync(req).ConfigureAwait(false);
		}).ConfigureAwait(false);

		if (affected == 0)
		{
			LogUpdateCompletedCountMismatch(dataTaskId);

			// Signal the processor to stop — the task row no longer exists.
			// The consumer loop will see the cancellation and exit cleanly.
			await taskScopedCts.CancelAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Deletes a completed task row. Failures are logged but do not propagate —
	/// if the delete fails (DB unavailable or row already gone), the task will be
	/// re-selected on the next poll but the processor should handle it idempotently.
	/// </summary>
	private async Task TryDeleteRequestAsync(Guid dataTaskId, CancellationToken cancellationToken)
	{
		try
		{
			await ResiliencePolicy.ExecuteAsync(async () =>
			{
				var req = new DeleteDataTask(
					dataTaskId,
					_configuration.Value,
					DbTimeouts.RegularTimeoutSeconds,
					cancellationToken);

				using var connection = _connectionFactory();
				_ = await connection.Ready().ResolveAsync(req).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Host shutdown — let it propagate naturally
		}
		catch (Exception ex)
		{
			LogDeleteTaskFailed(dataTaskId, ex);
		}
	}
}
