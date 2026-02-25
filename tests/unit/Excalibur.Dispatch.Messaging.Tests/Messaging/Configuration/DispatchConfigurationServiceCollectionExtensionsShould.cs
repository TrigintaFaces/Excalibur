// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Options.Configuration;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

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

		services.ShouldContain(descriptor =>
			descriptor.ServiceType == typeof(IDispatchMiddleware) &&
			descriptor.ImplementationType == typeof(SampleMiddleware) &&
			descriptor.Lifetime == ServiceLifetime.Transient);

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
