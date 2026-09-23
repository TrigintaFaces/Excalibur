// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// A typed dispatch converts the pipeline's non-generic result into <see cref="IMessageResult{T}"/>.
/// That conversion must carry <see cref="IMessageResult.Disposition"/> across unchanged.
/// </summary>
/// <remarks>
/// <para>
/// The conversion used to read a boolean "was this a cache hit" member instead, and a boolean has only
/// two values for a three-valued question. A result produced without a handler for a reason other than
/// caching — a duplicate the inbox suppressed — read as <c>false</c> there, exactly like a genuinely
/// handled result, so the conversion rebuilt it as <see cref="MessageDisposition.Handled"/>. The
/// suppression was erased silently: the caller saw a success whose handler had, as far as the result
/// could say, run.
/// </para>
/// <para>
/// Safety is the suppressed-duplicate arm. Liveness is the pair beside it: a cache hit must still say
/// cache hit and a handled message must still say handled, so a conversion that simply returned one
/// constant could not pass.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class TypedDispatchPreservesResultDispositionShould
{
	private sealed class ProbeQuery : IDispatchAction<string>;

	private static Dispatcher CreateSut(IMessageResult pipelineResult)
	{
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		_ = A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(pipelineResult));

		var finalHandler = new FinalDispatchHandler(
			A.Fake<IMessageBusProvider>(),
			NullLogger<FinalDispatchHandler>.Instance,
			retryPolicy: null,
			new Dictionary<string, MessageBusOptions>(StringComparer.Ordinal));

		return new Dispatcher(invoker, finalHandler);
	}

	private static Task<IMessageResult<string>> DispatchAsync(IMessageResult pipelineResult) =>
		CreateSut(pipelineResult).DispatchAsync<ProbeQuery, string>(
			new ProbeQuery(),
			new MessageContext(),
			TestContext.Current.CancellationToken);

	[Fact]
	public async Task KeepASuppressedDuplicateDistinguishableFromAHandledMessage()
	{
		// SAFETY. This is the arm a boolean could not express. Both results succeed and neither is a
		// cache hit, so every question other than the disposition gives the same answer for both.
		var suppressed = await DispatchAsync(
			MessageResult.Success(routingDecision: null, validationResult: null, authorizationResult: null,
				disposition: MessageDisposition.SuppressedAsDuplicate)).ConfigureAwait(false);

		var handled = await DispatchAsync(MessageResult.Success()).ConfigureAwait(false);

		suppressed.Succeeded.ShouldBeTrue();
		handled.Succeeded.ShouldBeTrue();
		suppressed.Succeeded.ShouldBe(
			handled.Succeeded,
			"Succeeded cannot tell these apart, which is why the disposition has to survive the conversion");

		suppressed.Disposition.ShouldBe(
			MessageDisposition.SuppressedAsDuplicate,
			"a duplicate the pipeline suppressed ran no handler; a caller that records completion on this "
			+ "result would mark a message processed that nothing processed");
		suppressed.Disposition.ShouldNotBe(handled.Disposition);
	}

	[Fact]
	public async Task StillReportACacheHitAsServedFromCache()
	{
		// LIVENESS. Without this the safety arm is satisfied by a conversion that reports
		// SuppressedAsDuplicate for everything.
		var result = await DispatchAsync(MessageResult.SuccessFromCache()).ConfigureAwait(false);

		result.Succeeded.ShouldBeTrue();
		result.Disposition.ShouldBe(MessageDisposition.ServedFromCache);
	}

	[Fact]
	public async Task StillReportAHandledMessageAsHandled()
	{
		// LIVENESS. The default has to stay the default: nothing above may push an ordinary result off
		// Handled.
		var result = await DispatchAsync(MessageResult.Success()).ConfigureAwait(false);

		result.Succeeded.ShouldBeTrue();
		result.Disposition.ShouldBe(MessageDisposition.Handled);
	}
}
