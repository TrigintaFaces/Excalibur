// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DispatchOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests.Core;

/// <summary>
/// A message time-to-live expires a message; it does not postpone one.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> The staging converters assigned <c>ScheduledAt = outboxMessage.ExpiresAt</c>.
/// <c>ScheduledAt</c> is the NOT-BEFORE field the drain uses to decide eligibility — it is enforced by
/// every store (the in-memory store skips a message whose <c>ScheduledAt</c> is in the future, the
/// relational reservations carry <c>scheduled_at IS NULL OR scheduled_at &lt;= now</c>, and
/// <c>OutboundMessage.IsReadyForDelivery()</c> returns false while it is in the future). Feeding the
/// expiry into it INVERTED the feature: a consumer who set a ten-minute TTL meaning <i>drop this if it
/// has not gone out in ten minutes</i> instead got <i>do not attempt delivery for ten minutes</i>, and
/// the message was then delivered late rather than dropped.
/// </para>
/// <para>
/// <b>WHY BOTH ARMS.</b> The safety arm alone is satisfied by a converter that never populates
/// <c>ScheduledAt</c> at all, which would silently break genuine scheduled delivery. The liveness arm
/// pins that a real schedule still withholds the message, so "stop scheduling anything" cannot pass.
/// </para>
/// <para>
/// <b>WHAT THIS DOES NOT CLAIM.</b> These arms bind the not-before semantics only. Expiry is still not
/// ENFORCED anywhere on the drain — no path reads <c>ExpiresAt</c> to dead-letter an expired message —
/// so a TTL currently expires nothing. That is a separate gap and is deliberately not asserted here;
/// asserting it would require a behaviour that does not yet exist.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxTimeToLiveDoesNotDelayDeliveryShould : IDisposable
{
	private readonly IOutboxStore _outboxStore = A.Fake<IOutboxStore>();
	private readonly IOutboxProcessor _outboxProcessor = A.Fake<IOutboxProcessor>();
	private readonly DispatchJsonSerializer _serializer = new();
	private readonly ILogger<MessageOutbox> _logger = A.Fake<ILogger<MessageOutbox>>();
	private MessageOutbox? _sut;

	public void Dispose() => _sut?.Dispose();

	/// <summary>
	/// SAFETY — the arm the defect is about. A time-to-live must not become a not-before time.
	/// </summary>
	[Fact]
	public async Task NotPostponeDeliveryOfAMessageThatMerelyCarriesATimeToLive()
	{
		var staged = await StageAsync(expiresAt: DateTimeOffset.UtcNow.AddMinutes(10), scheduledAt: null);

		staged.ScheduledAt.ShouldBeNull(
			"the expiry is not a schedule: routing it into ScheduledAt withholds the message for exactly "
			+ "the window the consumer meant it to be discarded after");

		staged.IsReadyForDelivery().ShouldBeTrue(
			"a message with a TTL and no schedule is eligible immediately; every store gates on "
			+ "ScheduledAt, so a non-null value here is a delivery delay and not a time limit");
	}

	/// <summary>
	/// LIVENESS. Without this, the arm above is satisfied by a converter that stopped scheduling at all.
	/// </summary>
	[Fact]
	public async Task StillWithholdAMessageThatCarriesAGenuineSchedule()
	{
		var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);

		var staged = await StageAsync(expiresAt: null, scheduledAt: scheduledAt);

		staged.ScheduledAt.ShouldBe(scheduledAt,
			"a schedule the caller actually asked for must survive staging");

		staged.IsReadyForDelivery().ShouldBeFalse(
			"this is a policy, not a removal: scheduled delivery must still withhold, or the safety arm "
			+ "above is satisfied by a converter that never schedules anything");
	}

	/// <summary>
	/// The two fields are independent: carrying both must schedule by the schedule, never by the expiry.
	/// </summary>
	[Fact]
	public async Task ScheduleByTheScheduleWhenAMessageCarriesBoth()
	{
		var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(5);
		var expiresAt = DateTimeOffset.UtcNow.AddMinutes(90);

		var staged = await StageAsync(expiresAt: expiresAt, scheduledAt: scheduledAt);

		staged.ScheduledAt.ShouldBe(scheduledAt,
			"with both set, reading the expiry would postpone the message by 90 minutes instead of 5 — "
			+ "the failure is largest exactly when a consumer uses both features together");
	}

	private async Task<OutboundMessage> StageAsync(DateTimeOffset? expiresAt, DateTimeOffset? scheduledAt)
	{
		var options = Options.Create(DispatchOutboxOptions.Balanced());
		options.Value.DefaultMessageTimeToLive = null;

		_sut = new MessageOutbox(_outboxStore, _outboxProcessor, _serializer, options, _logger);

		var message = A.Fake<IOutboxMessage>();
		A.CallTo(() => message.MessageId).Returns("msg-1");
		A.CallTo(() => message.MessageType).Returns("Test.Message");
		A.CallTo(() => message.MessageMetadata).Returns(string.Empty);
		A.CallTo(() => message.MessageBody).Returns([1, 2, 3]);
		A.CallTo(() => message.CreatedAt).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => message.ExpiresAt).Returns(expiresAt);
		A.CallTo(() => message.ScheduledAt).Returns(scheduledAt);

		OutboundMessage? captured = null;
		A.CallTo(() => _outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => captured = m);

		_ = await _sut.SaveMessagesAsync([message], CancellationToken.None);

		return captured.ShouldNotBeNull("the message must reach the store, or these arms measure nothing");
	}
}
