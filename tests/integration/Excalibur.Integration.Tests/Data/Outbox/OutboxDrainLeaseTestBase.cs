// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure locks on the property that <b>every</b> drain path holds a lease before a message
/// reaches a transport, and that the retry path honours the backoff floor the failure path wrote.
/// </summary>
/// <remarks>
/// <para>
/// The atomic claim is only half the guarantee. A drain also exposes a scheduled pass and a retry pass, and
/// each of those once selected its rows with a plain read: the scheduled pass filtered on an unset dispatcher
/// column, which is a filter and not a claim because the value it tested was never conditionally written, and
/// the retry pass selected on a recorded error with no floor term at all. Both published rows the dispatcher
/// never leased.
/// </para>
/// <para>
/// These arms assert the property at the <b>transport</b>, which is where a duplicate becomes irreversible:
/// by the time two dispatchers argue over the row, the transport already holds two copies. Both providers
/// exercised here have a sound claim, so a lock scoped to the claim passes on both while the drain remains
/// broken — which is precisely why these arms drive the publisher rather than the store.
/// </para>
/// </remarks>
public abstract class OutboxDrainLeaseTestBase
{
	/// <summary>Number of due-scheduled messages staged for the concurrency arm.</summary>
	private const int DueMessageCount = 6;

	/// <summary>The backoff floor used by the retry arms. Long enough to observe, short enough to wait out.</summary>
	protected const int RetryFloorSeconds = 4;

	/// <summary>Builds a store over the live container, optionally with an explicit failure backoff floor.</summary>
	/// <param name="failureBackoffFloorSeconds">The floor to configure, or <see langword="null"/> for the provider default.</param>
	/// <returns>A store bound to the fixture's database.</returns>
	protected abstract Task<IOutboxStore> CreateStoreAsync(int? failureBackoffFloorSeconds);

	/// <summary>Empties the outbox table between arms.</summary>
	/// <returns>A task that completes when the table is empty.</returns>
	protected abstract Task CleanupAsync();

	/// <summary>
	/// SAFETY. Two drains running the scheduled pass at the same time must hand each due message to the
	/// transport exactly once.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// LIVENESS is asserted in the same arm, deliberately: a drain that returns nothing to anybody satisfies
	/// disjointness perfectly, so the arm also requires that every staged message was in fact delivered. The
	/// transport stalls briefly on each publish, which holds the window between one drain's read and its
	/// mark-sent open long enough for the other drain's read to land inside it. A claim closes that window by
	/// construction; a plain read does not, and both drains then publish the same rows.
	/// </remarks>
	[Fact]
	public async Task HandEachDueScheduledMessageToTheTransportExactlyOnce_WhenTwoDrainsRunConcurrently()
	{
		var ct = TestContext.Current.CancellationToken;
		await CleanupAsync().ConfigureAwait(false);

		var store = await CreateStoreAsync(null).ConfigureAwait(false);
		var staged = new List<string>(DueMessageCount);

		for (var i = 0; i < DueMessageCount; i++)
		{
			var message = new OutboundMessage("DueScheduledMessage", [(byte)i], "orders")
			{
				// Due: its time has already arrived, so both the claim and the former plain read admit it.
				ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			};

			await store.StageMessageAsync(message, ct).ConfigureAwait(false);
			staged.Add(message.Id);
		}

		var delivered = new ConcurrentBag<string>();
		var storeA = await CreateStoreAsync(null).ConfigureAwait(false);
		var storeB = await CreateStoreAsync(null).ConfigureAwait(false);
		var publisherA = CreatePublisher(storeA, delivered, TimeSpan.FromMilliseconds(120));
		var publisherB = CreatePublisher(storeB, delivered, TimeSpan.FromMilliseconds(120));

		var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var drainA = Task.Run(
			async () =>
			{
				await start.Task.ConfigureAwait(false);
				_ = await publisherA.PublishScheduledMessagesAsync(ct).ConfigureAwait(false);
			},
			ct);
		var drainB = Task.Run(
			async () =>
			{
				await start.Task.ConfigureAwait(false);
				_ = await publisherB.PublishScheduledMessagesAsync(ct).ConfigureAwait(false);
			},
			ct);

		start.SetResult();
		await Task.WhenAll(drainA, drainB).ConfigureAwait(false);

		// SAFETY -- no message reached the transport twice.
		var duplicates = delivered
			.GroupBy(id => id, StringComparer.Ordinal)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		duplicates.ShouldBeEmpty(
			"Two concurrent scheduled drains handed the same message to the transport more than once. The " +
			"scheduled pass must claim, not read: filtering on an unset dispatcher column without " +
			"conditionally writing it is check-then-act, and the mark that follows only arbitrates the record " +
			"-- the transport already holds both copies.");

		// LIVENESS -- work was actually handed out. Disjointness alone is satisfied by delivering nothing.
		delivered.Order(StringComparer.Ordinal).ShouldBe(
			staged.Order(StringComparer.Ordinal),
			"Every due scheduled message must be delivered. A drain that claims nothing is trivially disjoint " +
			"and useless.");
	}

	/// <summary>
	/// A scheduled message must not be delivered before its time arrives -- the claim decides due-ness, so
	/// routing the scheduled pass through it must not make a future message visible early.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task NeverDeliverAScheduledMessageBeforeItsTimeArrives()
	{
		var ct = TestContext.Current.CancellationToken;
		await CleanupAsync().ConfigureAwait(false);

		var store = await CreateStoreAsync(null).ConfigureAwait(false);
		var future = new OutboundMessage("FutureScheduledMessage", [9], "orders")
		{
			ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
		};

		await store.StageMessageAsync(future, ct).ConfigureAwait(false);

		var delivered = new ConcurrentBag<string>();
		var publisher = CreatePublisher(store, delivered, TimeSpan.Zero);

		_ = await publisher.PublishScheduledMessagesAsync(ct).ConfigureAwait(false);

		delivered.ShouldBeEmpty("A message scheduled an hour from now is not due and must not be dispatched.");
	}

	/// <summary>
	/// SAFETY. A message inside its backoff floor must not be re-selected by the retry pass.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The failure path writes a next-attempt floor, and the retry pass then has to consult it. Selecting on a
	/// recorded error alone republishes the message on the very next poll cycle -- the zero-backoff retry loop
	/// the floor exists to forbid, reached by routing around the floor rather than by omitting it.
	/// </remarks>
	[Fact]
	public async Task NeverRepublishAFailedMessageInsideItsBackoffFloor()
	{
		var ct = TestContext.Current.CancellationToken;
		await CleanupAsync().ConfigureAwait(false);

		var store = await CreateStoreAsync(RetryFloorSeconds).ConfigureAwait(false);
		var messageId = await StageAndFailAsync(store, ct).ConfigureAwait(false);

		var delivered = new ConcurrentBag<string>();
		var publisher = CreatePublisher(store, delivered, TimeSpan.Zero);

		// Immediately inside the floor, and again -- a poll cycle is far shorter than the floor.
		_ = await publisher.RetryFailedMessagesAsync(3, ct).ConfigureAwait(false);
		_ = await publisher.RetryFailedMessagesAsync(3, ct).ConfigureAwait(false);

		delivered.ShouldBeEmpty(
			$"Message '{messageId}' was floored to now plus {RetryFloorSeconds}s by the failure path and was " +
			"republished anyway. The retry pass must consult the floor it wrote; a select keyed only on a " +
			"recorded error re-delivers on every poll cycle.");
	}

	/// <summary>
	/// LIVENESS. The same message must be redelivered once its backoff floor has elapsed -- a floor that never
	/// expires is a message lost, which the safety arm above would not notice.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task RepublishAFailedMessage_OnceItsBackoffFloorHasElapsed()
	{
		var ct = TestContext.Current.CancellationToken;
		await CleanupAsync().ConfigureAwait(false);

		var store = await CreateStoreAsync(RetryFloorSeconds).ConfigureAwait(false);
		var messageId = await StageAndFailAsync(store, ct).ConfigureAwait(false);

		var delivered = new ConcurrentBag<string>();
		var publisher = CreatePublisher(store, delivered, TimeSpan.Zero);

		var deadline = DateTimeOffset.UtcNow.AddSeconds((RetryFloorSeconds * 4) + 20);
		while (delivered.IsEmpty && DateTimeOffset.UtcNow < deadline)
		{
			_ = await publisher.RetryFailedMessagesAsync(3, ct).ConfigureAwait(false);

			if (delivered.IsEmpty)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
			}
		}

		delivered.ShouldBe(
			[messageId],
			$"A failed message must become deliverable again once its {RetryFloorSeconds}s floor elapses. It " +
			"never did, so the retry path drops the message instead of deferring it.");
	}

	/// <summary>Stages one message, claims it, and reports it failed -- leaving it floored and lease-free.</summary>
	private static async Task<string> StageAndFailAsync(IOutboxStore store, CancellationToken cancellationToken)
	{
		var message = new OutboundMessage("FailedMessage", [7], "orders");
		await store.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);

		// Claim it as this store's dispatcher, so the ownership guard on the failure path admits the report.
		var claimed = await store.GetUnsentMessagesAsync(10, cancellationToken).ConfigureAwait(false);
		claimed.Select(m => m.Id).ShouldContain(message.Id, "the staged message must be claimable before it can fail");

		await store.MarkFailedAsync(message.Id, "transport unavailable", 1, cancellationToken).ConfigureAwait(false);

		return message.Id;
	}

	/// <summary>
	/// Builds a publisher whose transport records the id of every message handed to it, and optionally stalls
	/// so the window between a drain's read and its mark-sent stays open long enough to be observed.
	/// </summary>
	private static MessageBusOutboxPublisher CreatePublisher(
		IOutboxStore store,
		ConcurrentBag<string> delivered,
		TimeSpan publishDelay)
	{
		var bus = A.Fake<IMessageBusAdapter>();

		_ = A.CallTo(() => bus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(async (IDispatchMessage _, IMessageContext context, CancellationToken token) =>
			{
				delivered.Add(context.MessageId);

				if (publishDelay > TimeSpan.Zero)
				{
					await Task.Delay(publishDelay, token).ConfigureAwait(false);
				}

				return A.Fake<IMessageResult>();
			});

		return new MessageBusOutboxPublisher(
			store,
			A.Fake<IPayloadSerializer>(),
			bus,
			A.Fake<IServiceProvider>(),
			NullLogger<MessageBusOutboxPublisher>.Instance);
	}
}
