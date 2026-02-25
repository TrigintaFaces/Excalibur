// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Polling;

namespace Excalibur.Dispatch.Testing.Tests.Polling;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class WaitHelpersShould
{
	#region WaitUntilAsync (sync condition)

	[Fact]
	public async Task ReturnTrueWhenConditionMetImmediately()
	{
		var result = await WaitHelpers.WaitUntilAsync(
			() => true,
			TimeSpan.FromSeconds(1));

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFalseWhenTimeout()
	{
		var result = await WaitHelpers.WaitUntilAsync(
			() => false,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromMilliseconds(20));

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnTrueWhenConditionBecomesTrue()
	{
		var counter = 0;
		var result = await WaitHelpers.WaitUntilAsync(
			() => ++counter >= 3,
			TimeSpan.FromSeconds(2),
			TimeSpan.FromMilliseconds(20));

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowOnNullSyncCondition()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			WaitHelpers.WaitUntilAsync(
				(Func<bool>)null!,
				TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public async Task ThrowOnExternalCancellation()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await Should.ThrowAsync<OperationCanceledException>(() =>
			WaitHelpers.WaitUntilAsync(
				() => false,
				TimeSpan.FromSeconds(10),
				cancellationToken: cts.Token));
	}

	#endregion

	#region WaitUntilAsync (async condition without CancellationToken)

	[Fact]
	public async Task ReturnTrueWhenAsyncConditionMetImmediately()
	{
		var result = await WaitHelpers.WaitUntilAsync(
			() => Task.FromResult(true),
			TimeSpan.FromSeconds(1));

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFalseWhenAsyncConditionTimesOut()
	{
		var result = await WaitHelpers.WaitUntilAsync(
			() => Task.FromResult(false),
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromMilliseconds(20));

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowOnNullAsyncCondition()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			WaitHelpers.WaitUntilAsync(
				(Func<Task<bool>>)null!,
				TimeSpan.FromSeconds(1)));
	}

	#endregion

	#region WaitUntilAsync (async condition with CancellationToken)

	[Fact]
	public async Task ReturnTrueWhenAsyncCancellableConditionMetImmediately()
	{
		var result = await WaitHelpers.WaitUntilAsync(
			_ => Task.FromResult(true),
			TimeSpan.FromSeconds(1));

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFalseWhenAsyncCancellableConditionTimesOut()
	{
		var result = await WaitHelpers.WaitUntilAsync(
			_ => Task.FromResult(false),
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromMilliseconds(20));

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowOnNullAsyncCancellableCondition()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			WaitHelpers.WaitUntilAsync(
				(Func<CancellationToken, Task<bool>>)null!,
				TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public async Task PassLinkedTokenToAsyncCancellableCondition()
	{
		var tokenPassed = false;
		var result = await WaitHelpers.WaitUntilAsync(
			ct =>
			{
				tokenPassed = ct.CanBeCanceled;
				return Task.FromResult(true);
			},
			TimeSpan.FromSeconds(1));

		result.ShouldBeTrue();
		tokenPassed.ShouldBeTrue();
	}

	#endregion

	#region WaitForValueAsync (sync producer)

	[Fact]
	public async Task ReturnValueWhenProducedImmediately()
	{
		Func<string?> producer = () => "found";
		var result = await WaitHelpers.WaitForValueAsync(
			producer,
			TimeSpan.FromSeconds(1));

		result.ShouldBe("found");
	}

	[Fact]
	public async Task ReturnNullWhenProducerTimesOut()
	{
		Func<string?> producer = () => null;
		var result = await WaitHelpers.WaitForValueAsync(
			producer,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromMilliseconds(20));

		result.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnValueWhenProducedAfterDelay()
	{
		var counter = 0;
		Func<string?> producer = () => ++counter >= 3 ? "eventually" : null;
		var result = await WaitHelpers.WaitForValueAsync(
			producer,
			TimeSpan.FromSeconds(2),
			TimeSpan.FromMilliseconds(20));

		result.ShouldBe("eventually");
	}

	[Fact]
	public async Task ThrowOnNullSyncProducer()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			WaitHelpers.WaitForValueAsync(
				(Func<string?>)null!,
				TimeSpan.FromSeconds(1)));
	}

	#endregion

	#region WaitForValueAsync (async producer)

	[Fact]
	public async Task ReturnValueFromAsyncProducer()
	{
		var result = await WaitHelpers.WaitForValueAsync(
			() => Task.FromResult<string?>("async-found"),
			TimeSpan.FromSeconds(1));

		result.ShouldBe("async-found");
	}

	[Fact]
	public async Task ReturnNullWhenAsyncProducerTimesOut()
	{
		var result = await WaitHelpers.WaitForValueAsync(
			() => Task.FromResult<string?>(null),
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromMilliseconds(20));

		result.ShouldBeNull();
	}

	[Fact]
	public async Task ThrowOnNullAsyncProducer()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			WaitHelpers.WaitForValueAsync(
				(Func<Task<string?>>)null!,
				TimeSpan.FromSeconds(1)));
	}

	#endregion

	#region RetryUntilSuccessAsync

	[Fact]
	public async Task ReturnTrueWhenActionSucceedsImmediately()
	{
		var result = await WaitHelpers.RetryUntilSuccessAsync(
			() => Task.CompletedTask,
			TimeSpan.FromSeconds(1));

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnTrueWhenActionSucceedsAfterRetries()
	{
		var attempts = 0;
		var result = await WaitHelpers.RetryUntilSuccessAsync(
			() =>
			{
				if (++attempts < 3) throw new InvalidOperationException("not yet");
				return Task.CompletedTask;
			},
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(20));

		result.ShouldBeTrue();
		attempts.ShouldBe(3);
	}

	[Fact]
	public async Task PropagateExceptionWhenMaxRetriesExceeded()
	{
		// When maxRetries is reached, the last exception propagates because
		// the catch filter (attempts < maxRetries) is false on the final attempt
		await Should.ThrowAsync<InvalidOperationException>(() =>
			WaitHelpers.RetryUntilSuccessAsync(
				() => throw new InvalidOperationException("always fail"),
				TimeSpan.FromSeconds(5),
				TimeSpan.FromMilliseconds(20),
				maxRetries: 3));
	}

	[Fact]
	public async Task ReturnFalseWhenTimeoutReachedDuringDelay()
	{
		// Timeout fires during the Task.Delay between retries, which returns false
		var attempts = 0;
		var result = await WaitHelpers.RetryUntilSuccessAsync(
			() =>
			{
				attempts++;
				throw new InvalidOperationException("fail");
			},
			TimeSpan.FromMilliseconds(50),
			TimeSpan.FromMilliseconds(200)); // retryDelay longer than timeout

		result.ShouldBeFalse();
		attempts.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ThrowOnNullAction()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			WaitHelpers.RetryUntilSuccessAsync(null!, TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public async Task ThrowOnExternalCancellationDuringRetry()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await Should.ThrowAsync<OperationCanceledException>(() =>
			WaitHelpers.RetryUntilSuccessAsync(
				() => throw new InvalidOperationException("fail"),
				TimeSpan.FromSeconds(10),
				cancellationToken: cts.Token));
	}

	#endregion

	#region DefaultPollInterval

	[Fact]
	public void HaveDefaultPollIntervalOf100Ms()
	{
		WaitHelpers.DefaultPollInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion
}
