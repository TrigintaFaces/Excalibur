// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Outbox;

using Xunit;

namespace Excalibur.Dispatch.Tests.Functional;

/// <summary>
///     Keystone author≠impl regression lock for Sprint 849 Lane K (Beads <c>rb4g4b</c>): the default
///     dispatch pipeline wired by <see cref="DispatchConfigurationServiceCollectionExtensions.AddDefaultDispatchPipelines"/>
///     MUST actually execute on a plain <c>IDispatcher.DispatchAsync</c> — including outbox staging when
///     outbox infrastructure is registered.
/// </summary>
/// <remarks>
///     <para>
///     <strong>The bug (pre-fix):</strong> <c>AddDefaultDispatchPipelines()</c> configured the "Default"
///     pipeline with <c>ForMessageKinds(MessageKinds.All)</c> but never <c>UseProfile("default")</c> (unlike
///     its <c>Strict</c>/<c>Events</c> siblings). With no profile selected the pipeline registers zero
///     middleware (<c>PipelineBuilder.HasMiddlewareRegistered == false</c>), so <c>DispatchBuilder</c> falls
///     back to the empty <c>Direct</c> profile and <c>Dispatcher._canBypassAllMiddleware</c> bypasses the whole
///     pipeline. Result: NONE of the default middleware — including <c>OutboxStagingMiddleware</c> — runs on a
///     plain <c>DispatchAsync</c>, so outbox staging silently never happens on the default path.
///     </para>
///     <para>
///     <strong>The fix:</strong> replace <c>ForMessageKinds(MessageKinds.All)</c> with
///     <c>UseProfile("default")</c>, which resolves <c>DefaultPipelineProfiles.CreateDefaultProfile()</c>
///     (8 middleware incl. <c>OutboxStagingMiddleware</c> at position 7). The default <c>DispatchAsync</c> then
///     runs the real default chain and stages.
///     </para>
///     <para>
///     <strong>Discipline:</strong> unlike the S848 <c>MessageFlowScenarioShould</c> harness (which invoked
///     <c>OutboxStagingMiddleware</c> DIRECTLY precisely because the default <c>DispatchAsync</c> bypassed it
///     pre-fix), this lock dispatches via the real <c>IDispatcher.DispatchAsync</c> — the DEFAULT path — and
///     asserts staging. That assertion is RED on the pre-fix tree (pipeline empty → middleware bypassed →
///     nothing staged) and GREEN after the <c>UseProfile("default")</c> fix. All observed state is real; no
///     constant is asserted. Deterministic and in-process (no container); waits poll a condition, never sleep.
///     </para>
/// </remarks>
[Trait("Category", "Functional")]
[Trait("Component", "Dispatch.Core")]
[Trait("Feature", "DefaultPipelineWiring")]
public sealed class DefaultPipelineOutboxWiringShould : FunctionalTestBase
{
    /// <summary>
    ///     AC-K.1 (FR-K.1/FR-K.2 — the keystone proof, RED pre-fix → GREEN post-fix): given
    ///     <c>AddDefaultDispatchPipelines()</c> + a registered observable <see cref="IOutboxStore"/> +
    ///     <c>OutboxStagingOptions.Enabled = true</c> + a command handler that stages an event via the real
    ///     <see cref="IOutboxWriter"/>, when the command is dispatched via the DEFAULT
    ///     <see cref="IDispatcher.DispatchAsync"/> path (NOT by invoking the middleware directly), then the
    ///     event IS staged in the outbox store — proving the default pipeline's outbox-staging middleware ran.
    /// </summary>
    [Fact]
    public async Task StageEventViaDefaultDispatchAsyncWhenOutboxInfraRegistered()
    {
        // Arrange — the real default pipeline + an observable outbox store + a handler that stages an event.
        var outbox = new ObservableOutboxStore();
        await using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IOutboxStore>(outbox);
            services.AddSingleton(new HandlerInvocationState());
            services.AddScoped<IActionHandler<PlaceWidgetOrder>, PlaceWidgetOrderHandler>();
            services.Configure<OutboxStagingOptions>(static o => o.Enabled = true);
        });

        var orderId = $"order-{Guid.NewGuid():N}";

        // Act — dispatch the command through the REAL default DispatchAsync path. We deliberately do NOT
        // resolve/invoke OutboxStagingMiddleware ourselves: the whole point is that the default pipeline
        // wired by AddDefaultDispatchPipelines runs it for us. We use the 3-arg IDispatcher.DispatchAsync
        // (explicit context, the same routing the S848 T1 harness used) so the dispatch goes through the
        // configured pipeline / middleware invoker — NOT the 2-arg extension's own IDirectLocalDispatcher
        // ultra-local shortcut, which would bypass the pipeline independently of the wiring under test and
        // make the lock vacuous. Pre-fix the pipeline is empty and bypassed (_canBypassAllMiddleware), so the
        // staging middleware never executes and nothing is staged → this assertion fails for the right reason (RED).
        await using (var scope = provider.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var context = scope.ServiceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
            context.MessageId = Guid.NewGuid().ToString();

            var result = await dispatcher
                .DispatchAsync(new PlaceWidgetOrder { OrderId = orderId }, context, CancellationToken.None)
                .ConfigureAwait(false);

            result.IsSuccess.ShouldBeTrue($"the command dispatch should succeed: {result.ErrorMessage}");
        }

        // Assert AC-K.1 — the event is observable as a staged outbox entry. This is ONLY true if the default
        // pipeline's OutboxStagingMiddleware ran on the default DispatchAsync path (RED pre-fix, GREEN post-fix).
        var staged = await WaitForConditionAsync(
                () => outbox.StagedMessages.Any(m => m.MessageType == nameof(WidgetOrderPlaced)),
                TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

        staged.ShouldBeTrue(
            "the WidgetOrderPlaced event MUST be staged by the default pipeline's OutboxStagingMiddleware on a " +
            "plain IDispatcher.DispatchAsync — pre-fix the empty 'Default' pipeline is bypassed and nothing stages.");

        outbox.StagedMessages
            .Count(m => m.MessageType == nameof(WidgetOrderPlaced))
            .ShouldBe(1, "exactly one event should be staged for one dispatched command");
    }

    /// <summary>
    ///     AC-K.2 (FR-K.1 — the full default chain executes on <c>DispatchAsync</c>, NOT the ultra-local
    ///     bypass): asserts three jointly-sufficient observable signals that the configured default middleware
    ///     chain ran end-to-end rather than the empty <c>Direct</c> bypass that calls the handler directly with
    ///     zero middleware.
    /// </summary>
    /// <remarks>
    ///     <para>The three signals:</para>
    ///     <list type="number">
    ///         <item><description>
    ///             <strong>Non-bypass marker (structural):</strong> the dispatched context carries
    ///             <c>OutboxEnabled == true</c>, which is set ONLY by <c>OutboxStagingMiddleware.SetOutboxContext</c>
    ///             (<c>OutboxStagingMiddleware.cs:159-163</c>) as the chain runs. The ultra-local bypass instantiates
    ///             no middleware, so this marker is absent on a bypassed dispatch — its presence proves the dispatch
    ///             routed through the middleware invoker pipeline, not the bypass.
    ///         </description></item>
    ///         <item><description>
    ///             <strong>Handler continuation reached:</strong> the command handler ran exactly once (the chain
    ///             flowed through to the terminal continuation).
    ///         </description></item>
    ///         <item><description>
    ///             <strong>Post-processing stage executed:</strong> the event was staged. Staging is the work of the
    ///             default profile's <c>OutboxStagingMiddleware</c> at position 7 of the 8-middleware default chain
    ///             (<c>DefaultPipelineProfiles.CreateDefaultProfile():65</c>). Its post-handler effect being observed
    ///             demonstrates the chain executed THROUGH the pipeline to PostProcessing — a position the empty
    ///             bypass can never reach. (Per the seam pin, the full set of default middleware is not all
    ///             independently observable from a minimal consumer container, so position-7 staging is the grounded
    ///             behavioral proxy for "the chain executed", combined with the structural non-bypass marker above.)
    ///         </description></item>
    ///     </list>
    /// </remarks>
    [Fact]
    public async Task ExecuteFullDefaultMiddlewareChainOnDispatchAsyncNotBypass()
    {
        var outbox = new ObservableOutboxStore();
        var handlerState = new HandlerInvocationState();
        await using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IOutboxStore>(outbox);
            services.AddSingleton(handlerState);
            services.AddScoped<IActionHandler<PlaceWidgetOrder>, PlaceWidgetOrderHandler>();
            services.Configure<OutboxStagingOptions>(static o => o.Enabled = true);
        });

        var orderId = $"order-{Guid.NewGuid():N}";
        IMessageContext dispatchedContext;

        await using (var scope = provider.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            dispatchedContext = scope.ServiceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
            dispatchedContext.MessageId = Guid.NewGuid().ToString();

            _ = await dispatcher
                .DispatchAsync(new PlaceWidgetOrder { OrderId = orderId }, dispatchedContext, CancellationToken.None)
                .ConfigureAwait(false);
        }

        // Signal 1 — non-bypass marker: only a middleware that actually ran sets OutboxEnabled on the context.
        // Absent on the ultra-local bypass (no middleware instantiated). Pre-fix this is false/absent (RED).
        dispatchedContext.GetItem<bool>("OutboxEnabled").ShouldBeTrue(
            "the default middleware chain must have run on DispatchAsync (OutboxStagingMiddleware sets OutboxEnabled); " +
            "its absence means the dispatch took the empty ultra-local bypass instead of the configured chain.");

        // Signal 2 — the chain reached the handler continuation exactly once.
        handlerState.InvocationCount(orderId).ShouldBe(1,
            "the default pipeline must invoke the command handler exactly once");

        // Signal 3 — the post-processing stage (OutboxStagingMiddleware, pos 7 of 8) executed after the handler.
        var staged = await WaitForConditionAsync(
                () => outbox.StagedMessages.Any(m => m.MessageType == nameof(WidgetOrderPlaced)),
                TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

        staged.ShouldBeTrue(
            "the post-handler OutboxStagingMiddleware (position 7 of the default chain) must have staged the event — " +
            "proving the chain executed through the pipeline to PostProcessing, not the empty Direct bypass.");
    }

    /// <summary>
    ///     AC-K.3 (FR-K.3 — the ultra-local fast path is preserved): a genuinely-empty consumer that does NOT
    ///     call <c>AddDefaultDispatchPipelines()</c> (registers no middleware) still dispatches a message
    ///     successfully via <see cref="IDispatcher.DispatchAsync"/>. The fix must not force middleware onto a
    ///     consumer who registered none. GREEN both pre- and post-fix (guards against the fix breaking the
    ///     bypass).
    /// </summary>
    [Fact]
    public async Task PreserveFastPathForGenuinelyEmptyConsumer()
    {
        var handlerState = new HandlerInvocationState();
        var services = new ServiceCollection();
        services.AddLogging(static b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(handlerState);
        services.AddScoped<IActionHandler<PlaceWidgetOrder>, NoOutboxWidgetHandler>();

        // Only the core dispatcher — deliberately NO AddDefaultDispatchPipelines(): the genuinely-empty case.
        services.AddDispatchPipeline();
        services.AddDispatchHandlers();

        await using var provider = services.BuildServiceProvider();
        var orderId = $"order-{Guid.NewGuid():N}";

        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var result = await dispatcher
            .DispatchAsync(new PlaceWidgetOrder { OrderId = orderId }, CancellationToken.None)
            .ConfigureAwait(false);

        result.IsSuccess.ShouldBeTrue(
            $"a genuinely-empty consumer must still dispatch via the fast path: {result.ErrorMessage}");
        handlerState.InvocationCount(orderId).ShouldBe(1,
            "the handler still runs on the ultra-local fast path even with no middleware registered");
    }

    /// <summary>
    ///     AC-K.4 (FR-K.4 — staging self-gates on infra; no throw when no store): given
    ///     <c>AddDefaultDispatchPipelines()</c> but NO concrete <see cref="IOutboxStore"/> registered, when a
    ///     command is dispatched via <see cref="IDispatcher.DispatchAsync"/>, then the dispatch succeeds with NO
    ///     throw (the <c>OutboxStagingMiddleware</c> self-guards on <c>_outboxStore != null</c>). GREEN both
    ///     pre- and post-fix — proves the fix does NOT introduce a resolve error / throw when no store exists.
    /// </summary>
    [Fact]
    public async Task NotThrowAndNotStageWhenOutboxStoreNotRegistered()
    {
        var handlerState = new HandlerInvocationState();
        await using var provider = BuildProvider(services =>
        {
            // No IOutboxStore registration. OutboxStagingOptions stays at its default (Enabled == false) so the
            // middleware ctor does not throw the "enabled but no store" guard; staging simply no-ops.
            services.AddSingleton(handlerState);
            services.AddScoped<IActionHandler<PlaceWidgetOrder>, NoOutboxWidgetHandler>();
        });

        var orderId = $"order-{Guid.NewGuid():N}";

        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var context = scope.ServiceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
        context.MessageId = Guid.NewGuid().ToString();

        var result = await dispatcher
            .DispatchAsync(new PlaceWidgetOrder { OrderId = orderId }, context, CancellationToken.None)
            .ConfigureAwait(false);

        result.IsSuccess.ShouldBeTrue(
            $"dispatch must succeed even when no outbox store is registered (staging self-gates): {result.ErrorMessage}");
        handlerState.InvocationCount(orderId).ShouldBe(1, "the handler still runs when no outbox store is registered");
    }

    /// <summary>
    ///     Option-A sibling guard (the sanctioned scope expansion) — guards the SAME Layer-3 invoker
    ///     materialization seam the Strict/Events profiles were latently broken on (the "twin bug": pre-fix,
    ///     <c>Strict</c>/<c>Events</c> also resolved to empty/unwired chains because a <c>UseProfile</c>-configured
    ///     pipeline never reached the dispatcher's <see cref="IDispatchMiddlewareInvoker"/>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The real <c>Strict</c>/<c>InternalEvent</c> profiles cannot be wired from this functional test
    ///     project without widening production visibility: their chains reference <c>internal</c> services
    ///     (<c>IContractVersionService</c>, <c>DeferredOutboxWriter</c>) and un-registered dependencies
    ///     (<c>IMessageMetrics</c>). Per the mini-spec's "assert its configured chain is non-empty via whatever
    ///     behavioral signal you can reach", this guard instead registers a <strong>custom public profile</strong>
    ///     containing a single observable test middleware and configures the dispatch ("Default") pipeline to
    ///     <c>UseProfile</c> it — exercising the IDENTICAL seam: a profile-configured pipeline's middleware MUST
    ///     reach the dispatcher invoker and execute on <c>DispatchAsync</c>.
    ///     </para>
    ///     <para>
    ///     RED pre-fix: the profile-configured middleware never reaches <c>_globalMiddleware</c>, so the invoker
    ///     has <c>HasMiddleware==false</c> and the dispatch bypasses the chain → the middleware never runs.
    ///     GREEN once Layer-3 materializes the invoker from the configured pipeline's <c>ConfiguredMiddlewareTypes</c>.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task WireMiddlewareForAProfileConfiguredPipelineOnDispatchAsync()
    {
        var marker = new MiddlewareExecutionMarker();
        const string profileName = "sibling-guard-profile";

        var services = new ServiceCollection();
        services.AddLogging(static b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(marker);
        services.AddSingleton<ObservableMarkerMiddleware>();
        services.AddScoped<IActionHandler<PlaceWidgetOrder>, NoOutboxWidgetHandler>();
        services.AddSingleton(new HandlerInvocationState());

        // Configure the dispatch pipeline to use a custom profile that contains the observable middleware.
        // This mirrors how AddDefaultDispatchPipelines configures "Default" -> UseProfile(...), so it travels
        // the same profile -> configured-pipeline -> dispatcher-invoker path the Strict/Events twin-bug lives on.
        services.AddDispatch(builder =>
        {
            var profile = new PipelineProfile(profileName, MessageKinds.All);
            profile.AddMiddleware<ObservableMarkerMiddleware>(1);
            _ = builder.RegisterProfile(profile);
            _ = builder.ConfigurePipeline("Default", pipeline => pipeline.UseProfile(profileName));
        });

        await using var provider = services.BuildServiceProvider();
        var orderId = $"order-{Guid.NewGuid():N}";

        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var context = scope.ServiceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
        context.MessageId = Guid.NewGuid().ToString();

        var result = await dispatcher
            .DispatchAsync(new PlaceWidgetOrder { OrderId = orderId }, context, CancellationToken.None)
            .ConfigureAwait(false);

        result.IsSuccess.ShouldBeTrue($"the profile-configured dispatch should succeed: {result.ErrorMessage}");

        var ran = await WaitForConditionAsync(() => marker.Executed, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        ran.ShouldBeTrue(
            "a middleware in the profile-configured dispatch pipeline MUST execute on DispatchAsync — pre-fix the " +
            "configured profile never reaches the dispatcher's IDispatchMiddlewareInvoker, so the chain is bypassed " +
            "(the same Layer-3 defect that left the Strict/Events profiles unwired).");
    }

    /// <summary>
    ///     Builds a service provider wired with the REAL default Dispatch pipeline
    ///     (<c>AddDispatchPipeline</c> + <c>AddDefaultDispatchPipelines</c>), letting each test add its own
    ///     handlers/stores/options via <paramref name="configure"/>. The observable store (when registered) is
    ///     added BEFORE the pipeline so the framework's TryAdd-based default wiring defers to it.
    /// </summary>
    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(static b => b.SetMinimumLevel(LogLevel.Warning));

        // Test-specific registrations (stores, handlers, options) BEFORE the pipeline so TryAdd defers to them.
        configure(services);

        // Core Dispatch services: IDispatcher, IMessageContextFactory/Accessor, LocalMessageBus.
        services.AddDispatchPipeline();

        // The real default pipeline under test: wires the default profile (incl. OutboxStagingMiddleware).
        services.AddDefaultDispatchPipelines();

        services.AddDispatchHandlers();

        return services.BuildServiceProvider();
    }
}

#region Test messages / handlers / observable state (self-contained — no coupling to the S848 T1 harness)

/// <summary>Command whose handler stages a <see cref="WidgetOrderPlaced"/> event via the real outbox writer.</summary>
public sealed record PlaceWidgetOrder : IDispatchAction
{
    public string OrderId { get; init; } = string.Empty;
}

/// <summary>Domain event staged into the outbox by the command handler.</summary>
public sealed class WidgetOrderPlaced : IDispatchEvent
{
    public string OrderId { get; set; } = string.Empty;
}

/// <summary>
///     Handles the command by staging a <see cref="WidgetOrderPlaced"/> event via the real
///     <see cref="IOutboxWriter"/> (DeferredOutboxWriter). On handler success the default pipeline's
///     <c>OutboxStagingMiddleware</c> persists the buffered event to the registered <see cref="IOutboxStore"/>.
/// </summary>
internal sealed class PlaceWidgetOrderHandler(IOutboxWriter outboxWriter, HandlerInvocationState state)
    : IActionHandler<PlaceWidgetOrder>
{
    public async Task HandleAsync(PlaceWidgetOrder action, CancellationToken cancellationToken)
    {
        state.Record(action.OrderId);
        await outboxWriter
            .WriteAsync(new WidgetOrderPlaced { OrderId = action.OrderId }, destination: "widgets", cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Handler that records its invocation but does NOT touch the outbox (for the no-store / fast-path guards).</summary>
internal sealed class NoOutboxWidgetHandler(HandlerInvocationState state) : IActionHandler<PlaceWidgetOrder>
{
    public Task HandleAsync(PlaceWidgetOrder action, CancellationToken cancellationToken)
    {
        state.Record(action.OrderId);
        return Task.CompletedTask;
    }
}

/// <summary>Thread-safe per-order handler invocation counter the tests assert against.</summary>
internal sealed class HandlerInvocationState
{
    private readonly ConcurrentDictionary<string, int> _invocations = new(StringComparer.Ordinal);

    public void Record(string orderId) => _invocations.AddOrUpdate(orderId, 1, static (_, c) => c + 1);

    public int InvocationCount(string orderId) => _invocations.GetValueOrDefault(orderId, 0);
}

/// <summary>Observable flag set when <see cref="ObservableMarkerMiddleware"/> executes (sibling-guard signal).</summary>
internal sealed class MiddlewareExecutionMarker
{
    private int _executed;

    public bool Executed => Volatile.Read(ref _executed) != 0;

    public void Mark() => Volatile.Write(ref _executed, 1);
}

/// <summary>
///     Minimal, fully-public test middleware that records its own execution into a
///     <see cref="MiddlewareExecutionMarker"/> and forwards the pipeline. Used by the Option-A sibling guard to
///     observe — without any internal/production dependency — whether a profile-configured pipeline's middleware
///     actually reaches the dispatcher invoker and runs on <c>DispatchAsync</c>.
/// </summary>
internal sealed class ObservableMarkerMiddleware(MiddlewareExecutionMarker marker) : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public MessageKinds ApplicableMessageKinds => MessageKinds.All;

    public ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        marker.Mark();
        return nextDelegate(message, context, cancellationToken);
    }
}

/// <summary>
///     A real, observable in-memory <see cref="IOutboxStore"/> the production <c>OutboxStagingMiddleware</c>
///     stages into; the tests poll <see cref="StagedMessages"/> to observe real staged state.
/// </summary>
internal sealed class ObservableOutboxStore : IOutboxStore
{
    private readonly ConcurrentDictionary<string, OutboundMessage> _messages = new(StringComparer.Ordinal);

    public IReadOnlyCollection<OutboundMessage> StagedMessages =>
        _messages.Values.Where(static m => m.Status == OutboxStatus.Staged).ToList();

    public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        _messages[message.Id] = message;
        return ValueTask.CompletedTask;
    }

    public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
        => ValueTask.FromResult<IEnumerable<OutboundMessage>>(
            _messages.Values.Where(static m => m.Status == OutboxStatus.Staged).Take(batchSize).ToList());

    public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.MarkSent();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.Status = OutboxStatus.Failed;
            msg.LastError = errorMessage;
            msg.RetryCount = retryCount;
        }

        return ValueTask.CompletedTask;
    }
}

#endregion
