// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Outbox.InMemory;
using Excalibur.Outbox.Outbox;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OutboxProcessingDefaultsShould
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Enforce_Builder_Attempt_Budget_And_Delay_In_The_Actual_Drain(int retries)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>(Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance);
        services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory().WithProcessing(processing =>
            processing.BatchSize(7).MaxRetryCount(retries).PollingInterval(TimeSpan.FromMilliseconds(20)).RetryDelay(TimeSpan.FromSeconds(5)))));
        services.Configure<InMemoryOutboxOptions>(options => options.FailureBackoffFloorSeconds = 1);
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IMessageResult>(Excalibur.Dispatch.MessageResult.Failed("controlled-before-effect")));
        services.AddSingleton(dispatcher);
        services.AddSingleton<IOutboxStore>(provider => new ObservedStore(provider.GetRequiredService<InMemoryOutboxStore>()));
        await using var provider = services.BuildServiceProvider();
        var store = (ObservedStore)provider.GetRequiredService<IOutboxStore>();
        MessageTypeRegistry.RegisterType<BudgetEvent>();
        var message = new OutboundMessage(typeof(BudgetEvent).AssemblyQualifiedName!, JsonSerializer.SerializeToUtf8Bytes(new BudgetEvent("budget")), "local");
        await store.StageMessageAsync(message, CancellationToken.None);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("budget-test");
        var before = DateTimeOffset.UtcNow;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        var after = DateTimeOffset.UtcNow;
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        store.ClaimSizes.First().ShouldBe(7);
        PersistedInMemoryOutboxState.Read(provider, message.Id).Status.ShouldBe(retries == 0 ? OutboxStatus.DeadLettered : OutboxStatus.Failed);
        if (retries == 1)
        {
            PersistedInMemoryOutboxState.Read(provider, message.Id).RetryCount.ShouldBe(1);
            var schedule = store.Schedules.Single();
            var captured = store.Claims.Single();
            captured.Id.ShouldBe(message.Id);
            captured.Claim.ShouldNotBeNullOrWhiteSpace();
            store.StrongBackoffClaims.Single().ShouldBe(captured);
            schedule.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(5));
            schedule.ShouldBeLessThanOrEqualTo(after.AddSeconds(5));
            await Task.Delay(TimeSpan.FromMilliseconds(1200), deadline.Token); // delay-ok: part of the default 5s schedule must elapse; the assertion is that the message is STILL not due
            (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        }
        else
        {
            store.Schedules.ShouldBeEmpty();
            (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        }
        store.WeakWriteCount.ShouldBe(0, "real drain completion must not downgrade to ID-only writes");
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Preserve_Consumer_Calculator_In_Either_Registration_Order(bool registerFirst)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>(Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance);
        var calculator = A.Fake<IBackoffCalculator>();
        A.CallTo(() => calculator.CalculateDelay(A<int>._)).Returns(TimeSpan.FromSeconds(8));
        if (registerFirst) services.AddSingleton(calculator);
        services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory().WithProcessing(processing =>
            processing.MaxRetryCount(1).RetryDelay(TimeSpan.FromSeconds(5)))));
        if (!registerFirst) services.AddSingleton(calculator);
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IMessageResult>(Excalibur.Dispatch.MessageResult.Failed("controlled-before-effect")));
        services.AddSingleton(dispatcher);
        await using var provider = services.BuildServiceProvider();
        MessageTypeRegistry.RegisterType<BudgetEvent>();
        var store = provider.GetRequiredService<IOutboxStore>();
        await store.StageMessageAsync(new OutboundMessage(typeof(BudgetEvent).AssemblyQualifiedName!, JsonSerializer.SerializeToUtf8Bytes(new BudgetEvent("calculator")), "local"), CancellationToken.None);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("calculator-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        A.CallTo(() => calculator.CalculateDelay(1)).MustHaveHappenedOnceExactly();
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

	private static IOutboxStore ConstructorStore()
	{
		var store = A.Fake<IOutboxStore>(b => b.Implements<IClaimScopedOutboxStore>()
			.Implements<IClaimScopedDeadLetterableOutboxStore>());
		A.CallTo(() => store.GetService(A<Type>._))
			.ReturnsLazily((Type type) => type.IsInstanceOfType(store) ? store : null);
		return store;
	}

    private sealed record BudgetEvent(string Value) : IIntegrationEvent;

    private sealed class ObservedStore(IOutboxStore inner) : OutboxStoreDecorator(inner)
    {
        internal ConcurrentQueue<int> ClaimSizes { get; } = new();
        internal ConcurrentQueue<DateTimeOffset> Schedules { get; } = new();
        internal ConcurrentQueue<(string Id, string Claim)> Claims { get; } = new();
        internal ConcurrentQueue<(string Id, string Claim)> StrongBackoffClaims { get; } = new();
        private int _weakWrites;
        internal int WeakWriteCount => Volatile.Read(ref _weakWrites);
        public override async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
        {
            ClaimSizes.Enqueue(batchSize);
            var rows = (await base.GetUnsentMessagesAsync(batchSize, cancellationToken).ConfigureAwait(false)).ToArray();
            foreach (var row in rows) { Claims.Enqueue((row.Id, row.DispatcherId!)); }
            return rows;
        }
        public override object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IClaimScopedOutboxStore))
            {
                return Inner.GetService(serviceType) is IClaimScopedOutboxStore claimScoped
                    ? new ObservedClaimBackoff(claimScoped, Schedules, StrongBackoffClaims) : null;
            }
            return serviceType == typeof(IBackoffSchedulableOutboxStore)
                ? Inner.GetService(serviceType) is IBackoffSchedulableOutboxStore schedulable
                    ? new ObservedBackoff(schedulable, Schedules, () => Interlocked.Increment(ref _weakWrites)) : null
                : base.GetService(serviceType);
        }

        public override ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _weakWrites);
            return base.MarkSentAsync(messageId, cancellationToken);
        }

        public override ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _weakWrites);
            return base.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken);
        }

        private sealed class ObservedClaimBackoff(IClaimScopedOutboxStore inner, ConcurrentQueue<DateTimeOffset> schedules,
            ConcurrentQueue<(string Id, string Claim)> claims) : IClaimScopedOutboxStore
        {
            public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string id, string claim, CancellationToken token)
                => inner.MarkSentAsync(id, claim, token);
            public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string id, string error, int attempts, string claim, CancellationToken token)
                => inner.MarkFailedAsync(id, error, attempts, claim, token);
            public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(string id, string error, int attempts, DateTimeOffset nextAttemptAt, string claim, CancellationToken token)
            {
                schedules.Enqueue(nextAttemptAt);
                claims.Enqueue((id, claim));
                return inner.MarkFailedWithBackoffAsync(id, error, attempts, nextAttemptAt, claim, token);
            }
        }

        private sealed class ObservedBackoff(IBackoffSchedulableOutboxStore inner, ConcurrentQueue<DateTimeOffset> schedules, Action weakWrite) : IBackoffSchedulableOutboxStore
        {
            public ValueTask MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken)
            {
                weakWrite();
                schedules.Enqueue(nextAttemptAt);
                return inner.MarkFailedWithBackoffAsync(messageId, errorMessage, retryCount, nextAttemptAt, cancellationToken);
            }
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 4)]
    [InlineData(int.MaxValue - 1, int.MaxValue)]
    public async Task Apply_Builder_Defaults_To_The_Registered_Processor(int retries, int attempts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>(Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance);
        services.AddSingleton(ConstructorStore());
        services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.WithProcessing(processing =>
            processing.BatchSize(7).MaxRetryCount(retries).RetryDelay(TimeSpan.FromSeconds(5)))));
        await using var provider = services.BuildServiceProvider();
        var delivery = provider.GetRequiredService<IOptions<OutboxDeliveryOptions>>().Value;
        delivery.ProducerBatchSize.ShouldBe(7);
        delivery.ConsumerBatchSize.ShouldBe(7);
        delivery.MaxAttempts.ShouldBe(attempts);
        provider.GetRequiredService<IOutboxProcessor>().ShouldBeOfType<Excalibur.Dispatch.Delivery.OutboxProcessor>();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Preserve_Explicit_Configuration_In_Either_Order(bool configureFirst, bool postConfigure)
    {
        var services = new ServiceCollection();
        void ConfigureConsumer()
        {
            void Delivery(OutboxDeliveryOptions options)
            {
                options.ProducerBatchSize = 11;
                options.ConsumerBatchSize = 13;
                options.MaxAttempts = 5; // Explicitly choosing the original default must still win.
                options.PerRunTotal = 19;
                options.BatchProcessing.ParallelProcessingDegree = 2;
            }
            void Processing(OutboxProcessingOptions options) => options.PollingInterval = TimeSpan.FromSeconds(9);
            if (postConfigure)
            {
                services.PostConfigure<OutboxDeliveryOptions>(Delivery);
                services.PostConfigure<OutboxProcessingOptions>(Processing);
            }
            else
            {
                services.Configure<OutboxDeliveryOptions>(Delivery);
                services.Configure<OutboxProcessingOptions>(Processing);
            }
        }
        if (configureFirst) ConfigureConsumer();
        services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.WithProcessing(processing =>
            processing.BatchSize(7).MaxRetryCount(0).PollingInterval(TimeSpan.FromSeconds(2)).EnableParallelProcessing(4))));
        if (!configureFirst) ConfigureConsumer();
        services.AddExcaliburOutbox(); // Repeated core registration cannot overwrite the consumer.
        using var provider = services.BuildServiceProvider();
        var delivery = provider.GetRequiredService<IOptions<OutboxDeliveryOptions>>().Value;
        delivery.ProducerBatchSize.ShouldBe(11);
        delivery.ConsumerBatchSize.ShouldBe(13);
        delivery.MaxAttempts.ShouldBe(5);
        delivery.PerRunTotal.ShouldBe(19);
        delivery.BatchProcessing.ParallelProcessingDegree.ShouldBe(2);
        provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value.PollingInterval.ShouldBe(TimeSpan.FromSeconds(9));
    }

    [Fact]
    public void Reject_Unrepresentable_Default_Total_Without_Restricting_The_Facade()
    {
        var services = new ServiceCollection();
        services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.WithProcessing(processing => processing.MaxRetryCount(int.MaxValue))));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<OutboxOptions>().Retry.MaxRetryCount.ShouldBe(int.MaxValue);
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OutboxDeliveryOptions>>().Value)
            .Message.ShouldContain("MaxAttempts");
    }

    [Theory]
    [InlineData(true, 5)]
    [InlineData(false, 5)]
    [InlineData(true, int.MaxValue)]
    [InlineData(false, int.MaxValue)]
    public void Allow_Explicit_Total_To_Override_Unrepresentable_Default(bool configureFirst, int attempts)
    {
        var services = new ServiceCollection();
        if (configureFirst) services.Configure<OutboxDeliveryOptions>(options => options.MaxAttempts = attempts);
        services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.WithProcessing(processing => processing.MaxRetryCount(int.MaxValue))));
        if (!configureFirst) services.PostConfigure<OutboxDeliveryOptions>(options => options.MaxAttempts = attempts);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<OutboxDeliveryOptions>>().Value.MaxAttempts.ShouldBe(attempts);
    }
}
