// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Concurrency")]
public sealed class RetryOwnershipScopeShould
{
	[Fact]
	public async Task ShareConcurrentPublicationAndRetryTransientDestinationOnlyOnNextAttempt()
	{
		using var owner = RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext(), static exception => exception is IOException);
		var attempt = owner.BeginAttempt();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var calls = 0;
		var first = owner.PublishDestinationAsync(attempt, "A", async () =>
		{
			Interlocked.Increment(ref calls);
			entered.SetResult();
			await release.Task.ConfigureAwait(false);
			throw new IOException("before effect");
		}, CancellationToken.None);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		var second = Task.Run(async () =>
		{
			var publication = owner.PublishDestinationAsync(attempt, "a", () =>
			{
				Interlocked.Increment(ref calls);
				return Task.CompletedTask;
			}, CancellationToken.None);
			joined.SetResult();
			return await publication.ConfigureAwait(false);
		});
		await joined.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		calls.ShouldBe(1);
		release.SetResult();
		var outcomes = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		outcomes[0].ShouldBeSameAs(outcomes[1]);
		calls.ShouldBe(1);
		var nextAttempt = owner.BeginAttempt();
		var next = await owner.PublishDestinationAsync(nextAttempt, "A", () =>
		{
			Interlocked.Increment(ref calls);
			return Task.CompletedTask;
		}, CancellationToken.None).ConfigureAwait(false);
		next.Fault.ShouldBeNull();
		calls.ShouldBe(2);
	}

	[Fact]
	public async Task AllowCapturedChildAfterNativePublicationHasCompleted()
	{
		var message = A.Fake<IDispatchEvent>();
		var context = new MessageContext();
		using var owner = RetryOwnershipScope.Enter(message, context, static exception => exception is IOException);
		var firstAttempt = owner.BeginAttempt();
		RetryOwnershipScope.AttemptState? secondAttempt = null;
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Task<RetryOwnershipScope.DestinationOutcome>? child = null;
		var first = await owner.PublishDestinationAsync(firstAttempt, "A", () =>
		{
			child = Task.Run(async () =>
			{
				await release.Task.ConfigureAwait(false);
				return await owner.PublishDestinationAsync(secondAttempt!, "A", static () => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);
			});
			return Task.FromException(new IOException("before effect"));
		}, CancellationToken.None).ConfigureAwait(false);
		first.Retryable.ShouldBeTrue();
		secondAttempt = owner.BeginAttempt();
		release.SetResult();
		(await child!.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false)).Fault.ShouldBeNull();
	}

	[Fact]
	public async Task RefuseGenuineSameDestinationRecursionWithoutDeadlock()
	{
		var message = A.Fake<IDispatchEvent>();
		var context = new MessageContext();
		using var owner = RetryOwnershipScope.Enter(message, context);
		var nestedCalls = 0;
		var attempt = owner.BeginAttempt();
		var outcome = await owner.PublishDestinationAsync(attempt, "A", async () =>
		{
			await owner.PublishDestinationAsync(attempt, "A", () => { nestedCalls++; return Task.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
		}, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		outcome.Fault.ShouldNotBeNull().SourceException.ShouldBeOfType<InvalidOperationException>();
		nestedCalls.ShouldBe(0);
	}

	[Fact]
	public async Task KeepLatePriorAttemptReportOutOfCurrentDecision()
	{
		using var owner = RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext());
		var firstAttempt = owner.BeginAttempt();
		var firstResult = MessageResult.Failed(new MessageProblemDetails { Status = 500 });
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var lateReporter = Task.Run(async () =>
		{
			await release.Task.ConfigureAwait(false);
			RetryOwnershipScope.ReportFanout(firstAttempt, firstResult, true);
		});
		var currentAttempt = owner.BeginAttempt();
		var currentResult = MessageResult.Failed(new MessageProblemDetails { Status = 500 });
		RetryOwnershipScope.ReportFanout(currentAttempt, currentResult, false);
		release.SetResult();
		await lateReporter.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		RetryOwnershipScope.AllowsRetryAfterResult(currentAttempt, currentResult).ShouldBeFalse();
	}

	[Fact]
	public void MatchBothIdentitiesAndRestoreNestedOwner()
	{
		var message = A.Fake<IDispatchAction>();
		var nestedMessage = A.Fake<IDispatchAction>();
		var context = new MessageContext();
		var nestedContext = new MessageContext();
		using (RetryOwnershipScope.Enter(message, context))
		{
			RetryOwnershipScope.Owns(message, context).ShouldBeTrue();
			RetryOwnershipScope.Owns(nestedMessage, context).ShouldBeFalse();
			RetryOwnershipScope.Owns(message, nestedContext).ShouldBeFalse();
			using (RetryOwnershipScope.Enter(nestedMessage, nestedContext))
			{
				RetryOwnershipScope.Owns(message, context).ShouldBeTrue();
				RetryOwnershipScope.Owns(nestedMessage, nestedContext).ShouldBeTrue();
			}
			RetryOwnershipScope.Owns(message, context).ShouldBeTrue();
			RetryOwnershipScope.Owns(nestedMessage, nestedContext).ShouldBeFalse();
		}
		RetryOwnershipScope.Owns(message, context).ShouldBeFalse();
	}

	[Fact]
	public async Task InvalidateOwnerCapturedByChildFlowAfterScopeEnds()
	{
		var message = A.Fake<IDispatchAction>();
		var context = new MessageContext();
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Task<bool> captured;
		using (RetryOwnershipScope.Enter(message, context))
		{
			captured = Task.Run(async () =>
			{
				await release.Task.ConfigureAwait(false);
				return RetryOwnershipScope.Owns(message, context);
			});
		}
		release.SetResult();
		(await captured.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false)).ShouldBeFalse();
	}

	[Fact]
	public async Task KeepOverlappingOwnersOfSameObjectsIsolated()
	{
		var message = A.Fake<IDispatchAction>();
		var context = new MessageContext();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var first = Task.Run(async () =>
		{
			using (RetryOwnershipScope.Enter(message, context))
			{
				await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
			}
			firstEnded.SetResult();
			RetryOwnershipScope.Owns(message, context).ShouldBeFalse();
		});
		var second = Task.Run(async () =>
		{
			using (RetryOwnershipScope.Enter(message, context))
			{
				entered.SetResult();
				await firstEnded.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
				RetryOwnershipScope.Owns(message, context).ShouldBeTrue();
			}
			RetryOwnershipScope.Owns(message, context).ShouldBeFalse();
		});
		await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		RetryOwnershipScope.Owns(message, context).ShouldBeFalse();
	}
}
