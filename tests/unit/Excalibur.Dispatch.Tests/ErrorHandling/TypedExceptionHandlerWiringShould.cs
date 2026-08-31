// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.ErrorHandling;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.ErrorHandling;

/// <summary>
/// A registered <see cref="ITypedExceptionHandler{TException}"/> must actually be invoked once
/// <c>UseTypedExceptionHandling()</c> places the middleware in the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The interface's documentation told a consumer that registering a handler was enough. Nothing put
/// <see cref="TypedExceptionHandlerMiddleware"/> in the pipeline, so no handler was ever consulted.
/// </para>
/// <para>
/// Every arm asserts the observable outcome of a real dispatch, never a registration. The handler here
/// converts a fault into a SUCCESS, which no other stage can produce -- so removing the middleware from
/// the pipeline reddens the safety arm rather than passing vacuously.
/// </para>
/// <para>
/// The last arm covers the other source of faults. An exception thrown by a message handler reaches this
/// middleware exactly as one raised by a pipeline component does: the terminal dispatch handler propagates
/// it rather than converting it to a failed result, so typed handlers cover both. An earlier revision
/// swallowed the handler's exception at the terminal handler, and this arm pinned that as a limitation.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
public sealed class TypedExceptionHandlerWiringShould
{
	private const string RecoveredMarker = "recovered-by-typed-handler";

	private sealed record ProbeAction : IDispatchAction;

	private sealed class ProbeHandler : IActionHandler<ProbeAction>
	{
		public Task HandleAsync(ProbeAction action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class ThrowingHandler : IActionHandler<ProbeAction>
	{
		public Task HandleAsync(ProbeAction action, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("handler blew up");
	}

	/// <summary>
	/// Throws from the <see cref="DispatchMiddlewareStage.End"/> stage, which sorts below
	/// <see cref="DispatchMiddlewareStage.ErrorHandling"/> -- so the fault unwinds through the middleware
	/// under test, exactly as a fault from any pipeline component below it would.
	/// </summary>
	private sealed class ThrowingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			throw new InvalidOperationException("pipeline component blew up");
	}

	/// <summary>Converts the fault into a success, which no other pipeline stage can do.</summary>
	private sealed class RecoveringHandler : ITypedExceptionHandler<InvalidOperationException>
	{
		public ValueTask<ExceptionHandlerResult> HandleAsync(
			InvalidOperationException exception,
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(ExceptionHandlerResult.Handled(MessageResult.Success(RecoveredMarker)));
	}

	private static ServiceProvider Build(bool useTypedExceptionHandling, bool faultInPipeline)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
		{
			if (useTypedExceptionHandling)
			{
				_ = dispatch.UseTypedExceptionHandling();
			}

			if (faultInPipeline)
			{
				_ = dispatch.UseMiddleware<ThrowingMiddleware>();
			}
		});

		_ = faultInPipeline
			? services.AddTransient<IActionHandler<ProbeAction>, ProbeHandler>()
			: services.AddTransient<IActionHandler<ProbeAction>, ThrowingHandler>();

		_ = services.AddSingleton<ITypedExceptionHandler<InvalidOperationException>, RecoveringHandler>();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// SAFETY -- the handler the consumer registered runs and its result reaches the caller. If the
	/// middleware leaves the pipeline this arm fails: nothing else turns the fault into a success.
	/// </summary>
	[Fact]
	public async Task InvokeTheRegisteredHandlerWhenTheMiddlewareIsInThePipeline()
	{
		await using var provider = Build(useTypedExceptionHandling: true, faultInPipeline: true);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue(
			"UseTypedExceptionHandling() places the middleware in the pipeline, so the registered "
			+ "ITypedExceptionHandler<InvalidOperationException> must convert the fault into its own result");
	}

	/// <summary>
	/// LIVENESS -- registration alone must NOT be enough, which is what the corrected documentation states.
	/// Without this arm a middleware that recovered unconditionally would satisfy the arm above while the
	/// handler was never consulted.
	/// </summary>
	[Fact]
	public async Task LeaveTheFaultUnhandledWhenTheMiddlewareIsNotInThePipeline()
	{
		await using var provider = Build(useTypedExceptionHandling: false, faultInPipeline: true);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var faulted = await Should.ThrowAsync<InvalidOperationException>(
			async () => await dispatcher.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken));

		faulted.Message.ShouldBe(
			"pipeline component blew up",
			"the handler is registered but the middleware was never added, so nothing consults it");
	}

	/// <summary>
	/// SAFETY, second source -- a fault raised by the MESSAGE HANDLER reaches the middleware too, not only one
	/// raised by a pipeline component. Same recovery, same assertion, so both sources are held to one contract
	/// rather than one of them being a documented exception to it.
	/// </summary>
	[Fact]
	public async Task AlsoSeeAnExceptionThrownByTheMessageHandlerItself()
	{
		await using var provider = Build(useTypedExceptionHandling: true, faultInPipeline: false);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue(
			"the terminal dispatch handler propagates the handler's exception, so the registered "
			+ "ITypedExceptionHandler<InvalidOperationException> converts it exactly as it does a pipeline fault");
	}
}
