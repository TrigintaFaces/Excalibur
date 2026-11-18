// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Middleware;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
[Trait("Pattern", "Regression")]
public sealed class PoisonFirstAttemptMetadataCollisionShould
{
	[Fact]
	public async Task KeepOriginalProducerMetadataAndOneInternalTimestampAcrossAttempts()
	{
		var metadata = new Dictionary<string, string?>
		{
			["FirstAttemptTime"] = "producer-owned-value",
			["business.reference"] = "order-42",
		};
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);
		using var middleware = NewMiddleware(new TimespanPoisonDetector(Options()), A.Fake<IPoisonMessageHandler>());
		var message = A.Fake<IDispatchMessage>();

		await middleware.InvokeAsync(message, context, Success, CancellationToken.None);
		var first = context.GetProcessingFeature().ShouldNotBeNull().FirstAttemptTime;
		first.ShouldNotBeNull();

		// The identical ingress metadata is reapplied during a retry on this context.
		foreach (var entry in metadata) { context.Items[entry.Key] = entry.Value!; }
		await middleware.InvokeAsync(message, context, Success, CancellationToken.None);

		context.GetProcessingFeature().ShouldNotBeNull().FirstAttemptTime.ShouldBe(first);
		context.Items["FirstAttemptTime"].ShouldBe("producer-owned-value");
		context.Items["business.reference"].ShouldBe("order-42");
		metadata["FirstAttemptTime"].ShouldBe("producer-owned-value");
	}

	[Fact]
	public async Task DetectAgeFromRetainedTypedEvidenceDespiteTheOriginalMetadataString()
	{
		var context = DispatchContextInitializer.CreateFromMetadata(new Dictionary<string, string?>
		{
			["FirstAttemptTime"] = "producer-owned-value",
			["business.reference"] = "order-42",
		});
		var retainedFirst = DateTimeOffset.UtcNow.AddDays(-1);
		context.GetOrCreateProcessingFeature().FirstAttemptTime = retainedFirst;
		var handler = A.Fake<IPoisonMessageHandler>();
		A.CallTo(() => handler.HandlePoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<string>._, A<CancellationToken>._, A<Exception?>._))
			.Returns(Task.CompletedTask);
		using var middleware = NewMiddleware(new TimespanPoisonDetector(Options()), handler);

		var result = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), context, Fail, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		context.GetProcessingFeature().ShouldNotBeNull().FirstAttemptTime.ShouldBe(retainedFirst);
		context.Items["FirstAttemptTime"].ShouldBe("producer-owned-value");
		context.Items["business.reference"].ShouldBe("order-42");
		A.CallTo(() => handler.HandlePoisonMessageAsync(
			A<IDispatchMessage>._, context, A<string>._, A<CancellationToken>._, A<Exception?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreserveAProgrammaticLegacyTimestampWhenInitializingTheTypedFeature()
	{
		var context = new MessageContext();
		var retainedFirst = DateTimeOffset.UtcNow.AddDays(-1);
		context.Items["FirstAttemptTime"] = retainedFirst;
		using var middleware = NewMiddleware(new TimespanPoisonDetector(Options()), A.Fake<IPoisonMessageHandler>());

		await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), context, Success, CancellationToken.None);

		context.GetProcessingFeature().ShouldNotBeNull().FirstAttemptTime.ShouldBe(retainedFirst);
		context.Items["FirstAttemptTime"].ShouldBe(retainedFirst);
	}

	[Fact]
	public async Task DoNotInterpretAnOldProducerStringAsInternalAttemptEvidence()
	{
		var context = DispatchContextInitializer.CreateFromMetadata(new Dictionary<string, string?>
		{
			["FirstAttemptTime"] = "1990-01-01T00:00:00Z",
		});
		var handler = A.Fake<IPoisonMessageHandler>();
		using var middleware = NewMiddleware(new TimespanPoisonDetector(Options()), handler);

		var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), context, Fail, CancellationToken.None));

		failure.Message.ShouldBe("ordinary-handler-failure");
		context.Items["FirstAttemptTime"].ShouldBe("1990-01-01T00:00:00Z");
		A.CallTo(() => handler.HandlePoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<string>._, A<CancellationToken>._, A<Exception?>._))
			.MustNotHaveHappened();
	}

	private static IOptions<PoisonMessageOptions> Options() =>
		Microsoft.Extensions.Options.Options.Create(new PoisonMessageOptions
		{
			Enabled = true,
			MaxProcessingTime = TimeSpan.FromHours(1),
		});

	private static PoisonMessageMiddleware NewMiddleware(IPoisonMessageDetector detector, IPoisonMessageHandler handler) =>
		new(detector, handler, Options(), NullLogger<PoisonMessageMiddleware>.Instance);

	private static ValueTask<IMessageResult> Success(IDispatchMessage message, IMessageContext context, CancellationToken token) =>
		new(MessageResult.Success());

	private static ValueTask<IMessageResult> Fail(IDispatchMessage message, IMessageContext context, CancellationToken token) =>
		throw new InvalidOperationException("ordinary-handler-failure");
}
