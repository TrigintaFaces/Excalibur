// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Services;

/// <summary>
/// Background service that polls for due saga timeouts and delivers them to saga handlers.
/// </summary>
/// <remarks>
/// <para>
/// This service periodically calls <see cref="ISagaTimeoutStore.ClaimDueTimeoutsAsync"/> to atomically
/// claim a bounded batch of timeouts that are ready for delivery, deserializes each timeout message, and
/// dispatches it through <see cref="IDispatcher"/> where saga handling middleware routes it to the correct
/// saga instance. Delivery never uses <see cref="ISagaTimeoutStore.GetDueTimeoutsAsync"/>, which is a
/// read-only diagnostic query that claims nothing: under multiple instances it would deliver the same
/// timeout more than once.
/// </para>
/// <para>
/// <b>Reliability:</b> Timeouts are marked as delivered only after successful dispatch, ensuring
/// at-least-once delivery semantics. The underlying <see cref="ISagaTimeoutStore"/> implementation
/// (e.g., SqlServerSagaTimeoutStore) must persist timeouts to survive process restarts.
/// </para>
/// </remarks>
internal sealed partial class SagaTimeoutDeliveryService : BackgroundService
{
	private readonly ISagaTimeoutStore _timeoutStore;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<SagaTimeoutDeliveryService> _logger;
	private readonly SagaTimeoutOptions _options;
	private readonly ISagaTypeRegistry _typeRegistry;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaTimeoutDeliveryService"/> class.
	/// </summary>
	/// <param name="timeoutStore">The timeout store to poll for due timeouts.</param>
	/// <param name="serviceProvider">The service provider for creating scoped dispatchers.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">The timeout delivery options.</param>
	/// <param name="typeRegistry">The type registry naming every timeout message type the host
	/// registered during composition. Required: it is the only resolution path, so a service built
	/// without one resolves nothing and retires every timeout undelivered.</param>
	public SagaTimeoutDeliveryService(
		ISagaTimeoutStore timeoutStore,
		IServiceProvider serviceProvider,
		ILogger<SagaTimeoutDeliveryService> logger,
		IOptions<SagaTimeoutOptions> options,
		ISagaTypeRegistry typeRegistry)
	{
		_timeoutStore = timeoutStore ?? throw new ArgumentNullException(nameof(timeoutStore));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));
	}

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Saga timeout types are preserved through registration")]
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Saga timeout types are preserved through registration")]
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var activity = SagaActivitySource.StartActivity("SagaTimeoutDeliveryService.Execute");

		LogServiceStarting();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessDueTimeoutsAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Graceful shutdown
				break;
			}
			catch (Exception ex)
			{
				LogPollCycleFailed(ex);
			}

			try
			{
				await Task.Delay(_options.PollInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogServiceStopping();
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON deserialization may require types that cannot be statically analyzed")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON deserialization may require runtime code generation")]
	private async Task ProcessDueTimeoutsAsync(CancellationToken cancellationToken)
	{
		using var activity = SagaActivitySource.StartActivity("ProcessDueTimeouts");

		// ClaimDueTimeoutsAsync atomically leases due timeouts to this processor, so under a
		// multi-instance deployment two SagaTimeoutDeliveryService instances polling concurrently
		// never claim (and therefore never deliver) the same due timeout.
		var claimedTimeouts = await _timeoutStore
			.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, _options.BatchSize, cancellationToken)
			.ConfigureAwait(false);

		if (claimedTimeouts.Count == 0)
		{
			return;
		}

		_ = (activity?.SetTag("timeout.count", claimedTimeouts.Count));

		if (_options.EnableVerboseLogging)
		{
			LogProcessingTimeouts(claimedTimeouts.Count);
		}

		foreach (var timeout in claimedTimeouts)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			await DeliverTimeoutAsync(timeout, cancellationToken).ConfigureAwait(false);
		}
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON deserialization may require types that cannot be statically analyzed")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON deserialization may require runtime code generation")]
	private async Task DeliverTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken)
	{
		using var activity = SagaActivitySource.StartActivity("DeliverTimeout");
		_ = (activity?.SetTag("saga.id", timeout.SagaId));
		_ = (activity?.SetTag("timeout.id", timeout.TimeoutId));
		_ = (activity?.SetTag("timeout.type", timeout.TimeoutType));
		_ = (activity?.SetTag("timeout.due_at", timeout.DueAt.ToString("O")));

		// Re-establish the timeout's OWN tenant for the whole of its delivery.
		//
		// The claim that produced this timeout is deliberately estate-wide — a background loop leases due
		// timeouts across every tenant in one batch, because a tenant-scoped claim would lease only the
		// untenanted partition and every tenant's timeouts would sit due forever. Isolation is therefore not
		// enforced at the claim; it is enforced HERE, by running each timeout under the tenant it was scheduled
		// by. Without this the handler runs with no ambient tenant, so the saga it loads resolves the untenanted
		// partition rather than the one the saga was saved under, finds nothing, and the timeout is a silent
		// no-op — no exception, no log, just a saga that never advances.
		//
		// The scope wraps the entire method, not just the dispatch, because the MarkDeliveredAsync calls on the
		// unresolvable-type and invalid-message paths retire the row by (TenantId, TimeoutId) and would otherwise
		// match nothing, redelivering that timeout forever.
		//
		// BeginScope takes the partition's own term for an untenanted timeout, NOT null. A null ambient does
		// not "resolve back to the untenanted partition" — it clears the ambient, and the default
		// ITenantContext then resolves nothing, which TenantScope.FromContext fails closed on
		// (TenantRequiredException). The reserved untenanted term is rejected only when AUTHORING a tenant
		// from caller input (Scoped); read back off a stored row it is the legitimate term for the partition,
		// which is exactly what FromStoredValue returns here.
		var partition = KeyedTenantPartition.FromStoredValue(timeout.TenantId);
		using var tenantScope = TenantContextHolder.BeginScope(partition.TenantId);
		_ = (activity?.SetTag("tenant.id", partition.TenantId));

		try
		{
			// The registry is the ONLY resolution path. TimeoutType is a value read back from the timeout
			// store, so resolving it by scanning every loaded assembly would let a stored string select any
			// type in the process and hand it to the deserializer below -- the gadget-chain shape. The
			// registry answers only for types the host registered during composition.
			var timeoutType = _typeRegistry.ResolveType(timeout.TimeoutType);
			if (timeoutType is null)
			{
				LogTimeoutTypeResolutionFailed(
					timeout.TimeoutType,
					timeout.TimeoutId);
				// Mark as delivered to prevent retry loop for unresolvable types
				await _timeoutStore.MarkDeliveredAsync(timeout.TimeoutId, cancellationToken).ConfigureAwait(false);
				return;
			}

			object? timeoutMessage;
			if (timeout.TimeoutData is not null)
			{
				timeoutMessage = JsonSerializer.Deserialize(timeout.TimeoutData, timeoutType);
			}
			else
			{
				timeoutMessage = CreateTimeoutMessageInstance(timeoutType);
			}

			if (timeoutMessage is null)
			{
				LogTimeoutMessageCreationFailed(timeout.TimeoutType);
				await _timeoutStore.MarkDeliveredAsync(timeout.TimeoutId, cancellationToken).ConfigureAwait(false);
				return;
			}

			if (timeoutMessage is not IDispatchMessage dispatchMessage)
			{
				LogTimeoutMessageTypeInvalid(timeout.TimeoutType);
				await _timeoutStore.MarkDeliveredAsync(timeout.TimeoutId, cancellationToken).ConfigureAwait(false);
				return;
			}

			// Dispatch via saga handling infrastructure using scoped dispatcher
			await using var scope = _serviceProvider.CreateAsyncScope();
			var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

			var context = new MessageContext(dispatchMessage, scope.ServiceProvider)
			{
				MessageId = timeout.TimeoutId,
			};
			context.SetMessageType(timeout.TimeoutType);
			context.SetReceivedTimestampUtc(DateTimeOffset.UtcNow);

			// The TenantContextHolder.BeginScope(...) above establishes the timeout's own tenant --
			// partition.IsRealTenant ? partition.TenantId : null, deliberately null for an estate-wide
			// timeout -- as the AMBIENT (Channel A) tenant for this delivery, but nothing previously
			// carried it onto THIS context's identity feature (Channel B), so any message this handler
			// republishes via the ambient dispatch overload inherited no tenant at all, regardless of
			// BeginScope. ApplyAmbientTenantFallback reads TenantContextHolder.Current directly (not
			// ITenantContext), so it reproduces exactly the value BeginScope just established -- real
			// tenant or deliberately absent -- never converting the untenanted case into a false owner.
			context.ApplyAmbientTenantFallback();

			_ = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken).ConfigureAwait(false);

			// Mark delivered after successful dispatch
			await _timeoutStore.MarkDeliveredAsync(timeout.TimeoutId, cancellationToken).ConfigureAwait(false);

			if (_options.EnableVerboseLogging)
			{
				LogTimeoutDelivered(timeout.TimeoutId, timeout.SagaId);
			}
		}
		catch (Exception ex)
		{
			LogTimeoutDeliveryFailed(timeout.TimeoutId, timeout.SagaId, ex);
			_ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message));
			// Do NOT mark as delivered - will retry on next poll
		}
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection to invoke constructors. Register types via ISagaTypeRegistry for AOT-safe instantiation.")]
	private static object? CreateTimeoutMessageInstance(Type timeoutType)
	{
		var constructor = timeoutType.GetConstructor(Type.EmptyTypes);
		return constructor?.Invoke(null);
	}

	// Source-generated logging methods
	[LoggerMessage(SagaEventId.TimeoutDeliveryStarted, LogLevel.Information,
		"Saga timeout delivery service starting")]
	private partial void LogServiceStarting();

	[LoggerMessage(SagaEventId.TimeoutServiceStopped, LogLevel.Information,
		"Saga timeout delivery service stopping")]
	private partial void LogServiceStopping();

	[LoggerMessage(SagaEventId.TimeoutProcessingStarted, LogLevel.Debug,
		"Processing {Count} due timeouts")]
	private partial void LogProcessingTimeouts(int count);

	[LoggerMessage(SagaEventId.TimeoutDeliveredSuccessfully, LogLevel.Debug,
		"Delivered timeout {TimeoutId} to saga {SagaId}")]
	private partial void LogTimeoutDelivered(string timeoutId, string sagaId);

	[LoggerMessage(SagaEventId.TimeoutDeliveryFailed, LogLevel.Error,
		"Failed to deliver timeout {TimeoutId} to saga {SagaId}")]
	private partial void LogTimeoutDeliveryFailed(string timeoutId, string sagaId, Exception ex);

	[LoggerMessage(SagaEventId.TimeoutBatchCompleted, LogLevel.Warning,
		"Timeout poll cycle failed, will retry next cycle")]
	private partial void LogPollCycleFailed(Exception ex);

	[LoggerMessage(SagaEventId.TimeoutTypeResolutionFailed, LogLevel.Warning,
		"Could not resolve timeout type {TimeoutType} for timeout {TimeoutId}: it is not a registered saga timeout type")]
	private partial void LogTimeoutTypeResolutionFailed(string timeoutType, string timeoutId);

	[LoggerMessage(SagaEventId.TimeoutMessageCreationFailed, LogLevel.Warning,
		"Could not create timeout message instance for type {TimeoutType}")]
	private partial void LogTimeoutMessageCreationFailed(string timeoutType);

	[LoggerMessage(SagaEventId.TimeoutMessageTypeInvalid, LogLevel.Warning,
		"Timeout message type {TimeoutType} does not implement IDispatchMessage")]
	private partial void LogTimeoutMessageTypeInvalid(string timeoutType);
}
