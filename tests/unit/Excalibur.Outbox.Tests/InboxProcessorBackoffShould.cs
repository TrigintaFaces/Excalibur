// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Author≠impl regression lock for S850 Lane B · <c>aed1gl</c> (inbox retry backoff is honored, and the
/// re-admission floor is the backoff base delay rather than a fixed 5-minute window).
/// </summary>
/// <remarks>
/// <para>
/// Authored by FrontendDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>) against the frozen GUIDE seam (msg 15508 + SA Option-C, msg 15555),
/// pinned by TestsDeveloper (15586). Two behaviors are locked, driving the private methods by reflection
/// (the same toolkit as <c>InboxProcessorShould</c>):
/// </para>
/// <list type="number">
/// <item><b>Backoff honored</b> — <c>MarkFailedForRetryAsync</c> routes a failed-for-retry entry through
/// <see cref="IBackoffSchedulableInboxStore.MarkFailedWithBackoffAsync"/> with the current attempt and
/// <c>nextAttemptAt ≈ now + CalculateDelay(attempt)</c> when the store supports it.</item>
/// <item><b>Fail-open</b> — a store that does NOT implement the capability falls back to the plain
/// <see cref="IInboxStore.MarkFailedAsync(string, string, string, System.Threading.CancellationToken)"/>.</item>
/// <item><b>Not-inert floor</b> — <c>ReserveBatchRecordsAsync</c> passes <c>now − CalculateDelay(1)</c> as the
/// re-admission floor, NOT the pre-fix hardcoded <c>now − 5min</c>.</item>
/// </list>
/// <para>
/// <b>RED on the pre-fix surface:</b> (1)/(2) — <c>MarkFailedForRetryAsync</c> did not exist (the pre-fix
/// path called <c>MarkFailedAsync</c> directly, ignoring backoff); (3) — the floor was the literal
/// <c>DateTimeOffset.UtcNow.AddMinutes(-5)</c>, so the captured value lands ~5 minutes back instead of one
/// base-delay back (a behavioral value mismatch). A fake <see cref="IBackoffCalculator"/> returns
/// deterministic delays so the assertions are exact.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class InboxProcessorBackoffShould
{
	[Fact]
	public async Task RecordComputedBackoffNextAttempt_WhenStoreIsBackoffSchedulable()
	{
		var store = A.Fake<IInboxStore>(o => o.Implements<IBackoffSchedulableInboxStore>());
		var backoff = A.Fake<IBackoffCalculator>();
		_ = A.CallTo(() => backoff.CalculateDelay(A<int>._)).ReturnsLazily((int attempt) => TimeSpan.FromSeconds(attempt));
		await using var processor = CreateProcessor(store, backoff);

		var markFailedForRetry = GetPrivateMethod("MarkFailedForRetryAsync");

		var before = DateTimeOffset.UtcNow;
		await (Task)markFailedForRetry.Invoke(processor, ["msg-1", "TestHandler", 3, CancellationToken.None])!;
		var after = DateTimeOffset.UtcNow;

		// Backoff store receives the attempt + an absolute next-attempt in [before+3s, after+3s].
		_ = A.CallTo(() => ((IBackoffSchedulableInboxStore)store).MarkFailedWithBackoffAsync(
				"msg-1",
				"TestHandler",
				A<string>._,
				3,
				A<DateTimeOffset>.That.Matches(t =>
					t >= before + TimeSpan.FromSeconds(3) && t <= after + TimeSpan.FromSeconds(3)),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// The plain (backoff-blind) mark-failed path must NOT be used for a backoff-capable store.
		A.CallTo(() => store.MarkFailedAsync("msg-1", "TestHandler", A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task FallBackToPlainMarkFailed_WhenStoreIsNotBackoffSchedulable()
	{
		var store = A.Fake<IInboxStore>(); // does NOT implement IBackoffSchedulableInboxStore
		var backoff = A.Fake<IBackoffCalculator>();
		_ = A.CallTo(() => backoff.CalculateDelay(A<int>._)).Returns(TimeSpan.FromSeconds(1));
		await using var processor = CreateProcessor(store, backoff);

		var markFailedForRetry = GetPrivateMethod("MarkFailedForRetryAsync");

		await (Task)markFailedForRetry.Invoke(processor, ["msg-2", "H", 2, CancellationToken.None])!;

		_ = A.CallTo(() => store.MarkFailedAsync("msg-2", "H", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseBackoffBaseDelayAsTheReadmissionFloor_NotAFixedFiveMinutes()
	{
		DateTimeOffset? capturedOlderThan = null;
		var store = A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>());
		_ = A.CallTo(() => ((IInboxStoreAdmin)store).GetFailedEntriesAsync(
				A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.Invokes(call => capturedOlderThan = call.GetArgument<DateTimeOffset?>(1))
			.ReturnsLazily(() => new ValueTask<IEnumerable<InboxEntry>>(Array.Empty<InboxEntry>()));

		var backoff = A.Fake<IBackoffCalculator>();
		_ = A.CallTo(() => backoff.CalculateDelay(1)).Returns(TimeSpan.FromSeconds(7));
		await using var processor = CreateProcessor(store, backoff);

		var reserveBatch = GetPrivateMethod("ReserveBatchRecordsAsync");

		var before = DateTimeOffset.UtcNow;
		await (Task)reserveBatch.Invoke(processor, [10, CancellationToken.None])!;
		var after = DateTimeOffset.UtcNow;

		// Floor == now − CalculateDelay(1) == now − 7s. Pre-fix used now − 5min, which is far outside this band.
		_ = capturedOlderThan.ShouldNotBeNull();
		capturedOlderThan!.Value.ShouldBeInRange(
			before - TimeSpan.FromSeconds(7),
			after - TimeSpan.FromSeconds(7));
	}

	private static MethodInfo GetPrivateMethod(string name)
	{
		var method = typeof(InboxProcessor).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
		return method.ShouldNotBeNull(
			$"aed1gl: InboxProcessor.{name} not found — the backoff-honoring seam (MarkFailedForRetryAsync / " +
			"backoff-base re-admission floor) is the fix; its absence is the pre-fix RED.");
	}

	private static InboxProcessor CreateProcessor(IInboxStore store, IBackoffCalculator backoff) =>
		new(
			Options.Create(new DeliveryInboxOptions
			{
				Capacity =
				{
					QueueCapacity = 500,
					ProducerBatchSize = 100,
					ConsumerBatchSize = 50,
					PerRunTotal = 1000,
					ParallelProcessingDegree = 4,
				},
				MaxAttempts = 5,
			}),
			store,
			A.Fake<IServiceProvider>(),
			new DispatchJsonSerializer(),
			NullLogger<InboxProcessor>.Instance,
			backoffCalculator: backoff);
}
