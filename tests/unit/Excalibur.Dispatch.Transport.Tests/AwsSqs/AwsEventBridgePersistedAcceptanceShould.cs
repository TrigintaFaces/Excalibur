// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.EventBridge;
using Amazon.Runtime;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>Measures EventBridge ingestion acceptance through the actual SDK and persisted retry owner.</summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "Regression")]
public sealed class AwsEventBridgePersistedAcceptanceShould
{
	private const string Accepted = "{\"FailedEntryCount\":0,\"Entries\":[{\"EventId\":\"controlled-event-id\"}]}";

	[Fact]
	public async Task RetainARejectedEntryForTheNextDurableAttemptRatherThanMarkItDelivered()
	{
		using var handler = new ResponseHandler(Failed("InternalFailure"));
		using var http = new HttpClient(handler, disposeHandler: false);
		var state = new PublishState();
		await using var provider = Build(CreateClient(http), state);
		var message = await StageAsync(provider).ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("eventbridge-rejected-ingestion");
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(0, Diagnostics(message, state, handler));
		message.Status.ShouldBe(OutboxStatus.Failed, Diagnostics(message, state, handler));
		message.RetryCount.ShouldBe(1);
		state.Calls.ShouldBe(1);
		handler.Requests.ShouldBe(1);
		await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken).ConfigureAwait(false); // delay-ok: the persisted retry floor must actually elapse before the next attempt is due -- that interval is the behaviour under test
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(0);
		handler.Requests.ShouldBe(2, Diagnostics(message, state, handler));
		state.Calls.ShouldBe(2);
		message.RetryCount.ShouldBe(2, "terminal retirement must record both observed logical delivery attempts");
		message.Status.ShouldBe(OutboxStatus.DeadLettered, Diagnostics(message, state, handler));
		var queue = provider.GetRequiredService<RetainingQueue>();
		queue.Entries.ShouldHaveSingleItem().Payload.ShouldBe(message.Payload);
		queue.Entries[0].OriginalAttempts.ShouldBe(2);
		queue.OriginalMessageIds.ShouldBe([message.Id]);
	}

	[Fact]
	public async Task DeliverOnlyAfterTheSdkReturnsAnAcceptedEntry()
	{
		using var handler = new ResponseHandler(Accepted);
		using var http = new HttpClient(handler, disposeHandler: false);
		var state = new PublishState();
		await using var provider = Build(CreateClient(http), state);
		var message = await StageAsync(provider).ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("eventbridge-accepted-ingestion");
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(1, Diagnostics(message, state, handler));
		message.Status.ShouldBe(OutboxStatus.Sent);
		state.Calls.ShouldBe(1);
		handler.Requests.ShouldBe(1);
	}

	[Fact]
	public async Task RetryAThrottledEntryOnlyOnTheNextDurableAttemptAndThenAcceptIt()
	{
		using var handler = new ResponseHandler(Failed("ThrottlingException"), Accepted);
		using var http = new HttpClient(handler, disposeHandler: false);
		var state = new PublishState();
		await using var provider = Build(CreateClient(http), state);
		var message = await StageAsync(provider).ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("eventbridge-throttled-then-accepted");
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(0);
		message.Status.ShouldBe(OutboxStatus.Failed);
		message.RetryCount.ShouldBe(1);
		handler.Requests.ShouldBe(1);
		await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken).ConfigureAwait(false); // delay-ok: the persisted retry floor must actually elapse before the next attempt is due -- that interval is the behaviour under test
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(1, Diagnostics(message, state, handler));
		message.Status.ShouldBe(OutboxStatus.Sent);
		state.Calls.ShouldBe(2);
		handler.Requests.ShouldBe(2);
	}

	[Theory]
	[InlineData("AccessDeniedException")]
	[InlineData("MalformedDetail")]
	[InlineData("InvalidAccountIdException")]
	[InlineData("InvalidArgument")]
	[InlineData("RedactionFailure")]
	[InlineData("NotAuthorizedForSourceException")]
	[InlineData("NotAuthorizedForDetailTypeException")]
	public async Task RetireAKnownPermanentEntryFailureOnTheFirstDurableAttempt(string errorCode)
	{
		using var handler = new ResponseHandler(Failed(errorCode));
		using var http = new HttpClient(handler, disposeHandler: false);
		var state = new PublishState();
		await using var provider = Build(CreateClient(http), state);
		var message = await StageAsync(provider).ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("eventbridge-permanent-ingestion");
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(0, Diagnostics(message, state, handler));
		message.Status.ShouldBe(OutboxStatus.DeadLettered, Diagnostics(message, state, handler));
		state.Calls.ShouldBe(1);
		handler.Requests.ShouldBe(1);
		var queue = provider.GetRequiredService<RetainingQueue>();
		var entry = queue.Entries.ShouldHaveSingleItem();
		entry.Payload.ShouldBe(message.Payload);
		entry.Reason.ShouldBe(errorCode == "MalformedDetail" ? DeadLetterReason.PoisonMessage : DeadLetterReason.UnhandledException);
		queue.OriginalMessageIds.ShouldBe([message.Id]);
	}

	[Theory]
	[InlineData("{\"FailedEntryCount\":0,\"Entries\":[]}")]
	[InlineData("{\"FailedEntryCount\":0}")]
	[InlineData("{\"FailedEntryCount\":0,\"Entries\":[{}]}")]
	[InlineData("{\"Entries\":[{\"EventId\":\"controlled-id\"}]}")]
	[InlineData("{\"FailedEntryCount\":0,\"Entries\":[{\"EventId\":\"id\",\"ErrorCode\":\"InternalFailure\"}]}")]
	[InlineData("{\"FailedEntryCount\":1,\"Entries\":[{\"EventId\":\"id\",\"ErrorCode\":\"InternalFailure\"}]}")]
	[InlineData("{\"FailedEntryCount\":0,\"Entries\":[{\"EventId\":\"one\"},{\"EventId\":\"two\"}]}")]
	[InlineData("{\"FailedEntryCount\":1,\"Entries\":[{\"ErrorMessage\":\"missing code\"}]}")]
	[InlineData("{\"FailedEntryCount\":1,\"Entries\":[{\"ErrorCode\":\"UnknownFutureFailure\"}]}")]
	public async Task RefuseToEstablishAcceptanceFromAnIncompleteResponse(string response)
	{
		using var handler = new ResponseHandler(response, Accepted);
		using var http = new HttpClient(handler, disposeHandler: false);
		var state = new PublishState();
		await using var provider = Build(CreateClient(http), state);
		var message = await StageAsync(provider).ConfigureAwait(false);
		var originalId = message.Id;
		var originalPayload = message.Payload.ToArray();
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("eventbridge-unestablished-ingestion");
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(0, Diagnostics(message, state, handler));
		message.Status.ShouldBe(OutboxStatus.Failed, Diagnostics(message, state, handler));
		message.RetryCount.ShouldBe(1);
		handler.Requests.ShouldBe(1);
		state.Calls.ShouldBe(1);
		provider.GetRequiredService<RetainingQueue>().Entries.ShouldBeEmpty();
		await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken).ConfigureAwait(false); // delay-ok: the persisted retry floor must actually elapse before the next attempt is due -- that interval is the behaviour under test
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(1);
		message.Status.ShouldBe(OutboxStatus.Sent);
		message.Id.ShouldBe(originalId);
		message.Payload.ShouldBe(originalPayload);
		state.Calls.ShouldBe(2);
		handler.Requests.ShouldBe(2);
		handler.Details[1].ShouldBe(handler.Details[0]);
		provider.GetRequiredService<RetainingQueue>().Entries.ShouldBeEmpty();
	}

	[Fact]
	public async Task RetainUnestablishedIngestionAfterExhaustionAcrossRecreatedProcessors()
	{
		using var handler = new ResponseHandler(Failed("UnknownFutureFailure"));
		using var http = new HttpClient(handler, disposeHandler: false);
		var state = new PublishState();
		await using var provider = Build(CreateClient(http), state);
		var message = await StageAsync(provider).ConfigureAwait(false);
		await DrainAsync(provider, "uncertain-first").ConfigureAwait(false);
		message.Status.ShouldBe(OutboxStatus.Failed);
		await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken).ConfigureAwait(false); // delay-ok: the persisted retry floor must actually elapse before the next attempt is due -- that interval is the behaviour under test
		await DrainAsync(provider, "uncertain-recreated").ConfigureAwait(false);
		message.Status.ShouldBe(OutboxStatus.DeadLettered);
		handler.Requests.ShouldBe(2);
		state.Calls.ShouldBe(2);
		var queue = provider.GetRequiredService<RetainingQueue>();
		var entry = queue.Entries.ShouldHaveSingleItem();
		entry.Payload.ShouldBe(message.Payload);
		queue.OriginalMessageIds.ShouldBe([message.Id]);
		entry.ExceptionMessage.ShouldNotBeNull().ShouldContain("ingestion");
		entry.ExceptionMessage.ShouldNotContain("unsupported");
		queue.Faults.ShouldHaveSingleItem().ShouldBeOfType<DispatchDeliveryUncertainException>();
		provider.GetRequiredService<IMessageFailureClassifier>().Classify(queue.Faults[0]).ShouldBe(MessageFailureKind.Uncertain);
	}

	[Fact]
	public async Task RespectThePhysicalAttemptCapWhenTheRetainingQueueIsUnavailable()
	{
		using var handler = new ResponseHandler(Failed("UnknownFutureFailure"));
		using var http = new HttpClient(handler, disposeHandler: false);
		var state = new PublishState();
		await using var provider = Build(CreateClient(http), state);
		var queue = provider.GetRequiredService<RetainingQueue>();
		queue.Unavailable = true;
		var message = await StageAsync(provider).ConfigureAwait(false);
		var originalPayload = message.Payload.ToArray();
		await DrainAsync(provider, "queue-outage-first").ConfigureAwait(false);
		message.Status.ShouldBe(OutboxStatus.Failed);
		await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken).ConfigureAwait(false); // delay-ok: the persisted retry floor must actually elapse before the next attempt is due -- that interval is the behaviour under test
		await DrainAsync(provider, "queue-outage-exhausted").ConfigureAwait(false);
		handler.Requests.ShouldBe(2);
		queue.EnqueueAttempts.ShouldBe(1);
		message.Status.ShouldNotBe(OutboxStatus.DeadLettered);
		message.RetryCount.ShouldBe(2, "the observed physical attempt must be persisted even though the external queue write failed");
		message.Payload.ShouldBe(originalPayload);
		queue.Entries.ShouldBeEmpty();
		await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken).ConfigureAwait(false); // delay-ok: the persisted retry floor must actually elapse before the next attempt is due -- that interval is the behaviour under test
		await DrainAsync(provider, "queue-outage-recreated").ConfigureAwait(false);
		handler.Requests.ShouldBe(2, "a successor must retry disposition, not publish beyond the persisted physical-attempt cap");
		queue.EnqueueAttempts.ShouldBe(2);
		queue.Unavailable = false;
		await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken).ConfigureAwait(false); // delay-ok: the persisted retry floor must actually elapse before the next attempt is due -- that interval is the behaviour under test
		await DrainAsync(provider, "queue-recovered").ConfigureAwait(false);
		handler.Requests.ShouldBe(2);
		state.Calls.ShouldBe(2);
		message.Status.ShouldBe(OutboxStatus.DeadLettered);
		queue.Entries.ShouldHaveSingleItem().Payload.ShouldBe(originalPayload);
		queue.OriginalMessageIds.ShouldBe([message.Id]);
	}

	private static async Task DrainAsync(IServiceProvider provider, string dispatcher)
	{
		await using var processor = (OutboxProcessor)provider.GetRequiredService<IOutboxProcessor>();
		processor.Init(dispatcher);
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(0);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task SendAJsonObjectThatPreservesEveryPluggablePayloadByte(int path)
	{
		using var handler = new ResponseHandler(Accepted);
		using var http = new HttpClient(handler, disposeHandler: false);
		var serializer = A.Fake<IPayloadSerializer>();
		byte[] bytes = [0, 255, 1, 128, 34, 92];
		A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).Returns(bytes);
		await using var bus = new AwsEventBridgeMessageBus(CreateClient(http), serializer, Options(), NullLogger<AwsEventBridgeMessageBus>.Instance);
		var context = new MessageContext();
		await (path switch
		{
			0 => bus.PublishAsync(new AcceptanceAction("binary-payload"), context, TestContext.Current.CancellationToken),
			1 => bus.PublishAsync(new AcceptanceEvent("binary-payload"), context, TestContext.Current.CancellationToken),
			_ => bus.PublishAsync(new AcceptanceDocument("binary-payload"), context, TestContext.Current.CancellationToken),
		}).ConfigureAwait(false);
		handler.Requests.ShouldBe(1);
		using var request = JsonDocument.Parse(handler.LastRequest!);
		var detail = request.RootElement.GetProperty("Entries")[0].GetProperty("Detail").GetString()!;
		using var envelope = JsonDocument.Parse(detail);
		envelope.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
		Convert.FromBase64String(envelope.RootElement.GetProperty("payload").GetString()!).ShouldBe(bytes);
	}

	private static string Failed(string code) => JsonSerializer.Serialize(new
	{
		FailedEntryCount = 1,
		Entries = new[] { new { ErrorCode = code, ErrorMessage = "controlled rejected ingestion" } },
	});

	private static AwsEventBridgeOptions Options() => new()
	{
		EventBusName = "controlled-bus", EnableArchiving = false, DefaultSource = "controlled-source", DefaultDetailType = "controlled-event",
	};

	private static AmazonEventBridgeClient CreateClient(HttpClient http) => new(new BasicAWSCredentials("controlled-access", "controlled-secret"),
		new AmazonEventBridgeConfig
		{
			ServiceURL = "http://localhost:4566", AuthenticationRegion = "us-east-1", UseHttp = true,
			MaxErrorRetry = 0, MaxStaleConnectionRetries = 0, RetryMode = RequestRetryMode.Standard, HttpClientFactory = new OwnedHttpFactory(http),
		});

	private static ServiceProvider Build(IAmazonEventBridge client, PublishState state)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new RetainingQueue());
		services.AddSingleton<IDeadLetterQueue>(provider => provider.GetRequiredService<RetainingQueue>());
		services.AddDispatch(dispatch => dispatch.UseMiddleware<RetryMiddleware>());
		services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
		services.Configure<RetryOptions>(options =>
		{
			options.MaxRetryAttempts = 2; options.BaseDelay = TimeSpan.Zero; options.MaxDelay = TimeSpan.FromMilliseconds(1); options.UseJitter = false;
		});
		services.AddSingleton(provider => new AwsEventBridgeMessageBus(client, provider.GetRequiredService<IPayloadSerializer>(), Options(),
			NullLogger<AwsEventBridgeMessageBus>.Instance));
		services.AddScoped(provider => new PublishHandler(provider.GetRequiredService<AwsEventBridgeMessageBus>(),
			provider.GetRequiredService<IMessageContextAccessor>(), state));
		services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory().WithProcessing(processing =>
			processing.MaxRetryCount(1).PollingInterval(TimeSpan.FromMilliseconds(20)).RetryDelay(TimeSpan.FromMilliseconds(10)))));
		services.Configure<InMemoryOutboxOptions>(options => options.FailureBackoffFloorSeconds = 1);
		var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IHandlerRegistry>().Register(typeof(AcceptanceEvent), typeof(PublishHandler), expectsResponse: false);
		return provider;
	}

	private static async Task<OutboundMessage> StageAsync(IServiceProvider provider)
	{
		MessageTypeRegistry.RegisterType<AcceptanceEvent>();
		var row = new OutboundMessage(nameof(AcceptanceEvent), JsonSerializer.SerializeToUtf8Bytes(new AcceptanceEvent("persisted")), "local");
		await provider.GetRequiredService<IOutboxStore>().StageMessageAsync(row, TestContext.Current.CancellationToken).ConfigureAwait(false);
		return row;
	}

	private static string Diagnostics(OutboundMessage row, PublishState state, ResponseHandler handler) =>
		$"Status={row.Status}, retryCount={row.RetryCount}, lastError={row.LastError}, handlerCalls={state.Calls}, HTTP requests={handler.Requests}";
	private sealed record AcceptanceEvent(string Value) : IIntegrationEvent;
	private sealed record AcceptanceAction(string Value) : IDispatchAction;
	private sealed record AcceptanceDocument(string Value) : IDispatchDocument;
	private sealed class PublishState { internal int Calls; }
	private sealed class PublishHandler(AwsEventBridgeMessageBus bus, IMessageContextAccessor accessor, PublishState state) : IEventHandler<AcceptanceEvent>
	{
		public Task HandleAsync(AcceptanceEvent eventMessage, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref state.Calls);
			return bus.PublishAsync(eventMessage, accessor.MessageContext ?? throw new InvalidOperationException("Dispatcher did not establish its context."), cancellationToken);
		}
	}

	private sealed class OwnedHttpFactory(HttpClient http) : Amazon.Runtime.HttpClientFactory
	{
		public override HttpClient CreateHttpClient(IClientConfig clientConfig) => http;
		public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;
		public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
	}

	private sealed class ResponseHandler(string response, string? subsequentResponse = null) : HttpMessageHandler
	{
		private int _requests;
		internal int Requests => Volatile.Read(ref _requests);
		internal string? LastRequest { get; private set; }
		internal List<string> Details { get; } = [];
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var attempt = Interlocked.Increment(ref _requests);
			LastRequest = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			using var document = JsonDocument.Parse(LastRequest);
			Details.Add(document.RootElement.GetProperty("Entries")[0].GetProperty("Detail").GetString()!);
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(attempt > 1 ? subsequentResponse ?? response : response, Encoding.UTF8, "application/x-amz-json-1.1"),
			};
		}
	}

	// In-memory retained evidence for this local control; recreating processors is not a process-restart durability proof.
	internal sealed class RetainingQueue : IDeadLetterQueue
	{
		internal void AssertRetained(OutboundMessage message, DeadLetterReason reason, int attempts)
		{
			EnqueueAttempts.ShouldBe(1);
			OriginalMessageIds.ShouldHaveSingleItem().ShouldBe(message.Id);
			var entry = Entries.ShouldHaveSingleItem();
			entry.Id.ShouldNotBe(Guid.Empty);
			entry.MessageType.ShouldBe(message.MessageType);
			entry.Payload.ShouldBe(message.Payload);
			entry.Reason.ShouldBe(reason);
			entry.OriginalAttempts.ShouldBe(attempts);
		}

		internal bool Unavailable { get; set; }
		internal int EnqueueAttempts { get; private set; }
		internal List<DeadLetterEntry> Entries { get; } = [];
		internal List<string> OriginalMessageIds { get; } = [];
		internal List<Exception> Faults { get; } = [];

		public Task<Guid> EnqueueAsync<T>(T message, DeadLetterReason reason, CancellationToken cancellationToken,
			Exception? exception = null, IDictionary<string, string>? metadata = null)
		{
			cancellationToken.ThrowIfCancellationRequested();
			EnqueueAttempts++;
			if (Unavailable) throw new IOException("Controlled retaining queue outage.");
			var original = message.ShouldBeAssignableTo<IOutboxMessage>();
			var id = Guid.NewGuid();
			OriginalMessageIds.Add(original.MessageId);
			if (exception is not null) Faults.Add(exception);
			Entries.Add(new DeadLetterEntry
			{
				Id = id, MessageType = original.MessageType, Payload = original.MessageBody.ToArray(), Reason = reason,
				ExceptionMessage = exception?.Message, OriginalAttempts = original.Attempts, Metadata = metadata,
			});
			return Task.FromResult(id);
		}

		public Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(CancellationToken cancellationToken,
			DeadLetterQueryFilter? filter = null, int limit = 100) => Task.FromResult<IReadOnlyList<DeadLetterEntry>>(Entries.Take(limit).ToArray());
		public Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
			Task.FromResult(Entries.SingleOrDefault(entry => entry.Id == entryId));
		public Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken) => Task.FromResult(false);
		public Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null) =>
			Task.FromResult((long)Entries.Count);
	}
}
