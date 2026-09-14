// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Outbox;

/// <summary>
/// Unit tests for <see cref="OutboxWriterExtensions.WriteScheduledAsync"/>.
/// Validates scheduled delivery scoping and argument validation.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class OutboxWriterExtensionsShould : UnitTestBase
{
	[Fact]
	public async Task DelegateToTheSchedulingWriterWhenItSupportsScheduling()
	{
		// Renamed from DelegateToWriteAsyncOnWriter, which asserted the defect: that a scheduled write on a
		// writer without scheduling support fell through to a plain immediate WriteAsync. Delegation to
		// WriteAsync is the wrong outcome, so a test named for it could only ever pass while the bug stood.
		var writer = new AwaitsBeforeRecordingWriter();
		var message = A.Fake<IDispatchMessage>();
		var scheduledAt = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

		await writer.WriteScheduledAsync(message, "dest", scheduledAt, CancellationToken.None);

		writer.Recorded.ShouldBe(scheduledAt);
	}

	[Fact]
	public async Task ThrowWhenWriterIsNull()
	{
		// Arrange
		IOutboxWriter? writer = null;
		var message = A.Fake<IDispatchMessage>();
		var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => writer!.WriteScheduledAsync(message, "dest", scheduledAt, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowWhenMessageIsNull()
	{
		// Arrange
		var writer = A.Fake<IOutboxWriter>();
		var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => writer.WriteScheduledAsync(null!, "dest", scheduledAt, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task RefuseAWriterThatCannotSchedule_RatherThanWritingItImmediately()
	{
		// This arm used to assert the opposite: that a writer without scheduling support silently received
		// a plain WriteAsync. That is a delivery at the WRONG TIME, which is the single thing this overload
		// exists to control, and it happened with no error and nothing logged. The message still arrives,
		// so nothing downstream looks broken -- a scheduled reminder or a delayed compensation just fires
		// at once. RED against any return to the fallback.
		var writer = A.Fake<IOutboxWriter>();
		var message = A.Fake<IDispatchMessage>();
		var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);

		var thrown = await Should.ThrowAsync<NotSupportedException>(
			async () => await writer.WriteScheduledAsync(message, null, scheduledAt, CancellationToken.None));

		thrown.Message.ShouldContain("cannot schedule");
		A.CallTo(() => writer.WriteAsync(A<IDispatchMessage>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task AcceptNullDestination()
	{
		var writer = new AwaitsBeforeRecordingWriter();
		var message = A.Fake<IDispatchMessage>();
		var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);

		await writer.WriteScheduledAsync(message, null, scheduledAt, CancellationToken.None);

		writer.Recorded.ShouldBe(scheduledAt);
	}

	[Fact]
	public async Task PassCancellationTokenThrough()
	{
		// Arrange
		var writer = new AwaitsBeforeRecordingWriter();
		var message = A.Fake<IDispatchMessage>();
		var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(30);
		using var cts = new CancellationTokenSource();

		// Act
		await writer.WriteScheduledAsync(message, "dest", scheduledAt, cts.Token);

		// Assert
		writer.Recorded.ShouldBe(scheduledAt);
	}

	[Fact]
	public async Task GiveTheScheduledTimeToAWriterThatOnlyRecordsItAfterAnAwait()
	{
		// The scheduled time used to travel to the writer through an AsyncLocal that the extension cleared
		// in a finally block -- which ran before an asynchronous writer had reached the line that reads it.
		// It is a parameter now, so it cannot be cleared out from under a writer that suspends first. This
		// arm is RED against any return to a set-then-clear ambient.
		var writer = new AwaitsBeforeRecordingWriter();
		var message = A.Fake<IDispatchMessage>();
		var scheduledAt = new DateTimeOffset(2026, 8, 3, 9, 30, 0, TimeSpan.Zero);

		await writer.WriteScheduledAsync(message, "dest", scheduledAt, CancellationToken.None);

		writer.Recorded.ShouldBe(
			scheduledAt,
			"the writer must see the caller's scheduled time for the whole write, including after it suspends");
	}

	/// <summary>
	/// A writer that yields before recording, so anything the extension tears down synchronously is already
	/// gone by the time the value is read.
	/// </summary>
	private sealed class AwaitsBeforeRecordingWriter : IOutboxWriter, IScheduledOutboxWriter
	{
		public DateTimeOffset? Recorded { get; private set; }

		public ValueTask WriteAsync(IDispatchMessage message, string? destination, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public async ValueTask WriteScheduledAsync(
			IDispatchMessage message,
			string? destination,
			DateTimeOffset scheduledAt,
			CancellationToken cancellationToken)
		{
			await Task.Yield();
			Recorded = scheduledAt;
		}
	}
}
