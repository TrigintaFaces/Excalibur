// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// LIVE REGRESSION LOCK -- TenantBridgeCensus (BackendDeveloper), thread n00ucu.
// Follow-up #2: TransportContextFactory.ResolveTenantId's `message is IDomainEvent` gate meant an
// IDispatchAction (command) received over a real transport adapter never got a tenant threaded onto
// its context, regardless of anything ambiently established (Excalibur_Dispatch-nkoky7).
//
// CORRECTED after an independent (Lamport) review found the first fix wrong at the VALUE: resolving
// the fallback through ITenantContext meant the shipped default registration (SingleTenantContext, a
// FIXED constant that ignores the ambient holder) would stamp __default__ unconditionally, even on a
// host with no ambient tenant established at all. Corrected design: TransportContextFactory.
// CreateForReceive/CreateForSend call ApplyAmbientTenantFallback(), which reads
// TenantContextHolder.Current directly -- registration-independent, absence stays absence. These
// tests bind the DEFAULT container (AddDispatch() only, no tenancy call at all).
//
// Measured through the REAL, unmocked RabbitMQTransportAdapter.ReceiveAsync -- the actual production
// method every inbound RabbitMQ message goes through, which internally calls the actual internal
// TransportContextFactory.CreateForReceive (the same shared function all 5 transport adapter families
// route through -- RabbitMQ, Kafka, AwsSqs/Sns/EventBridge, AzureServiceBus/EventHubs, GooglePubSub).
// Only the RabbitMQ.Client wire boundary (IChannel, IPayloadSerializer) is faked -- both are unused by
// ReceiveAsync(object, IDispatcher, ct), which takes an already-deserialized message and never touches
// the channel or serializer. Every line of tenant-propagation logic under test is real, unmocked
// production code. Kept (not deleted) per PM direction -- intended regression-lock skeleton.

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.RabbitMQ;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.RabbitMQ")]
public sealed class N00ucuTransportActionTenantProof
{
	private sealed record PlainCommand : IDispatchAction;

	private sealed class Capture
	{
		public bool HandlerInvoked { get; set; }
		public string? ContextTenantId { get; set; }
	}

	private sealed class PlainCommandHandler(Capture capture) : IActionHandler<PlainCommand>
	{
		public Task HandleAsync(PlainCommand action, CancellationToken cancellationToken)
		{
			capture.HandlerInvoked = true;
			capture.ContextTenantId = MessageContextHolder.Current?.GetTenantId();
			return Task.CompletedTask;
		}
	}

	private static RabbitMQTransportAdapter BuildRealAdapter(IServiceProvider serviceProvider)
	{
		// Real RabbitMqMessageBus, real RabbitMQTransportAdapter -- only the RabbitMQ.Client wire
		// boundary (IChannel) and the payload serializer are faked. Neither is touched by
		// ReceiveAsync(object transportMessage, IDispatcher, ct): it takes an already-deserialized
		// IDispatchMessage and calls straight into TransportContextFactory.CreateForReceive.
		var channel = A.Fake<IChannel>();
		var serializer = A.Fake<IPayloadSerializer>();
		var options = new RabbitMqOptions { Exchange = "test-exchange", RoutingKey = "test-key" };
		var busLogger = NullLoggerFactory().CreateLogger<RabbitMqMessageBus>();
		var adapterLogger = NullLoggerFactory().CreateLogger<RabbitMQTransportAdapter>();

		var messageBus = new RabbitMqMessageBus(
			channel, serializer, Microsoft.Extensions.Options.Options.Create(options), busLogger);
		return new RabbitMQTransportAdapter(adapterLogger, messageBus, serviceProvider);
	}

	private static ILoggerFactory NullLoggerFactory() =>
		Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

	[Fact]
	public async Task ReceiveAsync_PlainIDispatchAction_GetsAmbientTenantThreadedOntoContext_OnTheDefaultContainer()
	{
		// Arrange: real DI, real dispatcher, real middleware pipeline -- not mocks. DEFAULT container:
		// AddDispatch() only, no AddTenantContext()/AddMultiTenancy() call at all -- the container a
		// consumer actually receives.
		var capture = new Capture();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(capture);
		services.AddSingleton<IActionHandler<PlainCommand>, PlainCommandHandler>();
		_ = services.AddDispatch(typeof(N00ucuTransportActionTenantProof).Assembly);

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var adapter = BuildRealAdapter(provider);
		await adapter.StartAsync(CancellationToken.None);

		const string realTenantId = "tenant-42";

		// Act: receive a PLAIN IDispatchAction -- no IDomainEvent, no Metadata dictionary at all --
		// through the REAL adapter's REAL ReceiveAsync, with a real ambient tenant active at the
		// moment of receipt (the shape a consumer would reasonably expect to "just work", the same
		// way DispatcherWebExtensions makes it work for the HTTP-hosted path).
		IMessageResult receiveResult;
		using (TenantContextHolder.BeginScope(realTenantId))
		{
			receiveResult = await adapter.ReceiveAsync(new PlainCommand(), dispatcher, CancellationToken.None);
		}

		// Diagnostics first.
		receiveResult.Succeeded.ShouldBeTrue($"receive failed: {receiveResult.ErrorMessage}");
		capture.HandlerInvoked.ShouldBeTrue("PlainCommandHandler was never invoked");

		// THE MEASUREMENT: TransportContextFactory.ResolveTenantId still only inspects
		// `message is IDomainEvent { Metadata: {} m } && m.TryGetValue("TenantId", ...)` -- structurally
		// unreachable for a plain IDispatchAction. CreateForReceive now calls
		// ApplyAmbientTenantFallback() right after, reading TenantContextHolder.Current directly, so
		// the ambient tenant this test established is what actually reaches the handler's context --
		// on the DEFAULT container, no ITenantContext registered at all.
		capture.ContextTenantId.ShouldBe(realTenantId);
	}

	[Fact]
	public async Task ReceiveAsync_PlainIDispatchAction_StaysUntenanted_WhenNoAmbientTenantEstablished_EvenWithSingleTenantContextRegistered()
	{
		// THE REGRESSION THIS TEST EXISTS TO CATCH (found by independent review after the first fix
		// shipped): a fallback sourced from ITenantContext ignores the ambient holder entirely on the
		// shipped default registration (SingleTenantContext.TenantId is a FIXED constant,
		// TenantDefaults.DefaultTenantId, that never consults TenantContextHolder). That would stamp
		// __default__ on every command received with no ambient tenant established, splitting existing
		// outbox streams across a deploy: pre-fix untenanted rows fold to __untenanted__; a wrongly
		// "fixed" version would land new rows at __default__ -- two different partitions for what used
		// to be one.
		//
		// This test registers AddDefaultTenantContext() explicitly (SingleTenantContext, exactly the
		// shipped default for a host that has not opted into multi-tenancy) and establishes NO ambient
		// tenant at all -- proving the corrected, TenantContextHolder-direct fallback does not stamp
		// __default__ regardless of what ITenantContext resolves to.
		var capture = new Capture();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(capture);
		services.AddSingleton<IActionHandler<PlainCommand>, PlainCommandHandler>();
		_ = services.AddDispatch(typeof(N00ucuTransportActionTenantProof).Assembly);
		_ = services.AddDefaultTenantContext(); // registers SingleTenantContext -- TenantId is ALWAYS "__default__"

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var adapter = BuildRealAdapter(provider);
		await adapter.StartAsync(CancellationToken.None);

		// Deliberately no TenantContextHolder.BeginScope(...) around this call.
		var receiveResult = await adapter.ReceiveAsync(new PlainCommand(), dispatcher, CancellationToken.None);

		receiveResult.Succeeded.ShouldBeTrue($"receive failed: {receiveResult.ErrorMessage}");
		capture.HandlerInvoked.ShouldBeTrue("PlainCommandHandler was never invoked");

		// THE MEASUREMENT: must be null (untenanted), NEVER "__default__" -- despite SingleTenantContext
		// being registered and its ITenantContext.TenantId always answering "__default__".
		capture.ContextTenantId.ShouldBeNull();
	}

	[Fact]
	public async Task ReceiveAsync_PrefersDomainEventMetadataTenantId_OverAmbientTenant()
	{
		// The metadata-carried, message-level source must win -- a more specific source than the
		// ambient fallback.
		var capture = new Capture();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(capture);
		services.AddSingleton<IEventHandler<TenantMetadataDomainEvent>, TenantMetadataDomainEventHandler>();
		_ = services.AddDispatch(typeof(N00ucuTransportActionTenantProof).Assembly);

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var adapter = BuildRealAdapter(provider);
		await adapter.StartAsync(CancellationToken.None);

		// Ambient tenant is "tenant-ambient"; the domain event's own metadata carries
		// "tenant-explicit" -- the metadata-carried, message-level value must be the one threaded.
		IMessageResult receiveResult;
		using (TenantContextHolder.BeginScope("tenant-ambient"))
		{
			var domainEvent = new TenantMetadataDomainEvent
			{
				Metadata = new Dictionary<string, object> { ["TenantId"] = "tenant-explicit" },
			};
			receiveResult = await adapter.ReceiveAsync(domainEvent, dispatcher, CancellationToken.None);
		}

		receiveResult.Succeeded.ShouldBeTrue($"receive failed: {receiveResult.ErrorMessage}");
		capture.HandlerInvoked.ShouldBeTrue("TenantMetadataDomainEventHandler was never invoked");
		capture.ContextTenantId.ShouldBe("tenant-explicit");
	}

	private sealed class TenantMetadataDomainEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(TenantMetadataDomainEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	private sealed class TenantMetadataDomainEventHandler(Capture capture) : IEventHandler<TenantMetadataDomainEvent>
	{
		public Task HandleAsync(TenantMetadataDomainEvent eventMessage, CancellationToken cancellationToken)
		{
			capture.HandlerInvoked = true;
			capture.ContextTenantId = MessageContextHolder.Current?.GetTenantId();
			return Task.CompletedTask;
		}
	}
}
