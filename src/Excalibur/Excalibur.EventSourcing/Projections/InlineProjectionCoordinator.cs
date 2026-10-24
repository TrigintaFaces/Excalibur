// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Foreground completion and recovery share one ordered Inline cursor, independent of Async.</summary>
internal sealed class InlineProjectionCoordinator : IAsyncDisposable
{
	private readonly IServiceProvider _services;
	private readonly SemaphoreSlim _hostGate = new(1, 1);
	private readonly IOptions<EventNotificationOptions> _notificationOptions;
	private readonly ILogger<InlineProjectionCoordinator> _logger;
	private readonly TimeProvider _clock;
	private Task<AsyncProjectionProcessingHost>? _hostTask;

	public InlineProjectionCoordinator(IServiceProvider services, IOptions<EventNotificationOptions> notificationOptions,
		ILogger<InlineProjectionCoordinator> logger, TimeProvider? clock = null)
	{
		_services = services;
		_notificationOptions = notificationOptions;
		_logger = logger;
		_clock = clock ?? TimeProvider.System;
	}

	internal static void Register(IServiceCollection services)
	{
		services.TryAddSingleton<InlineProjectionCoordinator>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InlineProjectionHostedService>());

		// A COMPOSITION THAT CAN NEVER COMPLETE A SAVE IS REFUSED AT STARTUP, not on the first write.
		// Registered as a startup validator as well as a hosted service, because the hosted-service half
		// never runs in a host that does not run hosted services -- which is exactly the host this
		// refusal most needs to reach.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, DependencyInjection.InlineProjectionPrerequisiteValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, DependencyInjection.InlineProjectionPrerequisiteValidator>());

		// The pull entry point must drive INLINE too. Without this, a host that runs no background
		// service had no way to recover an incomplete fold: the save's own attempt was the only driver,
		// so the last save before the process went idle was never recovered.
		ProjectionProcessorRegistration.EnsureRegistered(services);
	}

	/// <summary>Drives the Inline cursor to the stream head, for a host with no background loop.</summary>
	/// <remarks>
	/// No required set: this is the whole-subscription question, not one save's. A caller asking for
	/// pending work wants every Inline projection advanced as far as it can go, and a halt in one is
	/// reported rather than hidden.
	/// </remarks>
	internal async Task<ProjectionProcessingResult> ProcessPendingAsync(CancellationToken token)
	{
		// MATERIALIZE THE DEFINITIONS BEFORE READING THE REGISTRY, the same step StartAsync performs.
		// Projection registrations are deferred to startup as closed generics, so a registry read on an
		// un-started container sees NOTHING and this method would report a vacuous CaughtUp having folded
		// not one event -- a silent success, in the one method a host-less consumer relies on.
		_ = _services.GetRequiredService<IEventNotificationBroker>();

		if (_services.GetRequiredService<IProjectionRegistry>().GetByMode(ProjectionMode.Inline).Count == 0)
		{
			return new ProjectionProcessingResult(0, ProjectionProcessingOutcome.CaughtUp, ProjectionHaltReason.None);
		}

		var host = await HostAsync().WaitAsync(token).ConfigureAwait(false);
		return await host.ProcessThroughAsync(null, required: null, token).ConfigureAwait(false);
	}

	internal async Task StartAsync(CancellationToken token)
	{
		// Resolving the broker materializes the deferred, closed-generic projection definitions.
		_ = _services.GetRequiredService<IEventNotificationBroker>();
		await (await HostAsync().ConfigureAwait(false)).StartAsync(token).ConfigureAwait(false);
	}

	internal async Task StopAsync(CancellationToken token)
	{
		if (_hostTask is { IsCompletedSuccessfully: true } started)
		{
			await (await started.ConfigureAwait(false)).StopAsync(token).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// The created host, shared by every caller, with a FAULT DELIBERATELY NOT CACHED.
	/// </summary>
	/// <remarks>
	/// This was a <see cref="Lazy{T}"/> over the creation task, which caches a faulted task
	/// permanently: one transient failure during creation -- a checkpoint store briefly unreachable --
	/// was then handed to every later save for the lifetime of the process, recoverable only by a
	/// restart and reported as a committed-but-pending save every time. Where the hosted service runs
	/// that is moot, because creation happens at startup and a failure correctly kills the process. It
	/// bites the pull-driven composition, where creation happens on the first save instead.
	/// </remarks>
	private Task<AsyncProjectionProcessingHost> HostAsync()
	{
		var cached = _hostTask;
		return cached is not null && !cached.IsFaulted && !cached.IsCanceled ? cached : CreateHostOnceAsync();
	}

	private async Task<AsyncProjectionProcessingHost> CreateHostOnceAsync()
	{
		await _hostGate.WaitAsync().ConfigureAwait(false);
		try
		{
			var cached = _hostTask;
			if (cached is not null && !cached.IsFaulted && !cached.IsCanceled)
			{
				return await cached.ConfigureAwait(false);
			}

			// Published BEFORE the await so concurrent savers join this creation rather than starting
			// their own, then cleared on failure so the NEXT caller retries instead of inheriting it.
			var created = CreateValidatedHostAsync();
			_hostTask = created;
			try
			{
				return await created.ConfigureAwait(false);
			}
			catch
			{
				_hostTask = null;
				throw;
			}
		}
		finally
		{
			_ = _hostGate.Release();
		}
	}

	internal async Task<ProjectionFreshness> CompleteAsync(
		EventNotificationContext context,
		IReadOnlyList<IDomainEvent> events)
	{
		ArgumentNullException.ThrowIfNull(events);

		if (_services.GetRequiredService<IProjectionRegistry>().GetByMode(ProjectionMode.Inline).Count == 0)
		{
			return ProjectionFreshness.NotRequested;
		}

		if (context.GlobalPosition is not > 0)
		{
			// A MISCONFIGURATION, reported rather than thrown. The events are already committed, so an
			// exception here would tell the caller a durable save failed when it did not. The read model
			// genuinely is behind and will stay behind, which is exactly what BoundExpired says -- but
			// unlike an expired wait this will never resolve, so it is logged at Error rather than
			// Warning. Startup validation is where this composition should be refused outright.
			_logger.LogError(
				"Inline projections are registered but the event store reported no committed global position "
				+ "for aggregate {AggregateId}. The events are committed; the read model cannot be advanced "
				+ "and will stay behind until a positioned event store is configured.",
				context.AggregateId);
			return ProjectionFreshness.BoundExpired;
		}
		using var bound = new CancellationTokenSource(_notificationOptions.Value.PostCommitTimeout, _clock);
		Task<ProjectionProcessingResult>? work = null;
        var processing = false;
		try
		{
			var host = await HostAsync().WaitAsync(bound.Token).ConfigureAwait(false);

			// WAIT ONLY ON THE PROJECTIONS THIS SAVE FEEDS. A projection consuming none of these event
			// types is not advanced by this save and is not attested by it, so holding the save open for
			// it would be waiting on work the save did not cause -- which is how one faulting projection
			// came to stall every writer.
			var committedTypes = new Type[events.Count];
			for (var i = 0; i < events.Count; i++)
			{
				committedTypes[i] = events[i].GetType();
			}

			var required = host.RequiredRegistrations(committedTypes);
			if (required.Count == 0)
			{
				// This save feeds no Inline projection. Nothing was waited for and nothing is claimed --
				// the same honest answer as a host with no Inline projection registered at all.
				return ProjectionFreshness.NotRequested;
			}

			// RETRY EXACTLY ONE HALT REASON, AND ONLY INSIDE THE BUDGET THE CALLER ALREADY GRANTED.
			//
			// Failing a committed save is the most expensive answer this method can give: it tells the
			// caller its durable write did not complete. So a transient stop must not produce it --
			// PrefixUnavailable means the next contiguous prefix is not readable YET, the normal reading
			// of an append still in flight, and it clears on its own.
			//
			// Every other reason is terminal here and must NOT be retried. A faulted projection, an
			// undeserializable event, or a write whose acknowledgement was lost do not clear by waiting,
			// and retrying them would convert an unconfirmed write into a reported success -- the silent
			// loss this seam exists to prevent. An earlier version of this method retried on the OUTCOME
			// alone and did exactly that; the regression arm for a lost acknowledgement caught it.
			//
			// The DEADLINE bounds the wait, never an attempt count: the budget is the contract.
			var backoff = TimeSpan.FromMilliseconds(25);
			while (true)
			{
				processing = true;
				work = host.ProcessThroughAsync(context.GlobalPosition, required, bound.Token);
				var result = await work.WaitAsync(bound.Token).ConfigureAwait(false);
				if (result.Outcome == ProjectionProcessingOutcome.CaughtUp)
				{
					return ProjectionFreshness.Fresh;
				}

				if (result.HaltReason != ProjectionHaltReason.PrefixUnavailable)
				{
					_logger.LogWarning(
						"Inline projections did not reach position {Position} for aggregate {AggregateId}: {Reason}. "
						+ "The events are committed; the read model is behind. Do not repeat the command.",
						context.GlobalPosition.Value, context.AggregateId, result.HaltReason);
					return ProjectionFreshness.BoundExpired;
				}

				await Task.Delay(backoff, _clock, bound.Token).ConfigureAwait(false);
				backoff = backoff.TotalMilliseconds >= 250 ? backoff : backoff * 2;
			}
		}
		catch (OperationCanceledException) when (bound.IsCancellationRequested)
		{
			// The BUDGET expired, which is the ordinary bounded-wait outcome and not an error: the events
			// are committed and the read model is merely behind. The abandoned work is still observed so
			// its failure is logged rather than swallowed as an unobserved task exception.
			if (work is not null && !work.IsCompleted)
			{
				_ = ObserveAsync(work);
			}

			_logger.LogWarning(
				"Inline projections did not reach position {Position} for aggregate {AggregateId} within the "
				+ "post-commit budget. The events are committed; the read model is behind. Do not repeat the command.",
				context.GlobalPosition.Value, context.AggregateId);
			return ProjectionFreshness.BoundExpired;
		}
        catch (Exception error) when (processing || error is System.IO.IOException
            or System.Data.Common.DbException or HttpRequestException or TimeoutException or OperationCanceledException)
        {
            // Append already succeeded. Preserve its token and report unconfirmed projection progress.
            // Configuration failures during host validation still fail fast at their original boundary.
            _logger.LogError(error,
                "Inline processing failed after committing position {Position} for aggregate {AggregateId}. "
                + "The events are durable; projection progress is unconfirmed. Do not repeat the command.",
                context.GlobalPosition.Value, context.AggregateId);
            return ProjectionFreshness.BoundExpired;
        }

	}

	private async Task ObserveAsync(Task work)
	{
		try { await work.ConfigureAwait(false); }
		catch (Exception error) { _logger.LogError(error, "Inline processing failed after its foreground deadline."); }
	}

	internal async Task<bool> HasAppliedAsync(Type projectionType, long position, CancellationToken token) =>
		await (await HostAsync().WaitAsync(token).ConfigureAwait(false))
			.HasAppliedAsync(projectionType, position, token).ConfigureAwait(false);

	private async Task<AsyncProjectionProcessingHost> CreateValidatedHostAsync()
    {
        await GeneratedProjectionValidation.ValidateAsync(_services, CancellationToken.None).ConfigureAwait(false);
        return await CreateHostAsync().ConfigureAwait(false);
    }

    private async Task<AsyncProjectionProcessingHost> CreateHostAsync()
    {
		var registry = _services.GetRequiredService<IProjectionRegistry>();
		var configured = _services.GetRequiredService<IOptions<GlobalStreamProjectionOptions>>().Value;
		ValidateName(configured);
		var source = _services.GetService<InlineProjectionSource>();
		if (source is not null && !ReferenceEquals(source.Query, _services.GetRequiredService<Excalibur.EventSourcing.Queries.IGlobalStreamQuery>()))
		{
			throw new InvalidOperationException("The global event reader was replaced independently of its SQL Inline checkpoint binding. "
				+ "Configure a source-affine reader/checkpoint pair; automatic Inline setup cannot certify this composition.");
		}
		var checkpoints = source?.Checkpoints;
		if (checkpoints is null)
		{
			if (_services.GetService<ISubscriptionCheckpointDurability>() is null
				&& _services.GetService<InMemoryProjectionCheckpointAcknowledgement>() is null)
			{
				throw new InvalidOperationException("Inline projections require a durable checkpoint store. "
					+ "UseSqlServer supplies one automatically; other providers must register their checkpoint store.");
			}
			checkpoints = _services.GetRequiredService<ISubscriptionCheckpointStore>();
		}
		var scopeIdentity = configured.SubscriptionScope;
		if (string.IsNullOrWhiteSpace(scopeIdentity))
		{
			if (source is null)
			{
				throw new InvalidOperationException("Set GlobalStreamProjectionOptions.SubscriptionScope for this custom Inline event source.");
			}
			await using var scope = _services.CreateAsyncScope();
			var registrations = registry.GetByMode(ProjectionMode.Inline);
			var probes = scope.ServiceProvider.GetServices<IPositionedProjectionProbe>().ToDictionary(p => p.ProjectionType);
			var parts = new List<string> { source.Identity };
			foreach (var registration in registrations.OrderBy(r => r.ProjectionType.FullName, StringComparer.Ordinal))
			{
				var destination = probes.TryGetValue(registration.ProjectionType, out var probe)
					? probe.GetDestinationScope(scope.ServiceProvider) : null;
				if (string.IsNullOrWhiteSpace(destination))
				{
					throw new InvalidOperationException($"Projection '{registration.ProjectionType.Name}' cannot identify its cluster automatically. "
						+ "Set GlobalStreamProjectionOptions.SubscriptionScope to a shared source/destination name.");
				}
				parts.Add(destination);
			}
			var descriptor = new StringBuilder();
			foreach (var part in parts) { descriptor.Append(part.Length).Append(':').Append(part); }
			scopeIdentity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(descriptor.ToString())));
		}
		var options = new GlobalStreamProjectionOptions
		{
			ProjectionName = "Inline:" + configured.ProjectionName,
			SubscriptionScope = scopeIdentity,
			SubscriptionRevision = configured.SubscriptionRevision,
			BatchSize = configured.BatchSize,
			IdlePollingInterval = configured.IdlePollingInterval,
		};
		ValidateEffectiveOptions(options);
		return new AsyncProjectionProcessingHost(registry, _services.GetRequiredService<IEventSerializer>(),
			checkpoints, Options.Create(options), _services, _services.GetRequiredService<ILogger<AsyncProjectionProcessingHost>>(),
			_notificationOptions, _clock, ProjectionMode.Inline);
	}

	private static void ValidateName(GlobalStreamProjectionOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.ProjectionName))
		{
			throw new OptionsValidationException(Options.DefaultName, typeof(GlobalStreamProjectionOptions),
				["ProjectionName must not be blank."]);
		}
	}

	private static void ValidateEffectiveOptions(GlobalStreamProjectionOptions options)
	{
		var validation = new GlobalStreamProjectionOptionsValidator().Validate(Options.DefaultName, options);
		if (validation.Failed)
		{
			throw new OptionsValidationException(Options.DefaultName, typeof(GlobalStreamProjectionOptions), validation.Failures);
		}
	}

	public async ValueTask DisposeAsync()
	{
		var created = _hostTask;
		if (created is not null)
		{
			try { (await created.ConfigureAwait(false)).Dispose(); }
			catch (Exception error) { _logger.LogDebug(error, "Inline initialization failed before disposal."); }
		}

		_hostGate.Dispose();
	}
}

internal sealed class InlineProjectionHostedService(InlineProjectionCoordinator coordinator) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken) => coordinator.StartAsync(cancellationToken);
	public Task StopAsync(CancellationToken cancellationToken) => coordinator.StopAsync(cancellationToken);
}
