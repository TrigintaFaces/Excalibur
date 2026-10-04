// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;
using System.Transactions;

using Excalibur.Data.DataProcessing.Requests;
using Excalibur.Dispatch.Messaging;

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
/// Enqueue and discovery use fresh connections. Each claimed task owns one SQL Server session
/// until processing and cleanup finish. Claims disable pooling so uncertain lock acknowledgments cannot
/// leave a lock on a pooled session. Session application locks prevent competing workers from
/// updating the same task; every owned write verifies the lock on that same session. The connection
/// factory must return a new, closed connection. Handler effects remain at least once.
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
	/// (e.g., via <c>Excalibur.Data.SqlServer</c>), enqueue and discovery use its
	/// comprehensive policy. Owned writes do not reopen or retry on another session.
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
		cancellationToken.ThrowIfCancellationRequested();
		if (Transaction.Current is not null)
		{
			throw new InvalidOperationException("Drain data tasks outside an ambient transaction; enqueue may participate in a business transaction.");
		}
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

		cancellationToken.ThrowIfCancellationRequested();

		if (requests.Count == 0)
		{
			return;
		}

		await ProcessRequestsAsync(requests, cancellationToken).ConfigureAwait(false);
	}

	private async Task ProcessRequestsAsync(IList<DataTaskRequest> requests, CancellationToken cancellationToken)
	{
		List<Exception>? failures = null;
		foreach (var candidate in requests)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Exception? taskFailure = null;
			try
			{
				await using var claim = await SqlDataTaskClaim.TryAcquireAsync(
					_connectionFactory, candidate.DataTaskId, _configuration.Value, cancellationToken).ConfigureAwait(false);
				if (claim is null)
				{
					continue;
				}
				try
				{
					// Discovery was only a hint. Another worker may have completed or changed it.
					var request = await claim.ReadEligibleAsync(cancellationToken).ConfigureAwait(false);
					if (request is not null)
					{
						await ProcessClaimedRequestAsync(claim, request, cancellationToken).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					taskFailure = ex;
					throw;
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				(failures ??= []).Add(taskFailure is not null && !ReferenceEquals(taskFailure, ex)
					? new AggregateException("Data task processing and ownership cleanup both failed.", taskFailure, ex)
					: ex);
			}
		}
		cancellationToken.ThrowIfCancellationRequested();
		if (failures is not null)
		{
			throw new AggregateException("One or more data tasks failed.", failures);
		}
	}

	private async Task ProcessClaimedRequestAsync(SqlDataTaskClaim claim, DataTaskRequest request, CancellationToken cancellationToken)
	{
		using var stale = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		using var deadlineStop = new CancellationTokenSource();
		using var processing = CancellationTokenSource.CreateLinkedTokenSource(stale.Token);
		var deadline = CancelAtDeadlineAsync(processing, request, deadlineStop.Token);
		try
		{
			if (!_processorRegistry.TryGetFactory(request.RecordType, out var factory))
			{
				LogProcessorNotFound(request.RecordType);
				throw new InvalidOperationException($"No data processor registered for record type '{request.RecordType}'.");
			}
			await using (var scope = _serviceProvider.CreateAsyncScope())
			{
				var processor = factory(scope.ServiceProvider);
				_ = await processor.RunAsync(request.CompletedCount, request.ProcessedCursor,
					async (count, cursor, token) =>
					{
						if (await claim.CheckpointAsync(count, cursor, token).ConfigureAwait(false) == 0)
						{
							LogUpdateCompletedCountMismatch(request.DataTaskId);
							await stale.CancelAsync().ConfigureAwait(false);
							stale.Token.ThrowIfCancellationRequested();
						}
					}, processing.Token).ConfigureAwait(false);
			}
			await deadlineStop.CancelAsync().ConfigureAwait(false);
			await deadline.ConfigureAwait(false);
			processing.Token.ThrowIfCancellationRequested();
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException) when (stale.IsCancellationRequested)
		{
			LogDataTaskStale(request.DataTaskId, request.RecordType);
			return;
		}
		catch (Exception ex)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var failure = processing.IsCancellationRequested
				? new TimeoutException($"Data task '{request.DataTaskId}' exceeded its configured processing timeout.", ex)
				: ex;
			try
			{
				await claim.UpdateAttemptsAsync(checked(request.Attempts + 1), cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
			catch (Exception updateError) { LogUpdateAttemptsFailed(request.DataTaskId, updateError); }
			LogProcessingDataTaskError(request.RecordType, request.Attempts + 1, failure);
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
			throw;
		}

		finally
		{
			await deadlineStop.CancelAsync().ConfigureAwait(false);
			await deadline.ConfigureAwait(false);
		}

		// Completed work is retryable if cleanup fails; do not charge another processing failure.
		cancellationToken.ThrowIfCancellationRequested();
		try { await claim.DeleteAsync(cancellationToken).ConfigureAwait(false); }
		catch (Exception ex) { LogDeleteTaskFailed(request.DataTaskId, ex); throw; }
	}

	private async Task CancelAtDeadlineAsync(CancellationTokenSource processing, DataTaskRequest request, CancellationToken stop)
	{
		try
		{
			await Task.Delay(TimeSpan.FromMilliseconds(_configuration.Value.DispatcherTimeoutMilliseconds), stop).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (stop.IsCancellationRequested)
		{
			return;
		}
		try
		{
			await processing.CancelAsync().ConfigureAwait(false);
		}
		catch (Exception callbackError)
		{
			// Observe user cancellation callbacks on this task, never on a timer thread.
			LogProcessingDataTaskError(request.RecordType, request.Attempts + 1, callbackError);
		}
	}

}
