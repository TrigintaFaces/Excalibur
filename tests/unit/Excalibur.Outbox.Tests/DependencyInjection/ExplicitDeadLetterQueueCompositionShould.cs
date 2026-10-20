// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Serialization;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;
using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Configuration")]
public sealed class ExplicitDeadLetterQueueCompositionShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseDefaultProcessorWhenQueueWasNotExplicitlyChosen(bool inbox)
    {
        await using var provider = CreateServices(inbox).BuildServiceProvider();
        var exception = Should.Throw<InvalidOperationException>(() => Resolve(provider, inbox));
        exception.Message.ShouldContain("IDeadLetterQueue");
        exception.Message.ShouldContain("NullDeadLetterQueue.Instance");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseDirectConstructionBeforeReadingOptions(bool inbox)
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var exception = Should.Throw<InvalidOperationException>(() => Construct(provider, inbox, null, throwingOptions: true));
        exception.Message.ShouldContain("IDeadLetterQueue");
        exception.Message.ShouldContain("NullDeadLetterQueue.Instance");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AcceptQueueRegisteredAfterSubsystem(bool inbox, bool explicitDiscard)
    {
        var services = CreateServices(inbox);
        services.AddSingleton<IDeadLetterQueue>(explicitDiscard ? NullDeadLetterQueue.Instance : A.Fake<IDeadLetterQueue>());
        await using var provider = services.BuildServiceProvider();
        Resolve(provider, inbox).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveConsumerProcessorWithoutImposingDefaultQueueContract(bool inbox)
    {
        var services = new ServiceCollection();
        object processor;
        if (inbox)
        {
            var custom = A.Fake<IInboxProcessor>();
            services.AddSingleton(custom);
            processor = custom;
        }
        else
        {
            var custom = A.Fake<IOutboxProcessor>();
            services.AddSingleton(custom);
            processor = custom;
        }
        RegisterSubsystem(services, inbox);
        await using var provider = services.BuildServiceProvider();
        Resolve(provider, inbox).ShouldBeSameAs(processor);
    }

    [Fact]
    public async Task LeaveStoreOnlyCompositionAvailableWithoutAQueue()
    {
        var store = A.Fake<IOutboxStore>();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddExcalibur(builder => builder.AddOutbox(_ => { }));
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOutboxStore>().ShouldBeSameAs(store);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportExplicitDiscardRatherThanAMissingQueue(bool inbox)
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var messages = new List<string>();
        await using IAsyncDisposable processor = inbox
            ? new InboxProcessor(Options.Create(new InboxOptions()), A.Fake<IInboxStore>(), provider,
                new DispatchJsonSerializer(), new CapturingLogger<InboxProcessor>(messages),
                PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: NullDeadLetterQueue.Instance)
            : new OutboxProcessor(Options.Create(DeliveryOutboxOptions.Balanced()), A.Fake<IOutboxStore>(),
                new DispatchJsonSerializer(), provider, new CapturingLogger<OutboxProcessor>(messages),
                PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: NullDeadLetterQueue.Instance);
        var warning = messages.Single(message => message.Contains("NullDeadLetterQueue", StringComparison.Ordinal));
        warning.ShouldContain("explicitly selected");
        warning.ShouldContain("discarded");
        warning.ShouldNotContain("No IDeadLetterQueue registered");
    }

    private static ServiceCollection CreateServices(bool inbox)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(A.Fake<IOutboxStore>());
        services.AddSingleton(A.Fake<IInboxStore>());
        services.AddSingleton(Options.Create(new InboxOptions()));
        RegisterSubsystem(services, inbox);
        return services;
    }

    private static void RegisterSubsystem(IServiceCollection services, bool inbox)
    {
        if (inbox)
        {
            services.AddInboxHostedService();
        }
        else
        {
            services.AddExcalibur(builder => builder.AddOutbox(_ => { }));
        }
    }

    private static object Resolve(IServiceProvider provider, bool inbox) => inbox
        ? provider.GetRequiredService<IInboxProcessor>()
        : provider.GetRequiredService<IOutboxProcessor>();

    private static object Construct(IServiceProvider provider, bool inbox, IDeadLetterQueue? queue, bool throwingOptions) => inbox
        ? new InboxProcessor(throwingOptions ? new UnreadableOptions<InboxOptions>() : Options.Create(new InboxOptions()),
            A.Fake<IInboxStore>(), provider, new DispatchJsonSerializer(), NullLogger<InboxProcessor>.Instance,
            PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: queue)
        : new OutboxProcessor(throwingOptions ? new UnreadableOptions<DeliveryOutboxOptions>() : Options.Create(DeliveryOutboxOptions.Balanced()),
            A.Fake<IOutboxStore>(), new DispatchJsonSerializer(), provider, NullLogger<OutboxProcessor>.Instance,
            PassThroughCircuitBreakerRegistry.Instance, deadLetterQueue: queue);

    private sealed class UnreadableOptions<T> : IOptions<T> where T : class
    {
        public T Value => throw new InvalidOperationException("Options were read before the queue prerequisite.");
    }

    private sealed class CapturingLogger<T>(List<string> warnings) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                warnings.Add(formatter(state, exception));
            }
        }
    }
}
