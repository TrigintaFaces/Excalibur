// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling.PoisonMessage;

[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
[Trait("Pattern", "Regression")]
public sealed class PoisonDetectorCompositionShould
{
	[Theory]
	[InlineData("retry")]
	[InlineData("exception")]
	[InlineData("age")]
	public async Task ReachEachDefaultDetectorThroughRegisteredMiddleware(string detector)
	{
		var handler = A.Fake<IPoisonMessageHandler>();
		var services = CreateServices(handler);
		services.AddPoisonMessageHandling(options =>
		{
			options.MaxRetryAttempts = detector == "retry" ? 1 : 100;
			options.MaxProcessingTime = TimeSpan.FromHours(1);
		});
		using var provider = services.BuildServiceProvider(validateScopes: true);
		var context = new MessageContext { MessageId = "default-" + detector };
		if (detector == "age")
		{
			context.Items["FirstAttemptTime"] = DateTimeOffset.UtcNow.AddHours(-2);
		}

		Exception fault = detector == "exception"
			? new FormatException("invalid payload")
			: new TimeoutException("retryable processing fault");
		var message = new TestMessage();
		var result = await provider.GetRequiredService<PoisonMessageMiddleware>().InvokeAsync(
			message, context, (_, _, _) => throw fault, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		A.CallTo(() => handler.HandlePoisonMessageAsync(
			message, context, A<string>._, CancellationToken.None, fault)).MustHaveHappenedOnceExactly();
		var detectors = provider.GetServices<IPoisonMessageDetector>().ToArray();
		detectors.Length.ShouldBe(3);
		detectors.ShouldContain(item => item is RetryCountPoisonDetector);
		detectors.ShouldContain(item => item is ExceptionTypePoisonDetector);
		detectors.ShouldContain(item => item is TimespanPoisonDetector);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task IncludeCustomDetectorsBeforeOrAfterHandlingWithoutDisplacingAggregate(bool registerBefore)
	{
		var handler = A.Fake<IPoisonMessageHandler>();
		var services = CreateServices(handler);
		if (registerBefore)
		{
			services.AddPoisonMessageDetector<ObservingDetector>();
		}
		services.AddPoisonMessageHandling();
		if (!registerBefore)
		{
			services.AddPoisonMessageDetector<ObservingDetector>();
		}
		services.AddPoisonMessageDetector<TerminalDetector>();
		using var provider = services.BuildServiceProvider(validateScopes: true);
		using var first = provider.CreateScope();
		using var second = provider.CreateScope();
		var middleware = first.ServiceProvider.GetRequiredService<PoisonMessageMiddleware>();
		middleware.ShouldBeSameAs(second.ServiceProvider.GetRequiredService<PoisonMessageMiddleware>());
		var observer = first.ServiceProvider.GetServices<IPoisonMessageDetector>().OfType<ObservingDetector>().Single();
		observer.ShouldBeSameAs(second.ServiceProvider.GetServices<IPoisonMessageDetector>().OfType<ObservingDetector>().Single());
		var terminal = provider.GetServices<IPoisonMessageDetector>().OfType<TerminalDetector>().Single();
		var message = new TestMessage();
		var context = new MessageContext { MessageId = "custom" };
		var fault = new TimeoutException("not default poison");
		var result = await middleware.InvokeAsync(message, context, (_, _, _) => throw fault, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		observer.Calls.ShouldBe(1);
		terminal.Calls.ShouldBe(1);
		A.CallTo(() => handler.HandlePoisonMessageAsync(
			message, context, "custom poison", CancellationToken.None, fault)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreserveHealthyPipelineResultWithoutPoisonHandling()
	{
		var handler = A.Fake<IPoisonMessageHandler>();
		var services = CreateServices(handler);
		services.AddPoisonMessageHandling();
		services.AddPoisonMessageDetector<TerminalDetector>();
		using var provider = services.BuildServiceProvider(validateScopes: true);
		var expected = MessageResult.Success();
		var result = await provider.GetRequiredService<PoisonMessageMiddleware>().InvokeAsync(
			new TestMessage(), new MessageContext(), (_, _, _) => ValueTask.FromResult(expected), CancellationToken.None);
		result.ShouldBeSameAs(expected);
		provider.GetServices<IPoisonMessageDetector>().OfType<TerminalDetector>().Single().Calls.ShouldBe(0);
		A.CallTo(() => handler.HandlePoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<string>._, A<CancellationToken>._, A<Exception?>._))
			.MustNotHaveHappened();
	}

	private static ServiceCollection CreateServices(IPoisonMessageHandler handler)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(handler);
		return services;
	}

	private sealed class TestMessage : IDispatchMessage;

	private sealed class ObservingDetector : IPoisonMessageDetector
	{
		public int Calls { get; private set; }
		public Task<PoisonDetectionResult> IsPoisonMessageAsync(IDispatchMessage message, IMessageContext context,
			MessageProcessingInfo processingInfo, Exception? exception = null)
		{
			Calls++;
			return Task.FromResult(PoisonDetectionResult.NotPoison());
		}
	}

	private sealed class TerminalDetector : IPoisonMessageDetector
	{
		public int Calls { get; private set; }
		public Task<PoisonDetectionResult> IsPoisonMessageAsync(IDispatchMessage message, IMessageContext context,
			MessageProcessingInfo processingInfo, Exception? exception = null)
		{
			Calls++;
			return Task.FromResult(PoisonDetectionResult.Poison("custom poison", nameof(TerminalDetector)));
		}
	}
}
