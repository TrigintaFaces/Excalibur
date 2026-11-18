// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
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
/// <para>The existing constructor/JSON(Type) runtime path retains its trimming/AOT limitations and
/// annotations. This hold integration does not certify full NativeAOT timeout delivery.</para>
/// </remarks>
internal sealed partial class SagaTimeoutDeliveryService : BackgroundService
{
	private readonly ISagaTimeoutStore _timeoutStore;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<SagaTimeoutDeliveryService> _logger;
	private readonly SagaTimeoutOptions _options;
	private readonly ISagaTypeRegistry _typeRegistry;
	private readonly SagaTimeoutFailureDelivery _failures;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaTimeoutDeliveryService"/> class.
	/// </summary>
	/// <param name="timeoutStore">The timeout store to poll for due timeouts.</param>
	/// <param name="serviceProvider">The service provider for creating scoped dispatchers.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">The timeout delivery options.</param>
	/// <param name="typeRegistry">The type registry naming every timeout message type the host
	/// registered during composition. Required: it is the only resolution path; an unresolved stored
	/// type is retained as Unknown for explicit configuration repair, never acknowledged undelivered.</param>
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
		_failures = new SagaTimeoutFailureDelivery(_timeoutStore,
			_serviceProvider.GetService<IMessageFailureClassifier>() ?? new DefaultMessageFailureClassifier(), _logger, _options.BatchSize);
	}

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _failures.ValidateAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
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

		await _failures.RetryPendingAsync(cancellationToken).ConfigureAwait(false);

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

		foreach (var claim in claimedTimeouts)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			await DeliverTimeoutAsync(claim, cancellationToken).ConfigureAwait(false);
		}
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON deserialization may require types that cannot be statically analyzed")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON deserialization may require runtime code generation")]
	private async Task DeliverTimeoutAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken)
	{
		var timeout = claim.Timeout;
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
		// The scope wraps dispatch and conditional hold/retirement so each mutation addresses the original
		// stored partition. Unresolvable/invalid messages are retained for explicit repair, never acknowledged.
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

		var dispatchSucceeded = false;
		try
		{
            if (await _failures.SuppressPendingAsync(claim, cancellationToken).ConfigureAwait(false)) { return; }
            // Keep polling/rebinding existing pending work at capacity, but do not dispatch another
            // new source whose failure could retain an unbounded payload backlog. No ack/delete:
            // ordinary lease expiry preserves eligibility once disposition storage recovers.
            if (!_failures.HasCapacity) { return; }
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
                await _failures.RecordUntypedResultAsync(claim, $"Timeout type '{timeout.TimeoutType}' is not registered; repair configuration before explicit resume.", cancellationToken).ConfigureAwait(false);
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
                await _failures.RecordUntypedResultAsync(claim, $"Timeout '{timeout.TimeoutId}' could not be constructed as a dispatch message; repair configuration before explicit resume.", cancellationToken).ConfigureAwait(false);
                return;
			}

			if (timeoutMessage is not IDispatchMessage dispatchMessage)
			{
				LogTimeoutMessageTypeInvalid(timeout.TimeoutType);
                await _failures.RecordUntypedResultAsync(claim, $"Timeout '{timeout.TimeoutId}' could not be constructed as a dispatch message; repair configuration before explicit resume.", cancellationToken).ConfigureAwait(false);
                return;
			}

			// Dispatch via saga handling infrastructure using scoped dispatcher
			await using var scope = _serviceProvider.CreateAsyncScope();
			var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

			var context = new MessageContext(dispatchMessage, scope.ServiceProvider)
			{
				MessageId = timeout.TimeoutId,
			};
			if (claim.OwnedDelivery is { } originalDelivery)
			{
				// Encode BEFORE dispatch. Failure leaves the row leased for retry; never queue a partial group.
				SagaTimeoutDeliveryMetadata.Write(context, originalDelivery);
			}
			context.SetMessageType(timeout.TimeoutType);
			context.SetReceivedTimestampUtc(DateTimeOffset.UtcNow);

			// The TenantContextHolder.BeginScope(...) above establishes the timeout's own tenant --
			// partition.TenantId, which for an estate-wide timeout is the partition's own reserved
			// untenanted term and NOT null, for the reason given at the BeginScope call itself --
			// as the AMBIENT (Channel A) tenant for this delivery, but nothing previously
			// carried it onto THIS context's identity feature (Channel B), so any message this handler
			// republishes via the ambient dispatch overload inherited no tenant at all, regardless of
			// BeginScope. ApplyAmbientTenantFallback reads TenantContextHolder.Current directly (not
			// ITenantContext), so it reproduces exactly the value BeginScope just established -- real
			// tenant or deliberately absent -- never converting the untenanted case into a false owner.
			context.ApplyAmbientTenantFallback();

			var result = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken).ConfigureAwait(false);

            // An untyped failed result carries no canonical retry evidence. Preserve it as Unknown,
            // with diagnostic text only; do not infer a kind from strings or fabricate an exception.
            if (result is { Succeeded: false })
            {
                await _failures.RecordUntypedResultAsync(claim, result.ErrorMessage, cancellationToken).ConfigureAwait(false);
                return;
            }
            dispatchSucceeded = true;

			// Mark delivered after successful dispatch
			await RetireAsync(claim, cancellationToken).ConfigureAwait(false);

			if (_options.EnableVerboseLogging)
			{
				LogTimeoutDelivered(timeout.TimeoutId, timeout.SagaId);
			}
		}
		catch (Exception ex)
		{
			LogTimeoutDeliveryFailed(timeout.TimeoutId, timeout.SagaId, ex);
			_ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message));
			// A retirement/cleanup fault after successful dispatch is not a new handler failure.
            // Caller cancellation likewise never grants a successful disposition.
            if (!dispatchSucceeded && !cancellationToken.IsCancellationRequested)
            { await _failures.RecordFaultAsync(claim, ex, cancellationToken).ConfigureAwait(false); }
		}
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection to invoke constructors. Register types via ISagaTypeRegistry for AOT-safe instantiation.")]
	private static object? CreateTimeoutMessageInstance(Type timeoutType)
	{
		var constructor = timeoutType.GetConstructor(Type.EmptyTypes);
		return constructor?.Invoke(null);
	}

	/// <summary>
	/// Retires a timeout the caller has just delivered, presenting the claim it holds.
	/// </summary>
	/// <remarks>
	/// A refusal here is NOT an error and must not be raised as one. It means this processor stalled past
	/// its lease and another has re-claimed the timeout, which is the case the lease exists to handle. The
	/// live claim holder owns the outcome from that point, so the correct behaviour is to record the fact
	/// and stop touching the row -- retrying the retirement would be an attempt to remove a row this caller
	/// no longer owns, which is the defect the claim exists to prevent.
	/// </remarks>
	private async Task RetireAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken)
	{
		var outcome = await _timeoutStore.MarkDeliveredAsync(claim, cancellationToken).ConfigureAwait(false);

		if (outcome == SagaTimeoutRetirementOutcome.Superseded)
		{
			LogTimeoutRetirementSuperseded(claim.Timeout.TimeoutId, claim.Timeout.SagaId);
		}
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

	[LoggerMessage(SagaEventId.TimeoutRetirementSuperseded, LogLevel.Information,
		"Timeout {TimeoutId} for saga {SagaId} was delivered but could not be retired: this processor's claim was superseded, so a live claimant now owns it")]
	private partial void LogTimeoutRetirementSuperseded(string timeoutId, string sagaId);

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
