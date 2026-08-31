// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Regression lock: <c>UseAt&lt;T&gt;(stage)</c> must actually place the middleware at the requested stage,
/// overriding the stage that middleware declares for itself.
/// </summary>
/// <remarks>
/// Pre-fix defect: the builder applied a registration's stage override only to middleware satisfying an
/// internal <c>IConfigurableMiddleware</c> interface that nothing in src, tests or samples implemented, so
/// the override was skipped on every registration with nothing logged and nothing thrown. A consumer's
/// <c>UseAt</c> call had no effect and the middleware kept its declared stage. Non-vacuity: the two
/// middlewares below declare Cache and Validation, so ordering by the declared stages runs Early first;
/// the safety arm demands the opposite order, reachable only if the override is honoured. The liveness arm
/// pairs with it — a registration carrying no override must still run at its own declared stage, which a
/// builder that simply forced every middleware to one stage would fail.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
public sealed class PipelineBuilderStageOverrideShould
{
	[Fact]
	public async Task PlaceMiddlewareAtTheStageRequestedByUseAt()
	{
		// Arrange
		var invocations = new List<string>();
		await using var provider = BuildProvider(invocations);
		var builder = new PipelineBuilder("StageOverride", provider);

		// LateMiddleware declares Cache (400). Ask for Start (0), which precedes EarlyMiddleware's
		// declared Validation (200), so the requested stage and the declared stage disagree on order.
		_ = builder.UseAt<LateMiddleware>(DispatchMiddlewareStage.Start);
		_ = builder.Use<EarlyMiddleware>();

		var pipeline = builder.Build();

		// Act
		_ = await pipeline.ExecuteAsync(
			A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), FinalDelegate, CancellationToken.None);

		// Assert
		invocations.Count.ShouldBe(2);
		invocations[0].ShouldBe(
			"Late",
			"UseAt<T>(stage) must override the stage the middleware declares for itself; when the override " +
			"is dropped the pipeline orders by the declared stages and runs Early first.");
		invocations[1].ShouldBe("Early");
	}

	[Fact]
	public async Task KeepTheDeclaredStageWhenNoOverrideIsRequested()
	{
		// Arrange
		var invocations = new List<string>();
		await using var provider = BuildProvider(invocations);
		var builder = new PipelineBuilder("NoOverride", provider);

		_ = builder.Use<LateMiddleware>();
		_ = builder.Use<EarlyMiddleware>();

		var pipeline = builder.Build();

		// Act
		_ = await pipeline.ExecuteAsync(
			A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), FinalDelegate, CancellationToken.None);

		// Assert - liveness: without an override each middleware still runs at its own declared stage.
		invocations.Count.ShouldBe(2);
		invocations[0].ShouldBe("Early");
		invocations[1].ShouldBe("Late");
	}

	private static ValueTask<IMessageResult> FinalDelegate(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
		=> new(MessageResult.Success());

	private static ServiceProvider BuildProvider(List<string> invocations)
	{
		var services = new ServiceCollection();
		services.AddSingleton<IPipelineProfileRegistry, PipelineProfileRegistry>();
		services.AddSingleton(new LateMiddleware(invocations));
		services.AddSingleton(new EarlyMiddleware(invocations));
		return services.BuildServiceProvider();
	}

	private sealed class LateMiddleware(List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cache;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			invocations.Add("Late");
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class EarlyMiddleware(List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			invocations.Add("Early");
			return nextDelegate(message, context, cancellationToken);
		}
	}
}
