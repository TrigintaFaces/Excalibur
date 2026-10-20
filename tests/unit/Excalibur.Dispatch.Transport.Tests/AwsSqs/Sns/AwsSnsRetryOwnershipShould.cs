// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System.Net;
using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Controls = Excalibur.Dispatch.Transport.Tests.AwsSqs.Sns.AwsSnsPersistedRetryPhysicalRequestsShould;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sns;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "Regression")]
public sealed class AwsSnsRetryOwnershipShould
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public async Task RejectHiddenSdkRetriesBeforeAnOwnedPublish(int errorRetries, int staleRetries)
    {
        using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = Controls.CreateClient(http, errorRetries, staleRetries);
        await using var provider = Controls.Build(client, new Controls.PublishState());
        var message = new OwnershipEvent();
        var context = new MessageContext();
        var middleware = ActivatorUtilities.CreateInstance<RetryMiddleware>(provider);
        var error = await Should.ThrowAsync<ConfigurationException>(async () =>
        {
            _ = await middleware.InvokeAsync(message, context, async (original, originalContext, token) =>
            {
                await provider.GetRequiredService<AwsSnsMessageBus>().PublishAsync((OwnershipEvent)original, originalContext, token).ConfigureAwait(false);
                return MessageResult.Success();
            }, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        error.Message.ShouldContain(errorRetries > 0 ? nameof(AmazonSimpleNotificationServiceConfig.MaxErrorRetry) : nameof(AmazonSimpleNotificationServiceConfig.MaxStaleConnectionRetries));
        handler.Requests.ShouldBe(0);
        client.Config.MaxErrorRetry.ShouldBe(errorRetries);
        client.Config.MaxStaleConnectionRetries.ShouldBe(staleRetries);
    }

    [Fact]
    public async Task LeaveABorrowedClientAliveAfterBusDisposal()
    {
        var client = A.Fake<IAmazonSimpleNotificationService>();
        var serializer = A.Fake<IPayloadSerializer>();
        var bus = new AwsSnsMessageBus(client, serializer, new AwsSnsOptions(), NullLogger<AwsSnsMessageBus>.Instance);
        await bus.DisposeAsync().ConfigureAwait(false);
        await bus.DisposeAsync().ConfigureAwait(false);
        A.CallTo(() => client.Dispose()).MustNotHaveHappened();
    }

    [Fact]
    public async Task UseEachNamedClientForItsOwnEncryptionTopic()
    {
        var orders = A.Fake<IAmazonSimpleNotificationService>();
        var payments = A.Fake<IAmazonSimpleNotificationService>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAmazonSimpleNotificationService>(orders);
        services.AddKeyedSingleton<IAmazonSimpleNotificationService>("orders", orders);
        services.AddKeyedSingleton<IAmazonSimpleNotificationService>("payments", payments);
        services.AddAwsSnsTransport("orders", sns => sns.TopicArn("arn:aws:sns:us-east-1:000000000000:orders").EnableEncryption("alias/orders"));
        services.AddAwsSnsTransport("payments", sns => sns.TopicArn("arn:aws:sns:us-west-2:000000000000:payments").EnableEncryption("alias/payments"));
        await using var provider = services.BuildServiceProvider();
        foreach (var applier in provider.GetServices<IHostedService>().OfType<AwsSnsTopicEncryptionService>())
        {
            await applier.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
        A.CallTo(() => orders.SetTopicAttributesAsync(A<SetTopicAttributesRequest>.That.Matches(request => request.TopicArn.EndsWith(":orders", StringComparison.Ordinal) && request.AttributeValue == "alias/orders"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => payments.SetTopicAttributesAsync(A<SetTopicAttributesRequest>.That.Matches(request => request.TopicArn.EndsWith(":payments", StringComparison.Ordinal) && request.AttributeValue == "alias/payments"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => orders.SetTopicAttributesAsync(A<SetTopicAttributesRequest>.That.Matches(request => request.TopicArn.EndsWith(":payments", StringComparison.Ordinal)), A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => payments.SetTopicAttributesAsync(A<SetTopicAttributesRequest>.That.Matches(request => request.TopicArn.EndsWith(":orders", StringComparison.Ordinal)), A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ResolveDistinctNamedBusesAndManagementClients()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
        foreach (var name in new[] { "orders", "payments" })
        {
            services.AddAwsSnsTransport(name, sns => sns.TopicArn($"arn:aws:sns:us-east-1:000000000000:{name}").ConfigureOptions(options =>
            {
                options.Connection.ServiceUrl = new Uri($"http://{name}.localhost:4566");
                options.Connection.AccessKey = "controlled-access";
                options.Connection.SecretKey = "controlled-secret";
                options.Connection.MaxErrorRetry = 1;
                options.Connection.Timeout = TimeSpan.FromSeconds(7);
            }));
        }
        await using var provider = services.BuildServiceProvider();
        var orders = provider.GetRequiredKeyedService<IAmazonSimpleNotificationService>("orders");
        var payments = provider.GetRequiredKeyedService<IAmazonSimpleNotificationService>("payments");
        orders.ShouldNotBeSameAs(payments);
        orders.Config.ServiceURL.ShouldContain("orders.localhost");
        payments.Config.ServiceURL.ShouldContain("payments.localhost");
        orders.Config.MaxErrorRetry.ShouldBe(1);
        payments.Config.Timeout.ShouldBe(TimeSpan.FromSeconds(7));
        provider.GetRequiredKeyedService<AwsSnsMessageBus>("orders").ShouldNotBeSameAs(provider.GetRequiredKeyedService<AwsSnsMessageBus>("payments"));
    }

    private sealed record OwnershipEvent : IDispatchEvent;

    [Fact]
    public void OwnOnlyTheLazySendClientAndLeaveOverridesUntouched()
    {
        var management = A.Fake<IAmazonSimpleNotificationService>();
        var send = A.Fake<IAmazonSimpleNotificationService>();
        var borrowed = A.Fake<IAmazonSimpleNotificationService>();
        var creations = 0;
        var registration = new AwsSnsClientRegistration();
		registration.Register(management, () => { creations++; return send; });
        registration.Resolve(borrowed).ShouldBeSameAs(borrowed);
        creations.ShouldBe(0);
        registration.Resolve(management).ShouldBeSameAs(send);
        registration.Resolve(management).ShouldBeSameAs(send);
        creations.ShouldBe(1);
        registration.Dispose();
        registration.Dispose();
        A.CallTo(() => send.Dispose()).MustHaveHappenedOnceExactly();
        A.CallTo(() => management.Dispose()).MustNotHaveHappened();
        A.CallTo(() => borrowed.Dispose()).MustNotHaveHappened();
        Should.Throw<ObjectDisposedException>(() => registration.Resolve(management));
    }

    [Fact]
    public async Task DisposeAnExplicitlyOwnedBusClientExactlyOnce()
    {
        var client = A.Fake<IAmazonSimpleNotificationService>();
        var bus = new AwsSnsMessageBus(client, A.Fake<IPayloadSerializer>(), new AwsSnsOptions(),
            NullLogger<AwsSnsMessageBus>.Instance, ownsClient: true);
        await bus.DisposeAsync().ConfigureAwait(false);
        await bus.DisposeAsync().ConfigureAwait(false);
        A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("action")]
    [InlineData("event")]
    [InlineData("document")]
    public async Task PreserveNativePayloadBytesForEachPublishPath(string kind)
    {
        var client = A.Fake<IAmazonSimpleNotificationService>();
        var serializer = A.Fake<IPayloadSerializer>();
        byte[] bytes = [0x01, 0x00, 0xff, 0x43];
        A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).Returns(bytes);
        PublishRequest? captured = null;
        A.CallTo(() => client.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
            .Invokes((PublishRequest request, CancellationToken _) => captured = request)
            .Returns(Task.FromResult(new PublishResponse { MessageId = "accepted" }));
        await using var bus = new AwsSnsMessageBus(client, serializer,
            new AwsSnsOptions { TopicArn = "arn:aws:sns:us-east-1:000000000000:bytes" }, NullLogger<AwsSnsMessageBus>.Instance);
        var context = new MessageContext();
        var publishing = kind switch
        {
            "action" => bus.PublishAsync(new OwnershipAction(), context, TestContext.Current.CancellationToken),
            "document" => bus.PublishAsync(new OwnershipDocument(), context, TestContext.Current.CancellationToken),
            _ => bus.PublishAsync(new OwnershipEvent(), context, TestContext.Current.CancellationToken),
        };
        await publishing.ConfigureAwait(false);
        captured.ShouldNotBeNull();
        Convert.FromBase64String(captured.Message).ShouldBe(bytes);
        A.CallTo(() => client.PublishAsync(A<PublishRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RefuseAPreCanceledCallerBeforePublishing()
    {
        var client = A.Fake<IAmazonSimpleNotificationService>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(false);
        await using var bus = new AwsSnsMessageBus(client, A.Fake<IPayloadSerializer>(), new AwsSnsOptions(), NullLogger<AwsSnsMessageBus>.Instance);
        _ = await Should.ThrowAsync<OperationCanceledException>(() => bus.PublishAsync(new OwnershipEvent(), new MessageContext(), cancellation.Token)).ConfigureAwait(false);
        A.CallTo(() => client.PublishAsync(A<PublishRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private sealed record OwnershipAction : IDispatchAction;
    private sealed record OwnershipDocument : IDispatchDocument;

    [Theory]
    [InlineData("AuthorizationError", 403)]
    [InlineData("InvalidSecurity", 403)]
    [InlineData("KMSAccessDenied", 400)]
    [InlineData("KMSDisabled", 400)]
    [InlineData("KMSNotFound", 400)]
    [InlineData("NotFound", 404)]
    public async Task RetireKnownAuthorizationAndConfigurationFailuresWithoutRepeatingHttp(string code, int status)
    {
        using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail, code, (HttpStatusCode)status);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = Controls.CreateClient(http, maxErrorRetry: 0);
        var state = new Controls.PublishState();
        await using var provider = Controls.Build(client, state);
        var message = await Controls.StageAsync(provider, "known-permanent").ConfigureAwait(false);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("sns-known-permanent");
        _ = await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        message.Status.ShouldBe(OutboxStatus.DeadLettered, message.LastError);
        ((global::Excalibur.Dispatch.Transport.Tests.AwsSqs.AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>()).AssertRetained(message, global::Excalibur.Dispatch.ErrorHandling.DeadLetterReason.UnhandledException, 1);
        message.RetryCount.ShouldBe(1, "terminal retirement records the first observed attempt");
        handler.Requests.ShouldBe(1);
        state.PublishCalls.ShouldBe(1);
    }

    [Fact]
    public async Task KeepKmsThrottlingInThePersistedRetryBudget()
    {
        using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail, "KMSThrottling", HttpStatusCode.BadRequest);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = Controls.CreateClient(http, maxErrorRetry: 0);
        await using var provider = Controls.Build(client, new Controls.PublishState());
        var message = await Controls.StageAsync(provider, "kms-throttling").ConfigureAwait(false);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("sns-kms-throttling");
        _ = await processor.DispatchPendingMessagesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        message.Status.ShouldBe(OutboxStatus.Failed, message.LastError);
        message.RetryCount.ShouldBe(1);
        handler.Requests.ShouldBe(1);
    }

    [Theory]
    [InlineData("action")]
    [InlineData("document")]
    [InlineData("cloud-event")]
    public async Task ApplyTheOwnedSdkGuardToEveryPublishPath(string path)
    {
        using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = Controls.CreateClient(http, maxErrorRetry: 1);
        await using var provider = Controls.Build(client, new Controls.PublishState());
        var ceOptions = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());
        var mapper = new AwsSnsCloudEventAdapter(ceOptions, NullLogger<AwsSnsCloudEventAdapter>.Instance);
        var bridge = new EnvelopeCloudEventBridge(new CloudEventEnvelopeConverter(ceOptions.Value),
            [new CloudEventEncoderAdapter<PublishRequest>(mapper)]);
        await using var bus = new AwsSnsMessageBus(client, provider.GetRequiredService<IPayloadSerializer>(),
            new AwsSnsOptions { TopicArn = "arn:aws:sns:us-east-1:000000000000:guard" }, NullLogger<AwsSnsMessageBus>.Instance,
            path == "cloud-event" ? bridge : null, path == "cloud-event" ? mapper : null);
        IDispatchMessage message = path switch
        {
            "action" => new OwnershipAction(),
            "document" => new OwnershipDocument(),
            _ => new OwnershipEvent(),
        };
        var middleware = ActivatorUtilities.CreateInstance<RetryMiddleware>(provider);
        _ = await Should.ThrowAsync<ConfigurationException>(async () =>
        {
            _ = await middleware.InvokeAsync(message, new MessageContext(), async (original, originalContext, token) =>
            {
                await (original switch
                {
                    IDispatchAction action => bus.PublishAsync(action, originalContext, token),
                    IDispatchDocument document => bus.PublishAsync(document, originalContext, token),
                    IDispatchEvent evt => bus.PublishAsync(evt, originalContext, token),
                    _ => throw new InvalidOperationException("Unexpected control message."),
                }).ConfigureAwait(false);
                return MessageResult.Success();
            }, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        handler.Requests.ShouldBe(0);
    }

    [Fact]
    public async Task PreserveExplicitForegroundSettingsSeparatelyFromManagementSettings()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer(), setAsCurrent: true);
        services.AddAwsSnsTransport("budgets", sns => sns.TopicArn("arn:aws:sns:us-east-1:000000000000:budgets").ConfigureOptions(options =>
        {
            options.Connection.AccessKey = "controlled-access";
            options.Connection.SecretKey = "controlled-secret";
            options.Connection.ServiceUrl = new Uri("http://localhost:4566");
            options.Connection.MaxErrorRetry = 3;
            options.Connection.Timeout = TimeSpan.FromSeconds(30);
            options.MaxRetryAttempts = 0;
            options.RequestTimeout = TimeSpan.FromSeconds(1);
        }));
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AwsSnsOptions>>().Get("budgets");
        options.MaxRetryAttempts.ShouldBe(0);
        options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(1));
        _ = provider.GetRequiredKeyedService<AwsSnsMessageBus>("budgets");
        options.MaxRetryAttempts.ShouldBe(0, "resolving the bus must not mutate the options monitor's explicit foreground budget");
        options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(1));
        var management = provider.GetRequiredKeyedService<IAmazonSimpleNotificationService>("budgets");
        management.Config.MaxErrorRetry.ShouldBe(3);
        management.Config.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task UseTheForegroundBudgetWhenTheSdkHasNoRetries(int retries)
    {
        using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = Controls.CreateClient(http, maxErrorRetry: 0);
        await using var provider = Controls.Build(client, new Controls.PublishState());
        await using var bus = new AwsSnsMessageBus(client, provider.GetRequiredService<IPayloadSerializer>(),
            new AwsSnsOptions { TopicArn = "arn:aws:sns:us-east-1:000000000000:foreground", MaxRetryAttempts = retries },
            NullLogger<AwsSnsMessageBus>.Instance);
        _ = await Should.ThrowAsync<AmazonSimpleNotificationServiceException>(() =>
            bus.PublishAsync(new OwnershipEvent(), new MessageContext(), TestContext.Current.CancellationToken)).ConfigureAwait(false);
        handler.Requests.ShouldBe(retries + 1);
    }

    [Fact]
    public async Task LeaveTheRetryBudgetWithABorrowedSdkThatHasRetries()
    {
        using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = Controls.CreateClient(http, maxErrorRetry: 1);
        await using var provider = Controls.Build(client, new Controls.PublishState(), new AwsSnsOptions
        {
            TopicArn = "arn:aws:sns:us-east-1:000000000000:borrowed", MaxRetryAttempts = 4,
        });
        _ = await Should.ThrowAsync<AmazonSimpleNotificationServiceException>(() => provider.GetRequiredService<AwsSnsMessageBus>()
            .PublishAsync(new OwnershipEvent(), new MessageContext(), TestContext.Current.CancellationToken)).ConfigureAwait(false);
        handler.Requests.ShouldBe(2, "the transport must not multiply the borrowed SDK's retry budget");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefuseToBorrowAnUnrelatedMessagesRetryOwner(bool differentMessage)
    {
        using var handler = new Controls.CountingHandler(Controls.ResponseMode.Fail);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = Controls.CreateClient(http, maxErrorRetry: 1);
        await using var provider = Controls.Build(client, new Controls.PublishState());
        var middleware = ActivatorUtilities.CreateInstance<RetryMiddleware>(provider);
        var original = new OwnershipEvent();
        Exception? error = null;
        _ = await middleware.InvokeAsync(original, new MessageContext(), async (message, context, token) =>
        {
            error = await Record.ExceptionAsync(() => provider.GetRequiredService<AwsSnsMessageBus>()
                .PublishAsync(differentMessage ? new OwnershipEvent() : (OwnershipEvent)message,
                    differentMessage ? context : new MessageContext(), token)).ConfigureAwait(false);
            return MessageResult.Success();
        }, TestContext.Current.CancellationToken).ConfigureAwait(false);
        error.ShouldBeAssignableTo<AmazonSimpleNotificationServiceException>();
        handler.Requests.ShouldBe(2, "an unrelated message or context must use its own SDK budget, not the dispatch owner's guard");
    }

    [Theory]
    [InlineData("action")]
    [InlineData("document")]
    [InlineData("event")]
    [InlineData("cloud-event")]
    public async Task DeliverEachPublishPathThroughTheActualSdkOnce(string path)
    {
        using var handler = new Controls.CountingHandler(Controls.ResponseMode.Success);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = Controls.CreateClient(http, maxErrorRetry: 0);
        await using var provider = Controls.Build(client, new Controls.PublishState());
        var ceOptions = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());
        var mapper = new AwsSnsCloudEventAdapter(ceOptions, NullLogger<AwsSnsCloudEventAdapter>.Instance);
        var bridge = new EnvelopeCloudEventBridge(new CloudEventEnvelopeConverter(ceOptions.Value),
            [new CloudEventEncoderAdapter<PublishRequest>(mapper)]);
        await using var bus = new AwsSnsMessageBus(client, provider.GetRequiredService<IPayloadSerializer>(),
            new AwsSnsOptions { TopicArn = "arn:aws:sns:us-east-1:000000000000:positive" }, NullLogger<AwsSnsMessageBus>.Instance,
            path == "cloud-event" ? bridge : null, path == "cloud-event" ? mapper : null);
        var context = new MessageContext();
        await (path switch
        {
            "action" => bus.PublishAsync(new OwnershipAction(), context, TestContext.Current.CancellationToken),
            "document" => bus.PublishAsync(new OwnershipDocument(), context, TestContext.Current.CancellationToken),
            _ => bus.PublishAsync(new OwnershipEvent(), context, TestContext.Current.CancellationToken),
        }).ConfigureAwait(false);
        handler.Requests.ShouldBe(1);
    }
}
