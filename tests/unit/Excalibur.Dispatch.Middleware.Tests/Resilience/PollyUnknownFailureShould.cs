// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
[Trait("Pattern", "Regression")]
public sealed class PollyUnknownFailureShould
{
	private sealed class UnclassifiedFailureException(string message) : Exception(message);

	[Fact]
	public async Task PropagateUnknownOnceWithOriginalIdentity()
	{
		var fault = new UnclassifiedFailureException("unrecognized business fault");
		var calls = 0;
		var caught = await Should.ThrowAsync<Exception>(() => Create().ExecuteAsync<int>(_ =>
		{
			calls++;
			return Task.FromException<int>(fault);
		}, CancellationToken.None));
		caught.ShouldBeSameAs(fault);
		calls.ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RecoverKnownTransientAndPreserveUncertainReplay(bool uncertain)
	{
		Exception fault = uncertain ? new DispatchDeliveryUncertainException("acceptance unknown") : new TimeoutException();
		var calls = 0;
		var result = await Create().ExecuteAsync<int>(_ => ++calls == 1
			? Task.FromException<int>(fault) : Task.FromResult(42), CancellationToken.None);
		result.ShouldBe(42);
		calls.ShouldBe(2);
	}

	[Fact]
	public async Task HonorExplicitUnknownRetryPredicate()
	{
		var calls = 0;
		var result = await Create(static _ => true).ExecuteAsync<int>(_ => ++calls == 1
			? Task.FromException<int>(new UnclassifiedFailureException("reviewed retryable business fault")) : Task.FromResult(42), CancellationToken.None);
		result.ShouldBe(42);
		calls.ShouldBe(2);
	}

	[Fact]
	public async Task StopOnCallerCancellationWithoutAnotherAttempt()
	{
		using var caller = new CancellationTokenSource();
		var calls = 0;
		var caught = await Should.ThrowAsync<OperationCanceledException>(() => Create().ExecuteAsync<int>(_ =>
		{
			calls++;
			caller.Cancel();
			return Task.FromCanceled<int>(caller.Token);
		}, caller.Token));
		caught.CancellationToken.ShouldBe(caller.Token);
		calls.ShouldBe(1);
	}

	private static PollyRetryPolicyAdapter Create(Func<Exception, bool>? predicate = null) => new(
		global::Microsoft.Extensions.Options.Options.Create(new PollyRetryOptions
		{
			MaxRetryAttempts = 2,
			BaseDelay = TimeSpan.Zero,
			UseJitter = false,
			ShouldRetry = predicate,
		}));
}
