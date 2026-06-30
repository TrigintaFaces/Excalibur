// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Options.Configuration;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Configuration)]
public sealed class DispatchConfigurationServiceCollectionExtensionsShould
{
	[Fact]
	public void AddHandlerRegisterInterfaceAndConcreteWithRequestedLifetime()
	{
		var services = new ServiceCollection();

		services.AddHandler<SampleMessage, SampleHandler>(ServiceLifetime.Singleton);

		services.ShouldContain(descriptor =>
			descriptor.ServiceType == typeof(IDispatchHandler<SampleMessage>) &&
			descriptor.ImplementationType == typeof(SampleHandler) &&
			descriptor.Lifetime == ServiceLifetime.Singleton);

		services.ShouldContain(descriptor =>
			descriptor.ServiceType == typeof(SampleHandler) &&
			descriptor.ImplementationType == typeof(SampleHandler) &&
			descriptor.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddMiddlewareRegisterInterfaceAndConcreteWithRequestedLifetime()
	{
		var services = new ServiceCollection();

		services.AddMiddleware<SampleMiddleware>(ServiceLifetime.Transient);

		// S717 T.2: middleware registered as concrete type only
		services.ShouldContain(descriptor =>
			descriptor.ServiceType == typeof(SampleMiddleware) &&
			descriptor.ImplementationType == typeof(SampleMiddleware) &&
			descriptor.Lifetime == ServiceLifetime.Transient);
	}

	[Fact]
	public void AddUpcastingMessageBusDecoratorSkipWhenPipelineIsMissing()
	{
		var services = new ServiceCollection();
		var bus = new RecordingMessageBus();
		services.AddSingleton<IMessageBus>(bus);

		services.AddUpcastingMessageBusDecorator();
		using var provider = services.BuildServiceProvider();

		var resolved = provider.GetRequiredService<IMessageBus>();
		resolved.ShouldBeSameAs(bus);
	}

	[Fact]
	public void AddUpcastingMessageBusDecoratorSkipWhenNoMessageBusIsRegistered()
	{
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IUpcastingPipeline>());

		services.AddUpcastingMessageBusDecorator();
		using var provider = services.BuildServiceProvider();

		provider.GetService<IMessageBus>().ShouldBeNull();
	}

	[Fact]
	public async Task AddUpcastingMessageBusDecoratorDecorateBusOnceAndRemainIdempotent()
	{
		var services = new ServiceCollection();
		var innerBus = new RecordingMessageBus();
		services.AddSingleton<IMessageBus>(innerBus);
		services.AddSingleton(A.Fake<IUpcastingPipeline>());

		services.AddUpcastingMessageBusDecorator();
		services.AddUpcastingMessageBusDecorator();

		services.Count(descriptor => descriptor.ServiceType == typeof(IMessageBus)).ShouldBe(1);

		using var provider = services.BuildServiceProvider();
		var decorated = provider.GetRequiredService<IMessageBus>();
		decorated.ShouldBeOfType<UpcastingMessageBusDecorator>();

		await decorated.PublishAsync(new SampleAction(), DispatchContextInitializer.CreateDefaultContext(), CancellationToken.None)
			.ConfigureAwait(false);

		innerBus.ActionPublishes.ShouldBe(1);
	}

	[Fact]
	public async Task AddUpcastingMessageBusDecoratorDecorateKeyedRegistrations()
	{
		var services = new ServiceCollection();
		var keyedBus = new RecordingMessageBus();
		services.AddKeyedSingleton<IMessageBus>("primary", keyedBus);
		services.AddSingleton(A.Fake<IUpcastingPipeline>());

		services.AddUpcastingMessageBusDecorator();
		using var provider = services.BuildServiceProvider();

		var decorated = provider.GetRequiredKeyedService<IMessageBus>("primary");
		decorated.ShouldBeOfType<UpcastingMessageBusDecorator>();

		await decorated.PublishAsync(new SampleAction(), DispatchContextInitializer.CreateDefaultContext(), CancellationToken.None)
			.ConfigureAwait(false);

		keyedBus.ActionPublishes.ShouldBe(1);
	}

	[Fact]
	public async Task AddUpcastingMessageBusDecoratorResolveKeyedRegistrationByImplementationType()
	{
		// AC-1 (bd-ib1kxp): a keyed IMessageBus registered by keyed implementation TYPE (no keyed instance)
		// MUST resolve through KeyedImplementationType. Pre-fix, the keyed branch of CreateInnerMessageBus
		// read the NON-keyed descriptor.ImplementationInstance getter on a keyed descriptor, which throws
		// InvalidOperationException on .NET 8+ — so this resolution would fail before reaching the keyed type.
		var services = new ServiceCollection();
		services.AddKeyedSingleton<IMessageBus, RecordingMessageBus>("typed");
		services.AddSingleton(A.Fake<IUpcastingPipeline>());

		services.AddUpcastingMessageBusDecorator();
		using var provider = services.BuildServiceProvider();

		var decorated = provider.GetRequiredKeyedService<IMessageBus>("typed");
		decorated.ShouldBeOfType<UpcastingMessageBusDecorator>();

		// The decorated inner bus is activated from the keyed type; publishing must not throw.
		await decorated.PublishAsync(new SampleAction(), DispatchContextInitializer.CreateDefaultContext(), CancellationToken.None)
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task AddUpcastingMessageBusDecoratorResolveKeyedRegistrationByImplementationFactory()
	{
		// AC-2 (bd-ib1kxp): a keyed IMessageBus registered by keyed FACTORY MUST resolve through the keyed
		// factory without ever reading a non-keyed accessor (pre-fix the keyed branch threw before reaching it).
		var services = new ServiceCollection();
		var keyedBus = new RecordingMessageBus();
		services.AddKeyedSingleton<IMessageBus>("factory", (_, _) => keyedBus);
		services.AddSingleton(A.Fake<IUpcastingPipeline>());

		services.AddUpcastingMessageBusDecorator();
		using var provider = services.BuildServiceProvider();

		var decorated = provider.GetRequiredKeyedService<IMessageBus>("factory");
		decorated.ShouldBeOfType<UpcastingMessageBusDecorator>();

		await decorated.PublishAsync(new SampleAction(), DispatchContextInitializer.CreateDefaultContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// The keyed factory's instance is the one the decorator wraps.
		keyedBus.ActionPublishes.ShouldBe(1);
	}

	[Fact]
	public async Task AddUpcastingMessageBusDecoratorIgnoreUnrelatedKeyedDescriptors()
	{
		// AC-3 (bd-ib1kxp, edge): an unrelated keyed descriptor in the collection MUST NOT cause the
		// decorator registration or the message-bus enumeration to throw — the enumeration sites guard on
		// ServiceType == typeof(IMessageBus) first, short-circuiting before any keyed-accessor read.
		var services = new ServiceCollection();
		services.AddKeyedSingleton("unrelated", "a-plain-keyed-string");
		var keyedBus = new RecordingMessageBus();
		services.AddKeyedSingleton<IMessageBus>("primary", keyedBus);
		services.AddSingleton(A.Fake<IUpcastingPipeline>());

		// Decoration must succeed despite the unrelated keyed descriptor being present.
		services.AddUpcastingMessageBusDecorator();
		using var provider = services.BuildServiceProvider();

		var decorated = provider.GetRequiredKeyedService<IMessageBus>("primary");
		decorated.ShouldBeOfType<UpcastingMessageBusDecorator>();

		await decorated.PublishAsync(new SampleAction(), DispatchContextInitializer.CreateDefaultContext(), CancellationToken.None)
			.ConfigureAwait(false);

		keyedBus.ActionPublishes.ShouldBe(1);

		// The unrelated keyed service is untouched and still resolvable.
		provider.GetRequiredKeyedService<string>("unrelated").ShouldBe("a-plain-keyed-string");
	}

	[Fact]
	public void AddDispatchCanConfigureExpectedLightModeOptions()
	{
		var services = new ServiceCollection();
		services.AddDispatch(builder =>
		{
			_ = builder.WithOptions(options =>
			{
				options.UseLightMode = true;
				options.Inbox.Enabled = false;
				options.Consumer.Dedupe.Enabled = true;
				options.Outbox.UseInMemoryStorage = true;
			});
		});
		using var provider = services.BuildServiceProvider();

		var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DispatchOptions>>().Value;
		options.UseLightMode.ShouldBeTrue();
		options.Inbox.Enabled.ShouldBeFalse();
		options.Consumer.Dedupe.Enabled.ShouldBeTrue();
		options.Outbox.UseInMemoryStorage.ShouldBeTrue();
	}

	[Fact]
	public void AddDispatchWithDurabilityConfigureExpectedDispatchOptions()
	{
		var services = new ServiceCollection();
		services.AddDispatchWithDurability();
		using var provider = services.BuildServiceProvider();

		var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DispatchOptions>>().Value;
		options.UseLightMode.ShouldBeFalse();
		options.Inbox.Enabled.ShouldBeTrue();
		options.Consumer.Dedupe.Enabled.ShouldBeFalse();
		options.Outbox.UseInMemoryStorage.ShouldBeFalse();
		options.Outbox.MaxRetries.ShouldBe(10);
		options.Outbox.SentMessageRetention.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void AddDefaultDispatchPipelinesRegisterSynthesizedDefaultMiddleware()
	{
		var services = new ServiceCollection();
		services.AddDefaultDispatchPipelines();
		using var provider = services.BuildServiceProvider();

		var synthesizer = provider.GetRequiredService<IDefaultPipelineSynthesizer>();
		var pipeline = synthesizer.SynthesizePipeline(MessageKinds.All, new DispatchOptions());

		pipeline.ShouldContain(typeof(TransportRouterMiddleware));
		pipeline.ShouldContain(typeof(InboxMiddleware));
		pipeline.ShouldContain(typeof(OutboxStagingMiddleware));
	}

	private sealed class RecordingMessageBus : IMessageBus
	{
		private int _actionPublishes;

		public int ActionPublishes => Volatile.Read(ref _actionPublishes);

		public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _actionPublishes);
			return Task.CompletedTask;
		}

		public Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken) => Task.CompletedTask;

		public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class SampleMessage : IDispatchMessage;

	private sealed class SampleAction : IDispatchAction;

	private sealed class SampleHandler : IDispatchHandler<SampleMessage>
	{
		public Task<IMessageResult> HandleAsync(
			SampleMessage message,
			IMessageContext context,
			CancellationToken cancellationToken) =>
			Task.FromResult<IMessageResult>(MessageResult.Success());
	}

	private sealed class SampleMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			new(MessageResult.Success());
	}
}