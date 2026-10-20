// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
		using var handler = new CountingHandler(ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = CreateClient(http, maxErrorRetry: 0);
		var state = new PublishState();
		await using var provider = Build(client, state);
		var message = await StageAsync(provider, "durable-two-attempts").ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("sqs-physical-characterization");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		message.Status.ShouldBe(OutboxStatus.Failed, Diagnostics(message, state, handler));
		message.RetryCount.ShouldBe(1);
		state.PublishCalls.ShouldBe(1, Diagnostics(message, state, handler));
		handler.Requests.ShouldBe(1, Diagnostics(message, state, handler));
		await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token).ConfigureAwait(false);
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		state.PublishCalls.ShouldBe(2, "each durable attempt invokes the bus once, without middleware multiplication");
		handler.Requests.ShouldBe(2, "each durable attempt must make one physical request with no SDK or foreground retry multiplication");
		message.Status.ShouldBe(OutboxStatus.DeadLettered, Diagnostics(message, state, handler));
		((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).AssertRetained(message, global::Excalibur.Dispatch.ErrorHandling.DeadLetterReason.MaxRetriesExceeded, 2);
		message.RetryCount.ShouldBe(2, "terminal retirement records both observed attempts; handler and HTTP counts prove the final attempt");
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		handler.Requests.ShouldBe(2, "the terminal outbox row must not start another SDK call");
	}

	[Fact]
	public async Task DeliverASuccessfulPersistedAttemptWithOnePhysicalRequest()
	{
		using var handler = new CountingHandler(ResponseMode.Success);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = CreateClient(http, maxErrorRetry: 0);
		var state = new PublishState();
		await using var provider = Build(client, state);
		var message = await StageAsync(provider, "one-successful-request").ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("sqs-physical-success");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false))
			.ShouldBe(1, Diagnostics(message, state, handler));
		message.Status.ShouldBe(OutboxStatus.Sent);
		((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).Entries.ShouldBeEmpty();
		message.RetryCount.ShouldBe(0);
		state.PublishCalls.ShouldBe(1);
		handler.Requests.ShouldBe(1);
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		handler.Requests.ShouldBe(1);
	}

	[Fact]
	public async Task CancelABlockedFirstPhysicalRequestWithoutRetryingOrConsumingThePersistedFailureBudget()
	{
		using var handler = new CountingHandler(ResponseMode.Block);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = CreateClient(http, maxErrorRetry: 0);
		var state = new PublishState();
		await using var provider = Build(client, state);
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
			message.Status.ShouldNotBe(OutboxStatus.Sent);
			message.Status.ShouldNotBe(OutboxStatus.DeadLettered);
			message.RetryCount.ShouldBe(0, "caller cancellation is not a retry-consuming transport failure");
			((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).Entries.ShouldBeEmpty();
		}
		finally
		{
			await cancellation.CancelAsync().ConfigureAwait(false);
			try { _ = await running.ConfigureAwait(false); }
			catch (OperationCanceledException) { }
		}
	}

	internal static AmazonSQSClient CreateClient(HttpClient http, int maxErrorRetry = 1, int maxStaleConnectionRetries = 0) =>
		new(new BasicAWSCredentials("controlled-access", "controlled-secret"),
		new AmazonSQSConfig
		{
			ServiceURL = "http://localhost:4566", AuthenticationRegion = "us-east-1", UseHttp = true,
			RetryMode = RequestRetryMode.Standard, MaxErrorRetry = maxErrorRetry,
			MaxStaleConnectionRetries = maxStaleConnectionRetries, HttpClientFactory = new OwnedHttpFactory(http),
		});

	internal static ServiceProvider Build(IAmazonSQS client, PublishState state, AwsSqsOptions? sendOptions = null)
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
			NullLogger<AwsSqsMessageBus>.Instance, ownsClient: false));
		services.AddScoped(provider => new PublishHandler(provider.GetRequiredService<AwsSqsMessageBus>(),
			provider.GetRequiredService<IMessageContextAccessor>(), state));
		services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory().WithProcessing(processing =>
			processing.MaxRetryCount(1).PollingInterval(TimeSpan.FromMilliseconds(20)).RetryDelay(TimeSpan.FromMilliseconds(10)))));
		services.Configure<InMemoryOutboxOptions>(options => options.FailureBackoffFloorSeconds = 1);
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
