// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Time;

namespace Excalibur.Dispatch.Tests.Messaging.Time;

/// <summary>
///     Tests for the <see cref="TimeProviderUtilities" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TimeProviderUtilitiesShould
{
	[Fact]
	public void GetUtcNowAsDateTime()
	{
		var before = DateTimeOffset.UtcNow.DateTime;
		var result = TimeProvider.System.GetUtcNowAsDateTime();
		var after = DateTimeOffset.UtcNow.DateTime;

		// DateTimeOffset.DateTime returns Unspecified kind (per .NET spec)
		(result - before).TotalSeconds.ShouldBeGreaterThanOrEqualTo(-1);
		(after - result).TotalSeconds.ShouldBeGreaterThanOrEqualTo(-1);
	}

	[Fact]
	public void ThrowForNullTimeProviderOnGetUtcNowAsDateTime()
	{
		Should.Throw<ArgumentNullException>(() =>
			TimeProviderUtilities.GetUtcNowAsDateTime(null!));
	}

	[Fact]
	public void ReturnCancellationTokenNoneForInfiniteDelay()
	{
		var token = TimeProvider.System.CreateCancellationToken(Timeout.InfiniteTimeSpan);

		token.ShouldBe(CancellationToken.None);
	}

	[Fact]
	public void ThrowForNullTimeProviderOnCreateCancellationToken()
	{
		Should.Throw<ArgumentNullException>(() =>
			TimeProviderUtilities.CreateCancellationToken(null!, TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public async Task CompleteImmediatelyForZeroDelay()
	{
		var task = TimeProvider.System.DelayAsync(TimeSpan.Zero, CancellationToken.None);

		task.IsCompleted.ShouldBeTrue();
		await task.ConfigureAwait(false);
	}

	[Fact]
	public async Task CompleteImmediatelyForNegativeDelay()
	{
		var task = TimeProvider.System.DelayAsync(TimeSpan.FromMilliseconds(-1), CancellationToken.None);

		task.IsCompleted.ShouldBeTrue();
		await task.ConfigureAwait(false);
	}

	[Fact]
	public void ThrowForNullTimeProviderOnDelayAsync()
	{
		Should.Throw<ArgumentNullException>(() =>
			TimeProviderUtilities.DelayAsync(null!, TimeSpan.FromSeconds(1), CancellationToken.None));
	}

	[Fact]
	public async Task CompleteDelayAfterSpecifiedDuration()
	{
		var task = TimeProvider.System.DelayAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

		await task.ConfigureAwait(false);
	}

	[Fact]
	public async Task CancelDelayWhenTokenCancelled()
	{
		using var cts = new CancellationTokenSource();
		var task = TimeProvider.System.DelayAsync(TimeSpan.FromSeconds(30), cts.Token);

		await cts.CancelAsync().ConfigureAwait(false);

		await Should.ThrowAsync<TaskCanceledException>(async () =>
			await task.ConfigureAwait(false)).ConfigureAwait(false);
	}
}
