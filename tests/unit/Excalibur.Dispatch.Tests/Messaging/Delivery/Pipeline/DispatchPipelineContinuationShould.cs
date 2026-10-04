// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Pipeline")]
public sealed class DispatchPipelineContinuationShould
{
	[Fact]
	public async Task ForwardReplacementArgumentsThroughEveryRemainingStage()
	{
		var original = A.Fake<IDispatchMessage>();
		var replacement = A.Fake<IDispatchMessage>();
		var replacementContext = new MessageContext();
		using var cancellation = new CancellationTokenSource();
		var visits = 0;
		var pipeline = new DispatchPipeline([
			new Middleware((_, _, next, _) => next(replacement, replacementContext, cancellation.Token)),
			new Middleware((message, context, next, token) =>
			{
				message.ShouldBeSameAs(replacement);
				context.ShouldBeSameAs(replacementContext);
				token.ShouldBe(cancellation.Token);
				visits++;
				return next(message, context, token);
			})]);

		await pipeline.ExecuteAsync(original, new MessageContext(), (message, context, token) =>
		{
			message.ShouldBeSameAs(replacement);
			context.ShouldBeSameAs(replacementContext);
			token.ShouldBe(cancellation.Token);
			visits++;
			return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
		}, CancellationToken.None);
		visits.ShouldBe(2);
	}

	[Fact]
	public async Task PreserveIndependentContinuationArgumentsDuringConcurrentRetryBranches()
	{
		var first = A.Fake<IDispatchMessage>();
		var second = A.Fake<IDispatchMessage>();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var seen = new System.Collections.Concurrent.ConcurrentBag<IDispatchMessage>();
		var pipeline = new DispatchPipeline([
			new Middleware(async (_, context, next, token) =>
			{
				var firstCall = next(first, context, token).AsTask();
				await entered.Task.ConfigureAwait(false);
				await next(second, context, token).ConfigureAwait(false);
				release.SetResult();
				return await firstCall.ConfigureAwait(false);
			}),
			new Middleware(async (message, context, next, token) =>
			{
				if (ReferenceEquals(message, first))
				{
					entered.SetResult();
					await release.Task.ConfigureAwait(false);
				}
				return await next(message, context, token).ConfigureAwait(false);
			})]);

		await pipeline.ExecuteAsync(first, new MessageContext(), (message, _, _) =>
		{
			seen.Add(message);
			return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
		}, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
		seen.Count.ShouldBe(2);
		seen.ShouldContain(first);
		seen.ShouldContain(second);
	}

	[Fact]
	public async Task DeliverMiddlewareCancellationToTheTerminal()
	{
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		var pipeline = new DispatchPipeline([
			new Middleware((message, context, next, _) => next(message, context, cancellation.Token))]);
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await pipeline.ExecuteAsync(A.Fake<IDispatchMessage>(), new MessageContext(), (_, _, token) =>
			{
				token.ThrowIfCancellationRequested();
				return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
			}, CancellationToken.None));
	}

	private sealed class Middleware(
		Func<IDispatchMessage, IMessageContext, DispatchRequestDelegate, CancellationToken, ValueTask<IMessageResult>> invoke)
		: IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken) =>
			invoke(message, context, nextDelegate, cancellationToken);
	}
}
