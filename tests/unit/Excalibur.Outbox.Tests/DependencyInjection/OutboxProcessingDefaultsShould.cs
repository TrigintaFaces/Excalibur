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
        message.Status.ShouldBe(retries == 0 ? OutboxStatus.DeadLettered : OutboxStatus.Failed);
        if (retries == 1)
        {
            message.RetryCount.ShouldBe(1);
            var schedule = store.Schedules.Single();
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

    private sealed record BudgetEvent(string Value) : IIntegrationEvent;

    private sealed class ObservedStore(IOutboxStore inner) : OutboxStoreDecorator(inner)
    {
        internal ConcurrentQueue<int> ClaimSizes { get; } = new();
        internal ConcurrentQueue<DateTimeOffset> Schedules { get; } = new();
        public override ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
        {
            ClaimSizes.Enqueue(batchSize);
            return base.GetUnsentMessagesAsync(batchSize, cancellationToken);
        }
        public override object? GetService(Type serviceType) => serviceType == typeof(IBackoffSchedulableOutboxStore)
            ? Inner.GetService(serviceType) is IBackoffSchedulableOutboxStore schedulable
                ? new ObservedBackoff(schedulable, Schedules) : null
            : base.GetService(serviceType);

        private sealed class ObservedBackoff(IBackoffSchedulableOutboxStore inner, ConcurrentQueue<DateTimeOffset> schedules) : IBackoffSchedulableOutboxStore
        {
            public ValueTask MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken)
            {
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
        services.AddSingleton(A.Fake<IOutboxStore>());
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
