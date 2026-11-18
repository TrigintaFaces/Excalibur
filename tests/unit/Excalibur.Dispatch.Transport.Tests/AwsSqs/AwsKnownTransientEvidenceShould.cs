// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Dispatch.Transport.Aws;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Transport")]
[Trait("Pattern", "Regression")]
public sealed class AwsKnownTransientEvidenceShould
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task PreserveOriginalSdkIdentityAndOnlyEstablishedTransientEvidenceAfterOwnerExit(bool sns, bool known)
	{
		var message = A.Fake<IDispatchEvent>();
		var context = new MessageContext();
		var services = new ServiceCollection();
		services.AddDispatch(static _ => { });
		using var provider = services.BuildServiceProvider();
		var classifier = provider.GetRequiredService<IMessageFailureClassifier>();
		var middleware = new RetryMiddleware(global::Microsoft.Extensions.Options.Options.Create(new RetryOptions
		{
			MaxRetryAttempts = 0,
			BaseDelay = TimeSpan.Zero,
			MaxDelay = TimeSpan.Zero,
			UseJitter = false,
		}), A.Fake<ITelemetrySanitizer>(), NullLogger<RetryMiddleware>.Instance, classifier);
		Exception original;
		Exception? observed;
		{
			if (sns)
			{
				var client = A.Fake<IAmazonSimpleNotificationService>();
				A.CallTo(() => client.Config).Returns(new AmazonSimpleNotificationServiceConfig { MaxErrorRetry = 0, MaxStaleConnectionRetries = 0 });
				original = new AmazonSimpleNotificationServiceException("controlled original") { ErrorCode = known ? "InternalError" : "UnrecognizedClientFailure", StatusCode = known ? HttpStatusCode.InternalServerError : HttpStatusCode.BadRequest };
				A.CallTo(() => client.PublishAsync(A<PublishRequest>._, A<CancellationToken>._)).Returns(Task.FromException<PublishResponse>(original));
				var send = new AwsSnsSendClient(client, new AwsSnsOptions { MaxRetryAttempts = 1 }, NullLogger.Instance);
				observed = await Record.ExceptionAsync(() => InvokeOwnedAsync(middleware, message, context, token => send.SendAsync(new PublishRequest(), message, context, token)));
				A.CallTo(() => client.PublishAsync(A<PublishRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			}
			else
			{
				var client = A.Fake<IAmazonSQS>();
				A.CallTo(() => client.Config).Returns(new AmazonSQSConfig { MaxErrorRetry = 0, MaxStaleConnectionRetries = 0 });
				original = new AmazonSQSException("controlled original") { ErrorCode = known ? "InternalError" : "UnrecognizedClientFailure", StatusCode = known ? HttpStatusCode.InternalServerError : HttpStatusCode.BadRequest };
				A.CallTo(() => client.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._)).Returns(Task.FromException<SendMessageResponse>(original));
				var send = new AwsSqsSendClient(client, new AwsSqsOptions { MaxRetryAttempts = 1 }, NullLogger.Instance);
				observed = await Record.ExceptionAsync(() => InvokeOwnedAsync(middleware, message, context, token => send.SendAsync(new SendMessageRequest(), message, context, token)));
				A.CallTo(() => client.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			}
		}
		observed.ShouldBeSameAs(original);
		classifier.Classify(observed!).ShouldBe(known ? MessageFailureKind.Transient : MessageFailureKind.Unknown);
	}
	private static Task<IMessageResult> InvokeOwnedAsync(RetryMiddleware middleware, IDispatchMessage message,
		IMessageContext context, Func<CancellationToken, Task> send) => middleware.InvokeAsync(message, context,
		async (_, _, token) =>
		{
			await send(token).ConfigureAwait(false);
			return MessageResult.Success();
		}, TestContext.Current.CancellationToken).AsTask();

}
