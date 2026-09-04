// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// A consumer that opts in to <c>Dispatch:ReturnCancelledResult</c> and dispatches on an already-cancelled
/// token must not have its handler executed -- on every entry point, including the bypass-eligible
/// direct-local fast path that carries the overwhelming majority of dispatches.
/// </summary>
/// <remarks>
/// The two arms differ only in the concrete type of the context, which is what selects the overload: a
/// <see cref="MessageContext"/> reaches the internal overload, any other <see cref="IMessageContext"/> stays
/// on the public one. The opt-in is a property of the context, not of the overload, so both arms must agree.
/// </remarks>
public sealed class DispatchCancellationBeforeFastPathShould
{
	private sealed record CancelProbeCommand : IDispatchAction;

	private sealed class CancelProbeCommandHandler : IActionHandler<CancelProbeCommand>
	{
		internal static int Executions;

		public Task HandleAsync(CancelProbeCommand message, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref Executions);
			return Task.CompletedTask;
		}
	}

	private static ServiceProvider Build()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The bypass-eligible direct-local container: no middleware, local bus, no router.
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.ConfigurePipeline(
				"DirectLocal",
				pipeline => pipeline.UseProfile(DefaultPipelineProfiles.Direct));
			_ = dispatch.WithOptions(options =>
			{
				options.UseLightMode = true;
				options.EnablePipelineSynthesis = false;
				options.Features.EnableMetrics = false;
				options.Features.EnableAuthorization = false;
				options.Features.ValidateMessageSchemas = false;
				options.Features.EnableVersioning = false;
				options.Features.EnableMultiTenancy = false;
				options.Features.EnableTransactions = false;
			});
		});

		_ = services.AddTransient<CancelProbeCommandHandler>();
		_ = services.AddTransient<IActionHandler<CancelProbeCommand>, CancelProbeCommandHandler>();

		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task NotExecuteTheHandlerOnTheConcreteContextOverload()
	{
		using var provider = Build();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var context = new MessageContext();
		context.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);
		var before = Volatile.Read(ref CancelProbeCommandHandler.Executions);

		var result = await dispatcher.DispatchAsync(new CancelProbeCommand(), context, cts.Token);

		Volatile.Read(ref CancelProbeCommandHandler.Executions).ShouldBe(
			before,
			"the consumer opted out of running work after cancellation, so the bypass-eligible fast path "
			+ "must return the cancelled result instead of invoking the handler");
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task NotExecuteTheHandlerOnTheInterfaceContextOverload()
	{
		using var provider = Build();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var context = new TestMessageContext { RequestServices = provider };
		context.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);
		var before = Volatile.Read(ref CancelProbeCommandHandler.Executions);

		var result = await dispatcher.DispatchAsync(new CancelProbeCommand(), context, cts.Token);

		Volatile.Read(ref CancelProbeCommandHandler.Executions).ShouldBe(
			before,
			"the opt-in is carried by the context, so the interface overload must behave identically");
		result.IsSuccess.ShouldBeFalse();
	}
}
