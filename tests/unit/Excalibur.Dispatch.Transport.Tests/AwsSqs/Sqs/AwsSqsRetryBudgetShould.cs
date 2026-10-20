// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.SQS;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Options;

using Polly.Timeout;

using Controls = Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs.AwsSqsPersistedRetryPhysicalRequestsShould;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

/// <summary>Verifies the send retry owner against physical HTTP requests from the actual SQS SDK.</summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "Regression")]
public sealed class AwsSqsRetryBudgetShould
{
	[Theory]
	[InlineData(1, 0)]
	[InlineData(0, 1)]
	public async Task RetireAnOwnedCustomSdkRetryConfigurationBeforeMakingAnyPhysicalRequest(int errorRetries, int staleRetries)
	{
		using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, errorRetries, staleRetries);
		var state = new Controls.PublishState();
		await using var provider = Controls.Build(client, state, SendOptions());
		var message = await Controls.StageAsync(provider, "invalid-owned-sdk-budget").ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("owned-invalid-sqs-sdk-budget");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds the dispatch, so a hidden SDK retry loop fails this test rather than stalling the shard
		(await processor.DispatchPendingMessagesAsync(deadline.Token).ConfigureAwait(false)).ShouldBe(0);
		state.PublishCalls.ShouldBe(1, message.LastError);
		handler.Requests.ShouldBe(0, "a client with hidden SDK retries must be refused before sending");
		message.Status.ShouldBe(OutboxStatus.DeadLettered, message.LastError);
		((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).AssertRetained(message, global::Excalibur.Dispatch.ErrorHandling.DeadLetterReason.UnhandledException, 1);
		message.LastError.ShouldContain("Unhandled exception");
		message.LastError.ShouldNotContain("Max retries exceeded");
	}

	[Theory]
	[InlineData(1, 0)]
	[InlineData(0, 1)]
	public async Task ExposeInvalidSdkRetrySettingsAsAnExplicitConfigurationException(int errorRetries, int staleRetries)
	{
		using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, errorRetries, staleRetries);
		await using var provider = Controls.Build(client, new Controls.PublishState(), SendOptions());
		var error = await Should.ThrowAsync<ConfigurationException>(() =>
			PublishWithRetryOwnerAsync(provider, new BudgetEvent("configuration"))).ConfigureAwait(false);
		error.Message.ShouldContain(errorRetries > 0 ? nameof(AmazonSQSConfig.MaxErrorRetry) : nameof(AmazonSQSConfig.MaxStaleConnectionRetries));
		handler.Requests.ShouldBe(0);
		client.Config.MaxErrorRetry.ShouldBe(errorRetries, "the framework must not mutate a caller-supplied client configuration");
		client.Config.MaxStaleConnectionRetries.ShouldBe(staleRetries);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public async Task BoundForegroundPhysicalRequestsByTheConfiguredFrameworkRetryBudget(int frameworkRetries)
	{
		using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		var options = SendOptions();
		options.MaxRetryAttempts = frameworkRetries;
		await using var provider = Controls.Build(client, new Controls.PublishState(), options);
		var bus = provider.GetRequiredService<AwsSqsMessageBus>();
		using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds the foreground publish, so an unbounded SDK retry budget fails this test rather than stalling the shard
		_ = await Should.ThrowAsync<AmazonSQSException>(() =>
			bus.PublishAsync(new BudgetEvent("foreground"), new MessageContext(), caller.Token)).ConfigureAwait(false);
		handler.Requests.ShouldBe(frameworkRetries + 1, "the foreground policy is the sole owner of HTTP attempts when SDK retries are zero");
		client.Config.MaxErrorRetry.ShouldBe(0);
		client.Config.MaxStaleConnectionRetries.ShouldBe(0);
	}

	[Fact]
	public async Task CancelTheFirstForegroundPhysicalRequestWithoutAnotherRequest()
	{
		using var handler = new Controls.CountingHandler(Controls.ResponseMode.Block);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		await using var provider = Controls.Build(client, new Controls.PublishState(), SendOptions());
		var bus = provider.GetRequiredService<AwsSqsMessageBus>();
		using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds the foreground publish, so an unbounded SDK retry budget fails this test rather than stalling the shard
		var running = bus.PublishAsync(new BudgetEvent("caller-cancel"), new MessageContext(), caller.Token);
		try
		{
			_ = await Task.WhenAny(handler.Entered.Task, running).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
			handler.Entered.Task.IsCompleted.ShouldBeTrue("the configured zero-retry client must reach the actual HTTP boundary");
			await caller.CancelAsync().ConfigureAwait(false);
			_ = await Should.ThrowAsync<OperationCanceledException>(() => running).ConfigureAwait(false);
			handler.Requests.ShouldBe(1);
		}
		finally
		{
			await caller.CancelAsync().ConfigureAwait(false);
			try { await running.ConfigureAwait(false); } catch (OperationCanceledException) { }
		}
	}

	[Fact]
	public async Task EnforceATotalSendDeadlineWhileTheCallerRemainsLive()
	{
		using var handler = new Controls.CountingHandler(Controls.ResponseMode.Block);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		var options = SendOptions();
		options.RequestTimeout = TimeSpan.FromMilliseconds(100);
		await using var provider = Controls.Build(client, new Controls.PublishState(), options);
		var bus = provider.GetRequiredService<AwsSqsMessageBus>();
		using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds the foreground publish, so an unbounded SDK retry budget fails this test rather than stalling the shard
		var running = bus.PublishAsync(new BudgetEvent("send-deadline"), new MessageContext(), caller.Token);
		try
		{
			_ = await Task.WhenAny(handler.Entered.Task, running).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
			handler.Entered.Task.IsCompleted.ShouldBeTrue();
			var completion = await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken))
				.ConfigureAwait(false);
			completion.ShouldBeSameAs(running, "the total send deadline must bound an async SDK call independently of the caller token");
			_ = await Should.ThrowAsync<TimeoutRejectedException>(() => running).ConfigureAwait(false);
			caller.IsCancellationRequested.ShouldBeFalse();
			handler.Requests.ShouldBe(1, "the total deadline must stop the entire retry pipeline, not start another physical request");
		}
		finally
		{
			await caller.CancelAsync().ConfigureAwait(false);
			try { await running.ConfigureAwait(false); }
			catch (OperationCanceledException) { }
			catch (TimeoutRejectedException) { }
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseAMissingOrUnreadableCustomClientConfigurationRatherThanAssumeZeroRetries(bool throws)
	{
		var client = A.Fake<IAmazonSQS>();
		if (throws) { A.CallTo(() => client.Config).Throws(new InvalidOperationException("unreadable custom config")); }
		else { A.CallTo(() => client.Config).Returns(null!); }
		await using var provider = Controls.Build(client, new Controls.PublishState(), SendOptions());
		_ = await Should.ThrowAsync<ConfigurationException>(() =>
			PublishWithRetryOwnerAsync(provider, new BudgetEvent("missing-config")))
			.ConfigureAwait(false);
		A.CallTo(() => client.SendMessageAsync(A<Amazon.SQS.Model.SendMessageRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PreserveConfiguredSdkRetriesForAnUnownedCustomClientWithoutAddingFrameworkRetries()
	{
		using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 1);
		await using var provider = Controls.Build(client, new Controls.PublishState(), SendOptions());
		_ = await Should.ThrowAsync<AmazonSQSException>(() => provider.GetRequiredService<AwsSqsMessageBus>()
			.PublishAsync(new BudgetEvent("unowned-sdk-retries"), new MessageContext(), TestContext.Current.CancellationToken))
			.ConfigureAwait(false);
		handler.Requests.ShouldBe(2, "the caller-configured SDK remains the sole retry owner outside a Dispatch retry scope");
		client.Config.MaxErrorRetry.ShouldBe(1);
	}

	private static async Task PublishWithRetryOwnerAsync(IServiceProvider provider, BudgetEvent message)
	{
		var context = new MessageContext();
		var middleware = ActivatorUtilities.CreateInstance<RetryMiddleware>(provider);
		_ = await middleware.InvokeAsync(message, context, async (original, originalContext, token) =>
		{
			await provider.GetRequiredService<AwsSqsMessageBus>().PublishAsync((BudgetEvent)original, originalContext, token)
				.ConfigureAwait(false);
			return MessageResult.Success();
		}, TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	[Fact]
	public void OwnOnlyTheCachedSendClientAndNeverConstructItForABorrowedOverride()
	{
		var receive = A.Fake<IAmazonSQS>();
		var send = A.Fake<IAmazonSQS>();
		var custom = A.Fake<IAmazonSQS>();
		var creations = 0;
		var registration = new AwsSqsClientRegistration();
		registration.Register(receive, () => { creations++; return send; });
		registration.Resolve(custom, SendOptions()).ShouldBeSameAs(custom);
		creations.ShouldBe(0);
		registration.Resolve(receive, SendOptions()).ShouldBeSameAs(send);
		registration.Resolve(receive, SendOptions()).ShouldBeSameAs(send);
		creations.ShouldBe(1);
		registration.Dispose();
		registration.Dispose();
		A.CallTo(() => send.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => receive.Dispose()).MustNotHaveHappened();
		A.CallTo(() => custom.Dispose()).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RespectANamedCustomFactoryRegisteredBeforeOrAfterTheTransport(bool after)
	{
		var services = RegistrationServices();
		var client = A.Fake<IAmazonSQS>();
		var factories = 0;
		void RegisterCustom() => services.AddKeyedSingleton<IAmazonSQS>("controlled", (_, _) => { factories++; return client; });
		if (!after) { RegisterCustom(); }
		services.AddAwsSqsTransport("controlled", builder => builder.UseRegion("us-east-1").MapQueue<BudgetEvent>(SendOptions().QueueUrl!.ToString()));
		if (after) { RegisterCustom(); }
		await using (var provider = services.BuildServiceProvider())
		{
			_ = provider.GetRequiredKeyedService<AwsSqsMessageBus>("controlled");
			provider.GetRequiredKeyedService<IAmazonSQS>("controlled").ShouldBeSameAs(client);
			provider.GetService<IAmazonSQS>().ShouldBeNull();
			provider.GetService<AwsSqsMessageBus>().ShouldBeNull();
			factories.ShouldBe(1);
			A.CallTo(() => client.Dispose()).MustNotHaveHappened();
		}
		A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveConsumerOwnershipOfANamedExternalInstanceBeforeOrAfterTransportRegistration(bool after)
	{
		var services = RegistrationServices();
		var client = A.Fake<IAmazonSQS>();
		void RegisterCustom() => services.AddKeyedSingleton<IAmazonSQS>("controlled", client);
		if (!after) { RegisterCustom(); }
		services.AddAwsSqsTransport("controlled", builder => builder.UseRegion("us-east-1")
			.MapQueue<BudgetEvent>(SendOptions().QueueUrl!.ToString()));
		if (after) { RegisterCustom(); }
		await using (var provider = services.BuildServiceProvider())
		{
			_ = provider.GetRequiredKeyedService<AwsSqsMessageBus>("controlled");
			provider.GetRequiredKeyedService<IAmazonSQS>("controlled").ShouldBeSameAs(client);
			provider.GetService<IAmazonSQS>().ShouldBeNull();
			provider.GetService<AwsSqsMessageBus>().ShouldBeNull();
		}
		A.CallTo(() => client.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public async Task KeepAnExplicitUnkeyedChannelClientIndependentOfTheNamedTransport()
	{
		var services = RegistrationServices();
		var channelClient = A.Fake<IAmazonSQS>();
		var transportClient = A.Fake<IAmazonSQS>();
		services.AddSingleton(channelClient);
		services.AddKeyedSingleton<IAmazonSQS>("controlled", transportClient);
		services.AddAwsSqsTransport("controlled", builder => builder.UseRegion("us-east-1")
			.MapQueue<BudgetEvent>(SendOptions().QueueUrl!.ToString()));
		services.AddSqsChannelAdapter(options => options.QueueUrl = SendOptions().QueueUrl);
		await using (var provider = services.BuildServiceProvider())
		{
			provider.GetRequiredService<IAmazonSQS>().ShouldBeSameAs(channelClient);
			provider.GetRequiredKeyedService<IAmazonSQS>("controlled").ShouldBeSameAs(transportClient);
			_ = provider.GetRequiredService<SqsChannelAdapter>();
			_ = provider.GetRequiredKeyedService<AwsSqsMessageBus>("controlled");
		}
		A.CallTo(() => channelClient.Dispose()).MustNotHaveHappened();
		A.CallTo(() => transportClient.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public async Task KeepNamedReceiveRetryBudgetsAndSeparateZeroRetrySendClientsIsolated()
	{
		var services = RegistrationServices();
		services.AddAwsSqsTransport("first", builder => builder.UseRegion("us-east-1").MapQueue<BudgetEvent>(SendOptions().QueueUrl!.ToString()).UseMaxRetryAttempts(1).UseRequestTimeout(TimeSpan.FromSeconds(7)));
		services.AddAwsSqsTransport("second", builder => builder.UseRegion("us-west-2").MapQueue<BudgetEvent>(SendOptions().QueueUrl!.ToString()).UseMaxRetryAttempts(2).UseRequestTimeout(TimeSpan.FromSeconds(9)));
		await using var provider = services.BuildServiceProvider();
		foreach (var (name, retries, seconds) in new[] { ("first", 1, 7), ("second", 2, 9) })
		{
			var receive = provider.GetRequiredKeyedService<IAmazonSQS>(name);
			receive.Config.MaxErrorRetry.ShouldBe(retries);
			var options = provider.GetRequiredService<IOptionsMonitor<AwsSqsOptions>>().Get(name);
			options.MaxRetryAttempts.ShouldBe(retries);
			options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(seconds));
			var send = provider.GetRequiredKeyedService<AwsSqsClientRegistration>(name).Resolve(receive, options);
			send.ShouldNotBeSameAs(receive);
			send.Config.MaxErrorRetry.ShouldBe(0);
			send.Config.MaxStaleConnectionRetries.ShouldBe(0);
			send.Config.RegionEndpoint.ShouldBe(receive.Config.RegionEndpoint);
		}
	}

	private static ServiceCollection RegistrationServices()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch();
		services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
		return services;
	}

	[Fact]
	public async Task CaptureLegacyProviderBudgetsAndCredentialsWithoutChangingTheSeparateSqsOptions()
	{
		var services = RegistrationServices();
		var credentials = new Amazon.Runtime.BasicAWSCredentials("controlled-access", "controlled-secret");
		services.Configure<AwsProviderOptions>(options =>
		{
			options.Connection.Credentials = credentials;
			options.MaxRetryAttempts = 1;
			options.RequestTimeout = TimeSpan.FromSeconds(7);
		});
		services.Configure<AwsSqsOptions>(options => options.QueueUrl = SendOptions().QueueUrl);
		services.AddAwsMessageBus(options => { options.EnableSns = false; options.EnableEventBridge = false; });
		await using var provider = services.BuildServiceProvider();
		var receive = provider.GetRequiredService<IAmazonSQS>();
		receive.Config.MaxErrorRetry.ShouldBe(1);
		var sourceOptions = provider.GetRequiredService<IOptions<AwsSqsOptions>>().Value;
		_ = provider.GetRequiredService<AwsSqsMessageBus>();
		sourceOptions.MaxRetryAttempts.ShouldBe(3, "legacy provider policy must not mutate independently registered SQS options");
		var effective = SendOptions();
		var send = provider.GetRequiredService<AwsSqsClientRegistration>().Resolve(receive, effective);
		effective.MaxRetryAttempts.ShouldBe(1);
		effective.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(7));
		send.Config.MaxErrorRetry.ShouldBe(0);
		send.Config.MaxStaleConnectionRetries.ShouldBe(0);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public async Task ApplyTheForegroundSendBudgetToEveryPublishingPath(int path)
	{
		using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		await using var provider = Controls.Build(client, new Controls.PublishState(), SendOptions());
		var bridge = A.Fake<IEnvelopeCloudEventBridge>();
		var encoder = A.Fake<ICloudEventEncoder<Amazon.SQS.Model.SendMessageRequest>>();
		A.CallTo(() => encoder.Options).Returns(new CloudEventOptions());
		A.CallTo(() => bridge.ToTransportAsync<Amazon.SQS.Model.SendMessageRequest>(A<MessageEnvelope>._, A<CloudEventMode>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new Amazon.SQS.Model.SendMessageRequest(SendOptions().QueueUrl!.ToString(), "controlled-cloud-event")));
		await using var bus = new AwsSqsMessageBus(client, provider.GetRequiredService<IPayloadSerializer>(),
			Microsoft.Extensions.Options.Options.Create(SendOptions()), Microsoft.Extensions.Options.Options.Create(new AwsSqsFifoOptions()),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<AwsSqsMessageBus>.Instance,
			path == 3 ? bridge : null, path == 3 ? encoder : null, ownsClient: false);
		var context = new MessageContext();
		_ = await Should.ThrowAsync<AmazonSQSException>(() => path switch
		{
			0 => bus.PublishAsync(new BudgetAction("action"), context, TestContext.Current.CancellationToken),
			1 => bus.PublishAsync(new BudgetEvent("event"), context, TestContext.Current.CancellationToken),
			2 => bus.PublishAsync(new BudgetDocument("document"), context, TestContext.Current.CancellationToken),
			_ => bus.PublishAsync(new BudgetEvent("cloud-event"), context, TestContext.Current.CancellationToken),
		}).ConfigureAwait(false);
		handler.Requests.ShouldBe(2);
	}

	private static AwsSqsOptions SendOptions() => new()
	{
		QueueUrl = new Uri("http://localhost:4566/000000000000/physical-request-controls"),
		MaxRetryAttempts = 1,
		RequestTimeout = TimeSpan.FromSeconds(3),
		RetryPolicy = new TransportRetryPolicyOptions { BaseDelayMs = 1, MaxDelayMs = 2 },
	};

	private sealed record BudgetEvent(string Value) : IDispatchEvent;

	[Theory]
	[InlineData("AccessDenied", 400)]
	[InlineData("QueueDoesNotExist", 400)]
	[InlineData("UnrecognizedClientFailure", 400)]
	public async Task PreserveNonTransientForegroundSdkFailuresWithoutAddingRetries(string code, int status)
	{
		using var handler = new ErrorHandler(code, status);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		await using var provider = Controls.Build(client, new Controls.PublishState(), SendOptions());
		var error = await Should.ThrowAsync<AmazonSQSException>(() => provider.GetRequiredService<AwsSqsMessageBus>()
			.PublishAsync(new BudgetEvent("non-transient"), new MessageContext(), TestContext.Current.CancellationToken)).ConfigureAwait(false);
		error.ErrorCode.ShouldBe(code);
		handler.Requests.ShouldBe(1);
	}

	[Theory]
	[InlineData("AccessDenied")]
	[InlineData("QueueDoesNotExist")]
	[InlineData("InvalidMessageContents")]
	public async Task RetireKnownOwnedSdkFailuresOnTheFirstDurableAttempt(string code)
	{
		using var handler = new ErrorHandler(code, 400);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		var state = new Controls.PublishState();
		await using var provider = Controls.Build(client, state, SendOptions());
		var message = await Controls.StageAsync(provider, "owned-permanent").ConfigureAwait(false);
		var processor = provider.GetRequiredService<IOutboxProcessor>();
		processor.Init("owned-permanent-sdk-failure");
		(await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldBe(0);
		message.Status.ShouldBe(OutboxStatus.DeadLettered, message.LastError);
		((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).AssertRetained(message, code == "InvalidMessageContents"
			? global::Excalibur.Dispatch.ErrorHandling.DeadLetterReason.PoisonMessage
			: global::Excalibur.Dispatch.ErrorHandling.DeadLetterReason.UnhandledException, 1);
		state.PublishCalls.ShouldBe(1);
		handler.Requests.ShouldBe(1);
	}

	[Theory]
	[InlineData("RequestThrottled", 400)]
	[InlineData("KmsThrottled", 400)]
	[InlineData("InternalError", 500)]
	public async Task RetryOnlyEstablishedTransientSdkFailures(string code, int status)
	{
		using var handler = new ErrorHandler(code, status);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		await using var provider = Controls.Build(client, new Controls.PublishState(), SendOptions());
		_ = await Should.ThrowAsync<AmazonSQSException>(() => provider.GetRequiredService<AwsSqsMessageBus>()
			.PublishAsync(new BudgetEvent("transient"), new MessageContext(), TestContext.Current.CancellationToken)).ConfigureAwait(false);
		handler.Requests.ShouldBe(2);
	}

	[Theory]
	[InlineData("AccessDenied")]
	[InlineData("QueueDoesNotExist")]
	[InlineData("InvalidMessageContents")]
	public async Task RetainTheOriginalSdkFaultInTheOwnedFrameworkFailure(string code)
	{
		using var handler = new ErrorHandler(code, 400);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		await using var provider = Controls.Build(client, new Controls.PublishState(), SendOptions());
		var error = await Record.ExceptionAsync(() => PublishWithRetryOwnerAsync(provider, new BudgetEvent("retained-sdk-fault")))
			.ConfigureAwait(false);
		switch (code)
		{
			case "AccessDenied": error.ShouldBeOfType<ConfigurationException>(); break;
			case "QueueDoesNotExist": error.ShouldBeOfType<ConfigurationException>(); break;
			default: error.ShouldBeOfType<DispatchSerializationException>(); break;
		}
		error!.InnerException.ShouldBeAssignableTo<AmazonSQSException>().ErrorCode.ShouldBe(code);
		handler.Requests.ShouldBe(1);
	}

	[Fact]
	public async Task HonorAConsumerClassifierVetoOfAnEstablishedTransientFailure()
	{
		using var handler = new ErrorHandler("InternalError", 500);
		using var http = new HttpClient(handler, disposeHandler: false);
		using var client = Controls.CreateClient(http, maxErrorRetry: 0);
		var classifier = A.Fake<IMessageFailureClassifier>();
		A.CallTo(() => classifier.Classify(A<Exception>._)).Returns(MessageFailureKind.Permanent);
		var send = new AwsSqsSendClient(client, SendOptions(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, classifier);
		_ = await Should.ThrowAsync<AmazonSQSException>(() => send.SendAsync(
			new Amazon.SQS.Model.SendMessageRequest(SendOptions().QueueUrl!.ToString(), "classifier-veto"),
			new BudgetEvent("classifier-veto"), new MessageContext(), TestContext.Current.CancellationToken)).ConfigureAwait(false);
		handler.Requests.ShouldBe(1);
		A.CallTo(() => classifier.Classify(A<Exception>._)).MustHaveHappenedOnceExactly();
	}

	private sealed class ErrorHandler(string code, int status) : HttpMessageHandler
	{
		private int _requests;
		internal int Requests => Volatile.Read(ref _requests);
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Increment(ref _requests);
			return Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)status)
			{
				Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
				{
					["__type"] = "com.amazonaws.sqs#" + code,
					["message"] = "controlled SQS failure",
				}), System.Text.Encoding.UTF8, "application/x-amz-json-1.0"),
			});
		}
	}

	[Fact]
	public async Task AvoidRetryingAnHttpResponseFaultAsThoughItWereANetworkFailure()
	{
		var client = A.Fake<IAmazonSQS>();
		A.CallTo(() => client.Config).Returns(new AmazonSQSConfig { MaxErrorRetry = 0, MaxStaleConnectionRetries = 0 });
		var fault = new HttpRequestException("controlled forbidden response", null, System.Net.HttpStatusCode.Forbidden);
		A.CallTo(() => client.SendMessageAsync(A<Amazon.SQS.Model.SendMessageRequest>._, A<CancellationToken>._)).ThrowsAsync(fault);
		var send = new AwsSqsSendClient(client, SendOptions(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
		var observed = await Should.ThrowAsync<HttpRequestException>(() => send.SendAsync(
			new Amazon.SQS.Model.SendMessageRequest(SendOptions().QueueUrl!.ToString(), "http-response-fault"),
			new BudgetEvent("http-response-fault"), new MessageContext(), TestContext.Current.CancellationToken)).ConfigureAwait(false);
		observed.ShouldBeSameAs(fault);
		A.CallTo(() => client.SendMessageAsync(A<Amazon.SQS.Model.SendMessageRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private sealed record BudgetAction(string Value) : IDispatchAction;
	private sealed record BudgetDocument(string Value) : IDispatchDocument;
}
