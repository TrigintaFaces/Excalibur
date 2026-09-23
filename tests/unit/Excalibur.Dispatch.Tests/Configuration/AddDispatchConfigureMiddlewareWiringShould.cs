// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Empirical regression lock for bgu603: middleware registered directly as
/// <see cref="IDispatchMiddleware"/> (e.g. via <c>AddOrderingValidation()</c>) must actually run when
/// the pipeline is composed through <c>AddDispatch(configure)</c> -- not just through the legacy
/// <c>AddDispatch(Assembly)</c> scanning overload, which already unioned
/// <c>GetServices&lt;IDispatchMiddleware&gt;()</c> into its pipeline. Before the fix, <c>BuildPipeline</c>
/// sourced middleware from <c>_globalMiddleware</c> (the <c>Use&lt;T&gt;()</c> list) only, so a consumer
/// following the documented <c>AddOrderingValidation()</c> registration path got no exception, no log,
/// and a validation middleware that silently never ran.
/// </summary>
/// <remarks>
/// Deliberately empirical, not a registration assertion: <c>GetService&lt;IDispatchMiddleware&gt;()</c>
/// returning non-null proves the middleware is IN THE CONTAINER, not that the composed pipeline actually
/// invokes it. This dispatches a real message through a real, DI-composed <see cref="IDispatcher"/> and
/// asserts the OBSERVABLE effect: an out-of-order message is rejected (safety), and an in-order one still
/// reaches the handler (liveness) -- so a regression that silently drops the middleware again fails this
/// lock even though every registration is still present.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
public sealed class AddDispatchConfigureMiddlewareWiringShould
{
	[Fact]
	public async Task RejectOutOfOrderMessage_WhenOrderingValidationRegisteredViaDI_AndComposedThroughAddDispatchConfigure()
	{
		// Arrange: the exact documented registration shape -- AddOrderingValidation() (DI-only,
		// TryAddEnumerable<IDispatchMiddleware, T>) alongside AddDispatch(configure) (the modern,
		// non-scanning composition path bgu603 says never read that registration).
		var services = new ServiceCollection();
		_ = services.AddLogging();
		// A single shared INSTANCE, registered under both the interface (so the handler registry --
		// built by walking DI descriptors -- discovers it via ServiceDescriptor.GetImplementationInstance()
		// falling back to its runtime type) and its own concrete type (so HandlerActivator's
		// GetService(concreteType) resolves the SAME instance instead of constructing an untracked one).
		var handler = new OrderedTestCommandHandler();
		_ = services.AddSingleton<IActionHandler<OrderedTestCommand>>(handler);
		_ = services.AddSingleton(handler);
		_ = services.AddOrderingValidation();
		_ = services.AddDispatch(configure: null);

		var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// First message establishes the high-water mark for this ordering key.
		var first = new MessageContext(new OrderedTestCommand(), provider);
		first.MarkOrderingEnforced();
		first.SetOrderingSequence(1, "bgu603-key");
		_ = await dispatcher.DispatchAsync(new OrderedTestCommand(), first, CancellationToken.None);

		// Act: a second message replays the SAME sequence on the same key -- strictly out of order.
		var replay = new MessageContext(new OrderedTestCommand(), provider);
		replay.MarkOrderingEnforced();
		replay.SetOrderingSequence(1, "bgu603-key");

		// Assert (safety): if OrderingValidationMiddleware never ran, this dispatch would silently
		// succeed a second time instead of throwing -- which is exactly the pre-fix defect.
		_ = await Should.ThrowAsync<OutOfOrderMessageException>(
			async () => await dispatcher.DispatchAsync(new OrderedTestCommand(), replay, CancellationToken.None));
	}

	[Fact]
	public async Task PassInOrderMessage_WhenOrderingValidationRegisteredViaDI_AndComposedThroughAddDispatchConfigure()
	{
		// Liveness arm: the same wiring must not turn into a blanket rejection -- a strictly-increasing
		// sequence on the same key still reaches the handler.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		// A single shared INSTANCE, registered under both the interface (so the handler registry --
		// built by walking DI descriptors -- discovers it via ServiceDescriptor.GetImplementationInstance()
		// falling back to its runtime type) and its own concrete type (so HandlerActivator's
		// GetService(concreteType) resolves the SAME instance instead of constructing an untracked one).
		var handler = new OrderedTestCommandHandler();
		_ = services.AddSingleton<IActionHandler<OrderedTestCommand>>(handler);
		_ = services.AddSingleton(handler);
		_ = services.AddOrderingValidation();
		_ = services.AddDispatch(configure: null);

		var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var first = new MessageContext(new OrderedTestCommand(), provider);
		first.MarkOrderingEnforced();
		first.SetOrderingSequence(1, "bgu603-liveness-key");
		_ = await dispatcher.DispatchAsync(new OrderedTestCommand(), first, CancellationToken.None);

		var second = new MessageContext(new OrderedTestCommand(), provider);
		second.MarkOrderingEnforced();
		second.SetOrderingSequence(2, "bgu603-liveness-key");

		var result = await dispatcher.DispatchAsync(new OrderedTestCommand(), second, CancellationToken.None);

		_ = result.ShouldNotBeNull();
		handler.HandledCount.ShouldBe(2);
	}

	private sealed class OrderedTestCommand : IDispatchAction;

	private sealed class OrderedTestCommandHandler : IActionHandler<OrderedTestCommand>
	{
		public int HandledCount { get; private set; }

		public Task HandleAsync(OrderedTestCommand action, CancellationToken cancellationToken)
		{
			HandledCount++;
			return Task.CompletedTask;
		}
	}
}
