// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Pipeline")]
public sealed class BoundContinuationShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepTheTerminalWhenMiddlewareReplacesTheContext(bool typed)
	{
		var replacement = new MessageContext();
		var chain = new ChainExecutor([new Middleware((message, _, next, token) => next(message, replacement, token))]);
		var expected = MessageResult.Success();
		var calls = 0;
		ValueTask<IMessageResult> Terminal(IDispatchMessage _, IMessageContext context, CancellationToken token)
		{
			context.ShouldBeSameAs(replacement);
			calls++;
			return ValueTask.FromResult<IMessageResult>(expected);
		}

		var message = A.Fake<IDispatchMessage>();
		var actual = typed
			? await chain.InvokeAsync<IMessageResult>(message, new MessageContext(), Terminal, CancellationToken.None)
			: await chain.InvokeAsync(message, new MessageContext(), (DispatchRequestDelegate)Terminal, CancellationToken.None);
		actual.ShouldBeSameAs(expected);
		calls.ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RetainEachInvocationTerminalAfterTheOuterMiddlewareReturns(bool typed)
	{
		var continuations = new List<DispatchRequestDelegate>();
		var chain = new ChainExecutor([new Middleware((_, _, next, _) =>
		{
			continuations.Add(next);
			return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
		})]);
		var context = new MessageContext();
		var message = A.Fake<IDispatchMessage>();
		var first = MessageResult.Success();
		var second = MessageResult.Failed("second");
		ValueTask<IMessageResult> First(IDispatchMessage msg, IMessageContext ctx, CancellationToken token) => ValueTask.FromResult<IMessageResult>(first);
		ValueTask<IMessageResult> Second(IDispatchMessage msg, IMessageContext ctx, CancellationToken token) => ValueTask.FromResult<IMessageResult>(second);
		if (typed)
		{
			await chain.InvokeAsync<IMessageResult>(message, context, First, CancellationToken.None);
			await chain.InvokeAsync<IMessageResult>(message, context, Second, CancellationToken.None);
		}
		else
		{
			await chain.InvokeAsync(message, context, (DispatchRequestDelegate)First, CancellationToken.None);
			await chain.InvokeAsync(message, context, (DispatchRequestDelegate)Second, CancellationToken.None);
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		(await continuations[0](message, context, CancellationToken.None)).ShouldBeSameAs(first);
		(await continuations[1](message, context, CancellationToken.None)).ShouldBeSameAs(second);
	}

	[Fact]
	public async Task IsolateOverlappingTerminalsSharingAContext()
	{
		var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstMessage = A.Fake<IDispatchMessage>();
		var secondMessage = A.Fake<IDispatchMessage>();
		var chain = new ChainExecutor([new Middleware(async (message, context, next, token) =>
		{
			var first = ReferenceEquals(message, firstMessage);
			(first ? firstEntered : secondEntered).SetResult();
			await (first ? releaseFirst : releaseSecond).Task.ConfigureAwait(false);
			return await next(message, context, token).ConfigureAwait(false);
		})]);
		var context = new MessageContext();
		var firstResult = MessageResult.Success();
		var secondResult = MessageResult.Failed("second");
		var firstCall = chain.InvokeAsync<IMessageResult>(firstMessage, context,
			(_, _, _) => ValueTask.FromResult<IMessageResult>(firstResult), CancellationToken.None).AsTask();
		await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		var secondCall = chain.InvokeAsync<IMessageResult>(secondMessage, context,
			(_, _, _) => ValueTask.FromResult<IMessageResult>(secondResult), CancellationToken.None).AsTask();
		await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
		releaseFirst.SetResult();
		var actualFirst = await firstCall.WaitAsync(TimeSpan.FromSeconds(10));
		releaseSecond.SetResult();
		var actualSecond = await secondCall.WaitAsync(TimeSpan.FromSeconds(10));
		actualFirst.ShouldBeSameAs(firstResult);
		actualSecond.ShouldBeSameAs(secondResult);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NotRetainTheFirstTerminalOrItsRequestState(bool typed)
	{
		var chain = new ChainExecutor([new Middleware(static (message, context, next, token) => next(message, context, token))]);
		var (terminal, request) = await BindTemporaryTerminal(chain, typed);
		for (var attempt = 0; attempt < 3; attempt++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}
		terminal.IsAlive.ShouldBeFalse();
		request.IsAlive.ShouldBeFalse();
		GC.KeepAlive(chain);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static async Task<(WeakReference Terminal, WeakReference Request)> BindTemporaryTerminal(ChainExecutor chain, bool typed)
	{
		var request = new object();
		ValueTask<IMessageResult> Terminal(IDispatchMessage message, IMessageContext context, CancellationToken token)
		{
			GC.KeepAlive(request);
			return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
		}
		Delegate terminal;
		IMessageResult result;
		if (typed)
		{
			Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>> callback = Terminal;
			terminal = callback;
			result = await chain.InvokeAsync<IMessageResult>(A.Fake<IDispatchMessage>(), new MessageContext(), callback, CancellationToken.None);
		}
		else
		{
			DispatchRequestDelegate callback = Terminal;
			terminal = callback;
			result = await chain.InvokeAsync(A.Fake<IDispatchMessage>(), new MessageContext(), callback, CancellationToken.None);
		}
		result.Succeeded.ShouldBeTrue();
		return (new WeakReference(terminal), new WeakReference(request));
	}

	private sealed class Middleware(
		Func<IDispatchMessage, IMessageContext, DispatchRequestDelegate, CancellationToken, ValueTask<IMessageResult>> invoke)
		: IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken) => invoke(message, context, nextDelegate, cancellationToken);
	}
}
