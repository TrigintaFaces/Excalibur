// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Outbox.Diagnostics;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

/// <summary>
/// Counts physical SDK HTTP requests under the persisted outbox retry owner. The controlled HTTP handler
/// replaces only the network boundary; the SDK, dispatcher, outbox processor and SQS bus are real.
/// These are local characterization controls, not AWS delivery or deduplication certification.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "Regression")]
public sealed class AwsSqsPersistedRetryPhysicalRequestsShould
{
	private const string QueueUrl = "http://localhost:4566/000000000000/physical-request-controls";

	[Fact]
	public async Task ConfirmTheRealSdkMakesTwoPhysicalRequestsWithOneConfiguredRetry()
	{
		using var handler = new CountingHandler(ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = CreateClient(http);
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var error = await Should.ThrowAsync<AmazonSQSException>(() => client.SendMessageAsync(
			new SendMessageRequest(QueueUrl, "positive-sdk-retry-control"), deadline.Token)).ConfigureAwait(false);
		error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		handler.Requests.ShouldBe(2, "MaxErrorRetry=1 means an initial physical request and one SDK retry");
	}

	[Fact]
	public async Task KeepOnePhysicalRequestPerPersistedAttemptWhenTheSendSdkRetriesAreDisabled()
	{
		using var observation = new SqsTelemetryObservation();
		using var handler = new CountingHandler(ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = CreateClient(http, maxErrorRetry: 0, telemetryProvider: observation.Provider);
		var state = new PublishState();
		await using var provider = Build(client, state, logger: observation.Logger);
		var message = await StageAsync(provider, "durable-two-attempts").ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("sqs-physical-characterization");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		await AwsPersistedOutboxObservation.AssertStateAsync(provider, message, OutboxStatus.Failed, Diagnostics(message, state, handler));
		await AwsPersistedOutboxObservation.AssertRetryCountAsync(provider, message, 1);
		state.PublishCalls.ShouldBe(1, Diagnostics(message, state, handler));
		handler.Requests.ShouldBe(1, Diagnostics(message, state, handler));
		await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token).ConfigureAwait(false); // delay-ok: the persisted retry floor must actually elapse before the next attempt is due -- that interval is the behaviour under test
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		state.PublishCalls.ShouldBe(2, "each durable attempt invokes the bus once, without middleware multiplication");
		handler.Requests.ShouldBe(2, "each durable attempt must make one physical request with no SDK or foreground retry multiplication");
		await AwsPersistedOutboxObservation.AssertStateAsync(provider, message, OutboxStatus.DeadLettered, Diagnostics(message, state, handler));
		((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).AssertRetained(message, global::Excalibur.Dispatch.ErrorHandling.DeadLetterReason.MaxRetriesExceeded, 2);
		await AwsPersistedOutboxObservation.AssertRetryCountAsync(provider, message, 2, "terminal retirement records both observed attempts; handler and HTTP counts prove the final attempt");
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		handler.Requests.ShouldBe(2, "the terminal outbox row must not start another SDK call");
		observation.AssertAttributedRequests(message.Id, handler.Requests, 2, "dispatch", requireOutboxActivity: true);
	}

	[Fact]
	public async Task DeliverASuccessfulPersistedAttemptWithOnePhysicalRequest()
	{
		using var observation = new SqsTelemetryObservation();
		using var handler = new CountingHandler(ResponseMode.Success);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = CreateClient(http, maxErrorRetry: 0, telemetryProvider: observation.Provider);
		var state = new PublishState();
		await using var provider = Build(client, state, logger: observation.Logger);
		var message = await StageAsync(provider, "one-successful-request").ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("sqs-physical-success");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false))
			.ShouldBe(1, Diagnostics(message, state, handler));
		await AwsPersistedOutboxObservation.AssertStateAsync(provider, message, OutboxStatus.Sent);
		((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).Entries.ShouldBeEmpty();
		await AwsPersistedOutboxObservation.AssertRetryCountAsync(provider, message, 0);
		state.PublishCalls.ShouldBe(1);
		handler.Requests.ShouldBe(1);
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		handler.Requests.ShouldBe(1);
		observation.AssertAttributedRequests(message.Id, handler.Requests, 1, "dispatch", requireOutboxActivity: true);
	}

	[Fact]
	public async Task CancelABlockedFirstPhysicalRequestWithoutRetryingOrConsumingThePersistedFailureBudget()
	{
		using var observation = new SqsTelemetryObservation();
		using var handler = new CountingHandler(ResponseMode.Block);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = CreateClient(http, maxErrorRetry: 0, telemetryProvider: observation.Provider);
		var state = new PublishState();
		await using var provider = Build(client, state, logger: observation.Logger);
		var message = await StageAsync(provider, "cancel-first-request").ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("sqs-physical-cancel");
		using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var running = processor.DispatchPendingMessagesAsync(cancellation.Token);
		try
		{
			_ = await Task.WhenAny(handler.Entered.Task, running)
				.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(false);
			handler.Entered.Task.IsCompleted.ShouldBeTrue(Diagnostics(message, state, handler));
			await cancellation.CancelAsync().ConfigureAwait(false);
			_ = await Should.ThrowAsync<OperationCanceledException>(() => running).ConfigureAwait(false);
			handler.Requests.ShouldBe(1);
			state.PublishCalls.ShouldBe(1);
			await AwsPersistedOutboxObservation.AssertNotStateAsync(provider, message, OutboxStatus.Sent);
			await AwsPersistedOutboxObservation.AssertNotStateAsync(provider, message, OutboxStatus.DeadLettered);
			await AwsPersistedOutboxObservation.AssertRetryCountAsync(provider, message, 0, "caller cancellation is not a retry-consuming transport failure");
			((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).Entries.ShouldBeEmpty();
			observation.AssertAttributedRequests(message.Id, handler.Requests, 1, "dispatch", requireOutboxActivity: true);
		}
		finally
		{
			await cancellation.CancelAsync().ConfigureAwait(false);
			try { _ = await running.ConfigureAwait(false); }
			catch (OperationCanceledException) { }
		}
	}

	[Fact]
	public async Task AttributeAnUnownedSdkRetryThroughAnExplicitAmbientActivityAndVendorTelemetry()
	{
		using var observation = new SqsTelemetryObservation();
		using var handler = new CountingHandler(ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = CreateClient(http, maxErrorRetry: 1, telemetryProvider: observation.Provider);
		await using var provider = Build(client, new PublishState(), new AwsSqsOptions
		{
			QueueUrl = new Uri(QueueUrl), MaxRetryAttempts = 1, RequestTimeout = TimeSpan.FromSeconds(3),
			RetryPolicy = new TransportRetryPolicyOptions { BaseDelayMs = 1, MaxDelayMs = 2 },
		}, observation.Logger);
		var context = new MessageContext { MessageId = Guid.NewGuid().ToString("N") };
		using var source = new ActivitySource(SqsTelemetryObservation.ApplicationSourceName);
		using var operation = source.StartActivity("unowned.sqs.publish").ShouldNotBeNull();
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var error = await Should.ThrowAsync<AmazonSQSException>(() => provider.GetRequiredService<AwsSqsMessageBus>()
			.PublishAsync(new PhysicalRequestEvent("unowned-observed-sdk-retries"), context, deadline.Token)).ConfigureAwait(false);

		error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
		handler.Requests.ShouldBe(2);
		client.Config.MaxErrorRetry.ShouldBe(1);
		observation.AssertAttributedRequests(context.MessageId.ShouldNotBeNull(), handler.Requests, 1, "sdk",
			requireOutboxActivity: false, requestsPerSdkOperation: 2);
	}

	// Observes existing public SDK callbacks; it implements no histogram, retry, or export algorithm.
	private sealed class SqsTelemetryObservation : IDisposable
	{
		internal const string ApplicationSourceName = "Excalibur.Tests.SqsConsumerOperation";
		private readonly ActivityListener _listener;
		private readonly ConcurrentQueue<ObservedMetric> _metrics = new();
		private readonly ConcurrentQueue<ObservedOperation> _operations = new();

		internal SqsTelemetryObservation()
		{
			_listener = new ActivityListener
			{
				ShouldListenTo = source => source.Name == BackgroundServiceActivitySource.SourceName
					|| source.Name == ApplicationSourceName,
				Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
				SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
			};
			ActivitySource.AddActivityListener(_listener);
			Provider = new Amazon.Runtime.Telemetry.DefaultTelemetryProvider();
			Provider.RegisterMeterProvider(new ObservingMeterProvider(this));
			Logger = A.Fake<ILogger<AwsSqsMessageBus>>();
			A.CallTo(() => Logger.IsEnabled(A<LogLevel>._)).Returns(true);
			A.CallTo(Logger).Where(call => call.Method.Name == "Log").Invokes(call =>
			{
				if (call.GetArgument<object>(2) is not IEnumerable<KeyValuePair<string, object?>> fields)
				{
					return;
				}

				var values = fields.ToDictionary(pair => pair.Key, pair => pair.Value);
				if (values.TryGetValue("RetryOwner", out var owner) && values.TryGetValue("MessageId", out var messageId))
				{
					_operations.Enqueue(new ObservedOperation(messageId?.ToString(), owner?.ToString(), CaptureActivity()));
				}
			});
		}

		internal Amazon.Runtime.Telemetry.DefaultTelemetryProvider Provider { get; }
		internal ILogger<AwsSqsMessageBus> Logger { get; }

		internal void AssertAttributedRequests(string messageId, int physicalRequests, int sdkOperations,
			string owner, bool requireOutboxActivity, int requestsPerSdkOperation = 1)
		{
			var operations = _operations.ToArray();
			operations.Length.ShouldBe(sdkOperations);
			foreach (var operation in operations)
			{
				operation.MessageId.ShouldBe(messageId);
				operation.Owner.ShouldBe(owner);
			}

			var attempts = _metrics.Where(metric => metric.Histogram && metric.Name == "client.call.attempt_duration").ToArray();
			attempts.Length.ShouldBe(physicalRequests,
				"selected HTTP-response/entered-cancel observations must match the actual handler boundary");
			foreach (var operation in operations)
			{
				attempts.Count(attempt => attempt.Context.TraceId == operation.Context.TraceId
					&& attempt.Context.SpanId == operation.Context.SpanId).ShouldBe(requestsPerSdkOperation,
					"each selected SDK operation must account for its own physical request observations");
			}

			foreach (var attempt in attempts)
			{
				double.IsFinite(attempt.Value).ShouldBeTrue();
				attempt.Context.TraceId.ShouldNotBe(default(ActivityTraceId));
				attempt.Context.SpanId.ShouldNotBe(default(ActivitySpanId));
				operations.Count(operation => operation.Context.TraceId == attempt.Context.TraceId
					&& operation.Context.SpanId == attempt.Context.SpanId).ShouldBe(1);
				attempt.Method.ShouldBe("SendMessage");
				attempt.Service.ShouldBe("SQS");
				if (requireOutboxActivity)
				{
					attempt.Context.OutboxMessageId.ShouldBe(messageId);
				}
			}

			// client.call.attempts is recorded separately: a retry decision is not a physical-send certificate.
			// No sum of that counter and logical operations is asserted here.
			var observedCounter = _metrics.Where(metric => !metric.Histogram && metric.Name == "client.call.attempts")
				.Sum(metric => metric.Value);
			TestContext.Current.TestOutputHelper?.WriteLine(
				$"SQS-TELEMETRY owner={owner}; physical={physicalRequests}; sdkOperations={operations.Length}; "
				+ $"attemptDurationObservations={attempts.Length}; sdkCallAttemptsObservedSum={observedCounter}");
		}

		public void Dispose() => _listener.Dispose();

		private void ObserveMetric(string name, double value, Amazon.Runtime.Telemetry.Attributes attributes, bool histogram) =>
			_metrics.Enqueue(new ObservedMetric(name, value, histogram, attributes.Get("rpc.method")?.ToString(),
				attributes.Get("rpc.service")?.ToString(), CaptureActivity()));

		// These five types derive the AWS SDK's own telemetry extension points rather than faking them.
		// MeterProvider/Meter/Histogram/MonotonicCounter/UpDownCounter are ABSTRACT classes the SDK
		// publishes precisely so a telemetry plugin can supply its own implementation -- deriving them is
		// the documented mechanism, not a workaround for it. Faking them instead intercepts members the
		// SDK makes no virtuality guarantee about, which is the fragility ADR-142 §D7 forbids and what
		// NoConcreteSdkFakesGovernanceShould enforces for every `Amazon.` type.
		//
		// CreateUpDownCounter is overridden because it is abstract, not because the test needs it: the
		// previous fake answered it implicitly, so leaving it out would have changed behaviour silently
		// from "returns a no-op counter" to "does not compile". It records like the others so that a
		// future SDK version which starts emitting up-down counters is observed rather than dropped.
		private sealed class ObservingMeterProvider(SqsTelemetryObservation observation)
			: Amazon.Runtime.Telemetry.Metrics.MeterProvider
		{
			public override Amazon.Runtime.Telemetry.Metrics.Meter GetMeter(
				string scope, Amazon.Runtime.Telemetry.Attributes attributes) => new ObservingMeter(observation);
		}

		private sealed class ObservingMeter(SqsTelemetryObservation observation)
			: Amazon.Runtime.Telemetry.Metrics.Meter
		{
			public override Amazon.Runtime.Telemetry.Metrics.Histogram<T> CreateHistogram<T>(
				string name, string units, string description) => new ObservingHistogram<T>(observation, name);

			public override Amazon.Runtime.Telemetry.Metrics.MonotonicCounter<T> CreateMonotonicCounter<T>(
				string name, string units, string description) => new ObservingMonotonicCounter<T>(observation, name);

			public override Amazon.Runtime.Telemetry.Metrics.UpDownCounter<T> CreateUpDownCounter<T>(
				string name, string units, string description) => new ObservingUpDownCounter<T>(observation, name);
		}

		private sealed class ObservingHistogram<T>(SqsTelemetryObservation observation, string name)
			: Amazon.Runtime.Telemetry.Metrics.Histogram<T> where T : struct
		{
			public override void Record(T value, Amazon.Runtime.Telemetry.Attributes attributes) =>
				observation.ObserveMetric(name, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
					attributes, histogram: true);
		}

		private sealed class ObservingMonotonicCounter<T>(SqsTelemetryObservation observation, string name)
			: Amazon.Runtime.Telemetry.Metrics.MonotonicCounter<T> where T : struct
		{
			public override void Add(T value, Amazon.Runtime.Telemetry.Attributes attributes) =>
				observation.ObserveMetric(name, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
					attributes, histogram: false);
		}

		private sealed class ObservingUpDownCounter<T>(SqsTelemetryObservation observation, string name)
			: Amazon.Runtime.Telemetry.Metrics.UpDownCounter<T> where T : struct
		{
			public override void Add(T value, Amazon.Runtime.Telemetry.Attributes attributes) =>
				observation.ObserveMetric(name, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
					attributes, histogram: false);
		}

		private static ObservedActivity CaptureActivity()
		{
			var current = Activity.Current;
			string? outboxMessageId = null;
			for (var ancestor = current; ancestor is not null; ancestor = ancestor.Parent)
			{
				if (ancestor.Source.Name == BackgroundServiceActivitySource.SourceName
					&& ancestor.OperationName == "outbox.dispatch_message")
				{
					outboxMessageId = ancestor.GetTagItem("messaging.message_id")?.ToString();
					break;
				}
			}

			return new ObservedActivity(current?.TraceId ?? default, current?.SpanId ?? default, outboxMessageId);
		}
	}

	private sealed record ObservedMetric(string Name, double Value, bool Histogram, string? Method,
		string? Service, ObservedActivity Context);
	private sealed record ObservedOperation(string? MessageId, string? Owner, ObservedActivity Context);
	private readonly record struct ObservedActivity(ActivityTraceId TraceId, ActivitySpanId SpanId, string? OutboxMessageId);


	internal static AmazonSQSClient CreateClient(HttpClient http, int maxErrorRetry = 1, int maxStaleConnectionRetries = 0,
		Amazon.Runtime.Telemetry.TelemetryProvider? telemetryProvider = null)
	{
		var config = new AmazonSQSConfig
		{
			ServiceURL = "http://localhost:4566", AuthenticationRegion = "us-east-1", UseHttp = true,
			RetryMode = RequestRetryMode.Standard, MaxErrorRetry = maxErrorRetry,
			MaxStaleConnectionRetries = maxStaleConnectionRetries, HttpClientFactory = new OwnedHttpFactory(http),
		};
		if (telemetryProvider is not null)
		{
			config.TelemetryProvider = telemetryProvider;
		}

		return new AmazonSQSClient(new BasicAWSCredentials("controlled-access", "controlled-secret"), config);
	}

	internal static ServiceProvider Build(IAmazonSQS client, PublishState state, AwsSqsOptions? sendOptions = null,
		ILogger<AwsSqsMessageBus>? logger = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>(new global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue());
		services.AddDispatch(dispatch => dispatch.UseMiddleware<RetryMiddleware>());
		services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
		services.Configure<RetryOptions>(options =>
		{
			options.MaxRetryAttempts = 2;
			options.BaseDelay = TimeSpan.Zero;
			options.MaxDelay = TimeSpan.FromMilliseconds(1);
			options.UseJitter = false;
			options.RetryableExceptions.Add(typeof(AmazonSQSException));
		});
		services.AddSingleton(provider => new AwsSqsMessageBus(client, provider.GetRequiredService<IPayloadSerializer>(),
			Microsoft.Extensions.Options.Options.Create(sendOptions ?? new AwsSqsOptions { QueueUrl = new Uri(QueueUrl) }),
			Microsoft.Extensions.Options.Options.Create(new AwsSqsFifoOptions()),
			logger ?? NullLogger<AwsSqsMessageBus>.Instance, ownsClient: false));
		services.AddScoped(provider => new PublishHandler(provider.GetRequiredService<AwsSqsMessageBus>(),
			provider.GetRequiredService<IMessageContextAccessor>(), state));
		services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory().WithProcessing(processing =>
			processing.MaxRetryCount(1).PollingInterval(TimeSpan.FromMilliseconds(20)).RetryDelay(TimeSpan.FromMilliseconds(10)))));
		services.Configure<InMemoryOutboxOptions>(options => options.FailureBackoffFloorSeconds = 1);
		AwsPersistedOutboxObservation.Register(services);
		var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IHandlerRegistry>().Register(typeof(PhysicalRequestEvent), typeof(PublishHandler), expectsResponse: false);
		return provider;
	}

	internal static async Task<OutboundMessage> StageAsync(ServiceProvider provider, string value)
	{
		MessageTypeRegistry.RegisterType<PhysicalRequestEvent>();
		// OutboxProcessor.PrepareDispatchAsync consumes DispatchJsonSerializer's UTF-8 JSON.
		// The SQS bus uses the separately registered magic-byte IPayloadSerializer when publishing.
		var payload = JsonSerializer.SerializeToUtf8Bytes(new PhysicalRequestEvent(value));
		var message = new OutboundMessage(typeof(PhysicalRequestEvent).AssemblyQualifiedName!, payload, "local");
		await provider.GetRequiredService<IOutboxStore>().StageMessageAsync(message, TestContext.Current.CancellationToken).ConfigureAwait(false);
		return message;
	}

	private sealed record PhysicalRequestEvent(string Value) : IIntegrationEvent;
	private static string Diagnostics(OutboundMessage message, PublishState state, CountingHandler handler) =>
		$"Outbox status={message.Status}, retryCount={message.RetryCount}, lastError={message.LastError}, "
		+ $"handler publishes={state.PublishCalls}, physical HTTP requests={handler.Requests}.";
	internal sealed class PublishState { internal int PublishCalls; }
	private sealed class PublishHandler(AwsSqsMessageBus bus, IMessageContextAccessor accessor, PublishState state)
		: IEventHandler<PhysicalRequestEvent>
	{
		public Task HandleAsync(PhysicalRequestEvent eventMessage, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref state.PublishCalls);
			return bus.PublishAsync(eventMessage, accessor.MessageContext
				?? throw new InvalidOperationException("The real dispatcher did not establish its message context."), cancellationToken);
		}
	}

	private sealed class OwnedHttpFactory(HttpClient client) : Amazon.Runtime.HttpClientFactory
	{
		public override HttpClient CreateHttpClient(IClientConfig clientConfig) => client;
		public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;
		public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
	}

	internal enum ResponseMode { Fail, Success, Block }
	internal sealed class CountingHandler(ResponseMode mode) : HttpMessageHandler
	{
		private int _requests;
		internal int Requests => Volatile.Read(ref _requests);
		internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		[SuppressMessage("Security", "CA5351:DoNotUseBrokenCryptographicAlgorithms",
			Justification = "The SQS response protocol mandates MD5OfMessageBody; this test checksum provides no cryptographic security.")]
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _requests);
			Entered.TrySetResult();
			if (mode == ResponseMode.Block)
			{
				await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
			}
			if (mode == ResponseMode.Fail)
			{
				return new HttpResponseMessage(HttpStatusCode.InternalServerError)
				{
					Content = new StringContent("{\"__type\":\"com.amazonaws.sqs#InternalError\",\"message\":\"CONTROLLED-SQS-500\"}",
						Encoding.UTF8, "application/x-amz-json-1.0"),
				};
			}
			using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
			var messageBody = body.RootElement.GetProperty("MessageBody").GetString()!;
			var md5 = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(messageBody)));
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent($"{{\"MessageId\":\"controlled-message-id\",\"MD5OfMessageBody\":\"{md5}\"}}",
					Encoding.UTF8, "application/x-amz-json-1.0"),
			};
		}
	}
}
