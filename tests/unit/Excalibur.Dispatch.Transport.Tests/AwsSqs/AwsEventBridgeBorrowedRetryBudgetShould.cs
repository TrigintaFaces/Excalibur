// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Runtime;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "Regression")]
public sealed class AwsEventBridgeBorrowedRetryBudgetShould
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task HonorPreCancelledCallerBeforeSerializationOrHttpForEveryOverload(int overload)
	{
		using var handler = new PhysicalHandler(Mode.Success);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Client(http, 1, 0);
		var serializer = A.Fake<IPayloadSerializer>();
		await using var bus = new AwsEventBridgeMessageBus(client, serializer, Options(), NullLogger<AwsEventBridgeMessageBus>.Instance);
		using var caller = new CancellationTokenSource();
		await caller.CancelAsync();
		var context = new MessageContext();
		await Should.ThrowAsync<OperationCanceledException>(() => overload switch
		{
			0 => bus.PublishAsync(A.Fake<IDispatchAction>(), context, caller.Token),
			1 => bus.PublishAsync(A.Fake<IDispatchEvent>(), context, caller.Token),
			_ => bus.PublishAsync(A.Fake<IDispatchDocument>(), context, caller.Token),
		});
		A.CallTo(serializer).MustNotHaveHappened();
		handler.Requests.ShouldBe(0);
		client.Config.MaxErrorRetry.ShouldBe(1);
	}

	[Theory]
	[InlineData(false, 1, 0)]
	[InlineData(false, 0, 1)]
	[InlineData(true, 1, 0)]
	[InlineData(true, 0, 1)]
	public async Task RefuseAnOwnedBorrowedRetryBudgetBeforeAnyPhysicalHttpRequest(bool named, int errorRetries, int staleRetries)
	{
		using var handler = new PhysicalHandler(Mode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Client(http, errorRetries, staleRetries);
		var state = new PublishState();
		await using var provider = Build(client, state, named);
		var row = await StageAsync(provider);
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("owned-borrowed-eventbridge");
		(await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
		state.Calls.ShouldBe(1);
		handler.Requests.ShouldBe(0, "an owned custom SDK retry budget is a configuration refusal before any HTTP call");
		provider.GetRequiredService<AwsEventBridgePersistedAcceptanceShould.RetainingQueue>()
			.AssertRetained(row, DeadLetterReason.UnhandledException, 1);
		client.Config.MaxErrorRetry.ShouldBe(errorRetries);
		client.Config.MaxStaleConnectionRetries.ShouldBe(staleRetries);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveUnownedBorrowedSdkRetriesAsTheOnlyRetryOwner(bool named)
	{
		using var handler = new PhysicalHandler(Mode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Client(http, 1, 0);
		await using var provider = Build(client, new PublishState(), named);
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var fault = await Should.ThrowAsync<AmazonEventBridgeException>(() => Bus(provider, named).PublishAsync(
			new EventBridgeBorrowedRetryEvent("unowned"), new MessageContext(), deadline.Token));
		fault.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		handler.Requests.ShouldBe(2, "the actual SDK must show its configured initial request plus one retry outside an owner");
		client.Config.MaxErrorRetry.ShouldBe(1);
		client.Config.MaxStaleConnectionRetries.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CompleteOneHealthyOwnedAttemptWithOnePhysicalRequest(bool named)
	{
		using var handler = new PhysicalHandler(Mode.Success);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Client(http, 0, 0);
		var state = new PublishState();
		await using var provider = Build(client, state, named);
		_ = await StageAsync(provider);
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("healthy-owned-eventbridge");
		(await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(1);
		(await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
		state.Calls.ShouldBe(1);
		handler.Requests.ShouldBe(1);
		provider.GetRequiredService<AwsEventBridgePersistedAcceptanceShould.RetainingQueue>().Entries.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CancelABlockedOwnedRequestWithoutAnotherRequestOrChargedFailure(bool named)
	{
		using var handler = new PhysicalHandler(Mode.Block);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Client(http, 0, 0);
		var state = new PublishState();
		await using var provider = Build(client, state, named, leaseTimeoutSeconds: 1);
		var row = await StageAsync(provider);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("cancel-owned-eventbridge");
		using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var running = processor.DispatchPendingMessagesAsync(caller.Token);
		try
		{
			_ = await Task.WhenAny(handler.Entered.Task, running).WaitAsync(TimeSpan.FromSeconds(5));
			handler.Entered.Task.IsCompleted.ShouldBeTrue();
			await caller.CancelAsync();
			await Should.ThrowAsync<OperationCanceledException>(() => running);
			handler.Requests.ShouldBe(1);
			state.Calls.ShouldBe(1);
			var store = provider.GetRequiredService<IOutboxStore>();
			var admin = (IOutboxStoreAdmin)store.GetService(typeof(IOutboxStoreAdmin))!;
			var failed = await admin.GetAllTenantsFailedMessagesAsync(10, null, 10, TestContext.Current.CancellationToken);
			failed.ShouldBeEmpty("caller cancellation leaves staged work leased rather than recording delivery failure");
			var statistics = await admin.GetAllTenantsStatisticsAsync(TestContext.Current.CancellationToken);
			statistics.StagedMessageCount.ShouldBe(1);
			statistics.FailedMessageCount.ShouldBe(0);
			statistics.SentMessageCount.ShouldBe(0);
			// Observe the actual stored row through normal lease recovery, not the
			// detached staged object. Claiming does not dispatch another handler/send.
			using var recoveryDeadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
			recoveryDeadline.CancelAfter(TimeSpan.FromSeconds(5));
			OutboundMessage? reclaimed = null;
			while (reclaimed is null)
			{
				reclaimed = (await store.GetUnsentMessagesAsync(1, recoveryDeadline.Token)).SingleOrDefault();
				if (reclaimed is null)
				{
				    await Task.Delay(TimeSpan.FromMilliseconds(25), recoveryDeadline.Token); // delay-ok: Poll actual eligibility and reclaim of the same retained row; this does not dispatch another handler or HTTP request.
				}
			}
			reclaimed.Id.ShouldBe(row.Id);
			reclaimed.Status.ShouldBe(OutboxStatus.Staged);
			reclaimed.RetryCount.ShouldBe(0);
			reclaimed.Payload.ShouldBe(row.Payload);
			handler.Requests.ShouldBe(1);
			state.Calls.ShouldBe(1);
			provider.GetRequiredService<AwsEventBridgePersistedAcceptanceShould.RetainingQueue>().Entries.ShouldBeEmpty();
		}
		finally
		{
			await caller.CancelAsync();
			try { _ = await running; } catch (OperationCanceledException) { }
		}
	}

	[Fact]
	public async Task KeepKnownPermanentEntryFailuresTerminalWithoutRetrying()
	{
		using var handler = new PhysicalHandler(Mode.Permanent);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Client(http, 0, 0);
		var state = new PublishState();
		await using var provider = Build(client, state, named: false);
		var row = await StageAsync(provider);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("permanent-eventbridge");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		(await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
		state.Calls.ShouldBe(1);
		handler.Requests.ShouldBe(1);
		provider.GetRequiredService<AwsEventBridgePersistedAcceptanceShould.RetainingQueue>()
			.AssertRetained(row, DeadLetterReason.UnhandledException, 1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseUnreadableOwnedClientConfigurationBeforeSdkInvocation(bool throws)
	{
		var client = A.Fake<IAmazonEventBridge>();
		if (throws) { A.CallTo(() => client.Config).Throws(new IOException("configuration-unreadable")); }
		else { A.CallTo(() => client.Config).Returns(null!); }
		await using var provider = Build(client, new PublishState(), named: false);
		_ = await StageAsync(provider);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("unreadable-eventbridge-config");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		(await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
		A.CallTo(() => client.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
		provider.GetRequiredService<AwsEventBridgePersistedAcceptanceShould.RetainingQueue>().Faults
			.ShouldHaveSingleItem().ShouldBeOfType<ConfigurationException>();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LeaveAnExternallyRegisteredClientForItsCallerToDispose(bool named)
	{
		var client = A.Fake<IAmazonEventBridge>();
		var provider = Build(client, new PublishState(), named);
		_ = Bus(provider, named);
		await provider.DisposeAsync();
		A.CallTo(() => client.Dispose()).MustNotHaveHappened();
		client.Dispose();
		A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreserveDirectConstructionClientOwnership()
	{
		var client = A.Fake<IAmazonEventBridge>();
		var bus = new AwsEventBridgeMessageBus(client, A.Fake<IPayloadSerializer>(), Options(), NullLogger<AwsEventBridgeMessageBus>.Instance);
		await bus.DisposeAsync();
		await bus.DisposeAsync();
		A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task PreserveNamedAliasIdentityAndSingleClientOwnership(bool containerOwnsClient, bool unkeyedFirst)
	{
		var client = A.Fake<IAmazonEventBridge>();
		var disposeCalls = 0;
		var cleaned = 0;
		var effectiveCleanup = 0;
		var factoryCalls = 0;
		A.CallTo(() => client.Dispose()).Invokes(() =>
		{
			Interlocked.Increment(ref disposeCalls);
			if (Interlocked.Exchange(ref cleaned, 1) == 0) { Interlocked.Increment(ref effectiveCleanup); }
		});
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
		if (containerOwnsClient)
		{
			services.AddKeyedSingleton<IAmazonEventBridge>("alias-control", (_, _) => { Interlocked.Increment(ref factoryCalls); return client; });
		}
		else
		{
			services.AddKeyedSingleton<IAmazonEventBridge>("alias-control", client);
		}
		services.AddAwsEventBridgeTransport("alias-control", builder => builder.Region("us-east-1"));
		factoryCalls.ShouldBe(0, "registration must not eagerly invoke caller factories");
		var provider = services.BuildServiceProvider();
		try
		{
			IAmazonEventBridge firstClient;
			AwsEventBridgeMessageBus firstBus;
			if (unkeyedFirst)
			{
				firstClient = provider.GetRequiredService<IAmazonEventBridge>();
				firstBus = provider.GetRequiredService<AwsEventBridgeMessageBus>();
				firstClient.ShouldBeSameAs(provider.GetRequiredKeyedService<IAmazonEventBridge>("alias-control"));
				firstBus.ShouldBeSameAs(provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("alias-control"));
			}
			else
			{
				firstClient = provider.GetRequiredKeyedService<IAmazonEventBridge>("alias-control");
				firstBus = provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("alias-control");
				firstClient.ShouldBeSameAs(provider.GetRequiredService<IAmazonEventBridge>());
				firstBus.ShouldBeSameAs(provider.GetRequiredService<AwsEventBridgeMessageBus>());
			}
			firstClient.ShouldBeSameAs(client);
			A.CallTo(() => client.Dispose()).MustNotHaveHappened();
			factoryCalls.ShouldBe(containerOwnsClient ? 1 : 0);
		}
		finally
		{
			await provider.DisposeAsync();
		}
		if (containerOwnsClient)
		{
			// Explicit contract correction: stock DI captures both raw factory aliases.
			// Two Dispose INVOCATIONS are allowed; effective idempotent cleanup remains one.
			disposeCalls.ShouldBe(2);
			effectiveCleanup.ShouldBe(1);
		}
		else
		{
			A.CallTo(() => client.Dispose()).MustNotHaveHappened();
			client.Dispose();
			A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
			effectiveCleanup.ShouldBe(1);
		}
	}

	[Fact]
	public async Task PreserveAnUnrelatedExplicitDefaultSdkInstanceDuringOrdinaryNamedRegistration()
	{
		var explicitDefault = A.Fake<IAmazonEventBridge>();
		var named = A.Fake<IAmazonEventBridge>();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
		services.AddSingleton<IAmazonEventBridge>(explicitDefault);
		services.AddKeyedSingleton<IAmazonEventBridge>("named-borrowed", named);
		services.AddAwsEventBridgeTransport("named-borrowed", builder => builder.Region("us-east-1"));
		var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IAmazonEventBridge>().ShouldBeSameAs(explicitDefault);
		provider.GetRequiredKeyedService<IAmazonEventBridge>("named-borrowed").ShouldBeSameAs(named);
		_ = provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("named-borrowed");
		await provider.DisposeAsync();
		A.CallTo(() => explicitDefault.Dispose()).MustNotHaveHappened();
		A.CallTo(() => named.Dispose()).MustNotHaveHappened();
		explicitDefault.Dispose(); named.Dispose();
	}

	[Fact]
	public void ExposeRawLateKeyedOnlyOverrideAsAnOwningAliasDiagnosticRatherThanASafeBorrowedComposition()
	{
		var client = A.Fake<IAmazonEventBridge>();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddAwsEventBridgeTransport("late", builder => builder.Region("us-east-1"));
		services.AddKeyedSingleton<IAmazonEventBridge>("late", client);
		using (var provider = services.BuildServiceProvider())
		{
			provider.GetRequiredService<IAmazonEventBridge>().ShouldBeSameAs(client);
			provider.GetRequiredKeyedService<IAmazonEventBridge>("late").ShouldBeSameAs(client);
		}
		// Only SDK aliases were resolved, no bus. The unkeyed factory result is
		// container-owned under stock DI; the safe helper counterpart must stay zero.
		A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
	}

	private static ServiceProvider Build(IAmazonEventBridge client, PublishState state, bool named, int? leaseTimeoutSeconds = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new AwsEventBridgePersistedAcceptanceShould.RetainingQueue());
		services.AddSingleton<IDeadLetterQueue>(provider => provider.GetRequiredService<AwsEventBridgePersistedAcceptanceShould.RetainingQueue>());
		services.AddDispatch(dispatch => dispatch.UseMiddleware<RetryMiddleware>());
		services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
		services.AddSingleton(Options());
		if (named)
		{
			services.AddKeyedSingleton<IAmazonEventBridge>("borrowed-control", client);
			services.AddAwsEventBridgeTransport("borrowed-control", builder => builder.Region("us-east-1"));
		}
		else
		{
			services.AddSingleton<IAmazonEventBridge>(client);
			services.AddAwsMessageBus(options => { options.EnableSqs = false; options.EnableSns = false; options.EnableEventBridge = true; });
		}
		services.Configure<RetryOptions>(options =>
		{
			options.MaxRetryAttempts = 2; options.BaseDelay = TimeSpan.Zero;
			options.MaxDelay = TimeSpan.FromMilliseconds(1); options.UseJitter = false;
		});
		services.AddScoped(provider => new PublishHandler(Bus(provider, named), provider.GetRequiredService<IMessageContextAccessor>(), state));
		services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory().WithProcessing(processing =>
			processing.MaxRetryCount(1).PollingInterval(TimeSpan.FromMilliseconds(20)).RetryDelay(TimeSpan.FromMilliseconds(10)))));
		services.Configure<InMemoryOutboxOptions>(options => options.FailureBackoffFloorSeconds = 1);
		if (leaseTimeoutSeconds is { } leaseSeconds)
		{
			services.Configure<InMemoryOutboxOptions>(options => options.LeaseTimeoutSeconds = leaseSeconds);
		}
		var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IHandlerRegistry>().Register(typeof(EventBridgeBorrowedRetryEvent), typeof(PublishHandler), expectsResponse: false);
		return provider;
	}
	private static AwsEventBridgeMessageBus Bus(IServiceProvider provider, bool named) => named
		? provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("borrowed-control")
		: provider.GetRequiredService<AwsEventBridgeMessageBus>();
	private static async Task<OutboundMessage> StageAsync(ServiceProvider provider)
	{
		MessageTypeRegistry.RegisterType<EventBridgeBorrowedRetryEvent>();
		var row = new OutboundMessage(typeof(EventBridgeBorrowedRetryEvent).AssemblyQualifiedName!,
			JsonSerializer.SerializeToUtf8Bytes(new EventBridgeBorrowedRetryEvent("persisted")), "local");
		await provider.GetRequiredService<IOutboxStore>().StageMessageAsync(row, TestContext.Current.CancellationToken);
		return row;
	}
	private static AwsEventBridgeOptions Options() => new() { EventBusName = "controlled-bus", EnableArchiving = false };
	private static AmazonEventBridgeClient Client(HttpClient http, int retries, int stale) => new(
		new BasicAWSCredentials("controlled-access", "controlled-secret"), new AmazonEventBridgeConfig
		{
			ServiceURL = "http://localhost:4566", AuthenticationRegion = "us-east-1", UseHttp = true,
			MaxErrorRetry = retries, MaxStaleConnectionRetries = stale, RetryMode = RequestRetryMode.Standard,
			HttpClientFactory = new OwnedHttpFactory(http),
		});
	private sealed record EventBridgeBorrowedRetryEvent(string Value) : IIntegrationEvent;
	private sealed class PublishState { internal int Calls; }
	private sealed class PublishHandler(AwsEventBridgeMessageBus bus, IMessageContextAccessor accessor, PublishState state)
		: IEventHandler<EventBridgeBorrowedRetryEvent>
	{
		public Task HandleAsync(EventBridgeBorrowedRetryEvent message, CancellationToken token)
		{
			Interlocked.Increment(ref state.Calls);
			return bus.PublishAsync(message, accessor.MessageContext ?? throw new InvalidOperationException("Missing actual dispatch context."), token);
		}
	}
	private sealed class OwnedHttpFactory(HttpClient http) : Amazon.Runtime.HttpClientFactory
	{
		public override HttpClient CreateHttpClient(IClientConfig config) => http;
		public override bool UseSDKHttpClientCaching(IClientConfig config) => false;
		public override bool DisposeHttpClientsAfterUse(IClientConfig config) => false;
	}
	private enum Mode { Fail, Success, Block, Permanent }
	private sealed class PhysicalHandler(Mode mode) : HttpMessageHandler
	{
		private int _requests;
		internal int Requests => Volatile.Read(ref _requests);
		internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
		{
			Interlocked.Increment(ref _requests);
			Entered.TrySetResult();
			if (mode == Mode.Block) { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
			if (mode == Mode.Fail)
			{
				return new HttpResponseMessage(HttpStatusCode.InternalServerError)
				{
					Content = new StringContent("{\"__type\":\"InternalFailure\",\"message\":\"controlled-sdk-500\"}", Encoding.UTF8, "application/x-amz-json-1.1"),
				};
			}
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(mode == Mode.Permanent
					? "{\"FailedEntryCount\":1,\"Entries\":[{\"ErrorCode\":\"AccessDeniedException\",\"ErrorMessage\":\"controlled\"}]}"
					: "{\"FailedEntryCount\":0,\"Entries\":[{\"EventId\":\"controlled-accepted\"}]}", Encoding.UTF8, "application/x-amz-json-1.1"),
			};
		}
	}
}
