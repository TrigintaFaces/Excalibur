// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.ServiceBus;

/// <summary>
/// Unit tests for <see cref="ServiceBusTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). The GetService arms
/// here pin the "hand out the native SDK seam" contract -- production already implements it correctly;
/// without this lock a regression to the base default would silently decline the
/// <see cref="IServiceBusSenderSeam"/> capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class ServiceBusTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "orders-queue";
	private readonly IServiceBusSenderSeam _fakeSender;
	private readonly ServiceBusTransportSender _sut;

	public ServiceBusTransportSenderShould()
	{
		_fakeSender = A.Fake<IServiceBusSenderSeam>();
		_sut = new ServiceBusTransportSender(
			_fakeSender,
			TestDestination,
			NullLogger<ServiceBusTransportSender>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		await _fakeSender.DisposeAsync();
	}

	[Fact]
	public void Expose_destination_from_constructor()
	{
		_sut.Destination.ShouldBe(TestDestination);
	}

	[Fact]
	public void Throw_when_sender_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSender((IServiceBusSenderSeam)null!, TestDestination, NullLogger<ServiceBusTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSender(A.Fake<IServiceBusSenderSeam>(), null!, NullLogger<ServiceBusTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSender(A.Fake<IServiceBusSenderSeam>(), TestDestination, null!));
	}

	[Fact]
	public void Return_sender_seam_via_GetService()
	{
		var result = _sut.GetService(typeof(IServiceBusSenderSeam));
		result.ShouldBe(_fakeSender);
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = _sut.GetService(typeof(string));
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_when_GetService_type_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	#region Scheduled delivery is honoured on every send path

	// Scheduled delivery used to be applied only on the single-send path, so the same message was held
	// until its due time when sent alone or as batch overflow, and delivered immediately when it
	// happened to fit inside a batch. The delivery a caller got depended on how many other messages
	// were in flight beside it.

	[Fact]
	public async Task Apply_the_scheduled_time_to_a_message_that_fits_the_batch()
	{
		// Arrange
		var dueAt = DateTimeOffset.UtcNow.AddHours(2);
		var captured = CaptureBatch(overflowIndices: []);

		// Act
		var result = await _sut.SendBatchAsync([Scheduled("msg-1", dueAt)], CancellationToken.None);

		// Assert
		result.IsCompleteSuccess.ShouldBeTrue();
		captured.Value.ShouldNotBeNull();
		captured.Value!.Count.ShouldBe(1);
		captured.Value[0].ScheduledEnqueueTime.ShouldBe(dueAt);
	}

	[Fact]
	public async Task Apply_the_scheduled_time_to_every_scheduled_entry_of_a_mixed_batch()
	{
		// Arrange
		var dueAt = DateTimeOffset.UtcNow.AddHours(2);
		var captured = CaptureBatch(overflowIndices: []);

		// Act
		var result = await _sut.SendBatchAsync(
			[Scheduled("scheduled-1", dueAt), Immediate("immediate-1"), Scheduled("scheduled-2", dueAt)],
			CancellationToken.None);

		// Assert
		result.IsCompleteSuccess.ShouldBeTrue();
		captured.Value.ShouldNotBeNull();
		captured.Value![0].ScheduledEnqueueTime.ShouldBe(dueAt);
		captured.Value[2].ScheduledEnqueueTime.ShouldBe(dueAt);

		// Liveness: an ordinary message in the same batch stays immediately deliverable.
		captured.Value[1].ScheduledEnqueueTime.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public async Task Schedule_a_message_sent_on_its_own()
	{
		// Arrange
		var dueAt = DateTimeOffset.UtcNow.AddHours(2);
		A.CallTo(() => _fakeSender.ScheduleMessageAsync(
				A<ServiceBusMessage>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(Task.FromResult(42L));

		// Act
		var result = await _sut.SendAsync(Scheduled("msg-1", dueAt), CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.SequenceNumber.ShouldBe(42L);
		A.CallTo(() => _fakeSender.ScheduleMessageAsync(
				A<ServiceBusMessage>.That.Matches(m => m.ScheduledEnqueueTime == dueAt),
				dueAt,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Schedule_a_batch_entry_that_overflows_onto_the_individual_path()
	{
		// Arrange
		var dueAt = DateTimeOffset.UtcNow.AddHours(2);
		_ = CaptureBatch(overflowIndices: [0]);
		A.CallTo(() => _fakeSender.ScheduleMessageAsync(
				A<ServiceBusMessage>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(Task.FromResult(7L));

		// Act
		var result = await _sut.SendBatchAsync(
			[Scheduled("overflowed", dueAt), Immediate("fitted")],
			CancellationToken.None);

		// Assert
		result.IsCompleteSuccess.ShouldBeTrue();
		result.Results[0].SequenceNumber.ShouldBe(7L);
		A.CallTo(() => _fakeSender.ScheduleMessageAsync(A<ServiceBusMessage>._, dueAt, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Leave_an_unscheduled_message_immediately_deliverable()
	{
		// Liveness: the scheduled-time mapping must not stamp a time onto messages that never asked for
		// one, which would hold every ordinary message.
		var captured = CaptureBatch(overflowIndices: []);

		// Act
		var result = await _sut.SendBatchAsync([Immediate("msg-1"), Immediate("msg-2")], CancellationToken.None);

		// Assert
		result.IsCompleteSuccess.ShouldBeTrue();
		captured.Value.ShouldNotBeNull();
		captured.Value!.ShouldAllBe(m => m.ScheduledEnqueueTime == default(DateTimeOffset));
	}

	[Fact]
	public async Task Ignore_a_scheduled_time_that_is_not_a_parseable_timestamp()
	{
		// Arrange
		var message = Immediate("msg-1");
		message.Properties[TransportTelemetryConstants.PropertyKeys.ScheduledTime] = "not-a-timestamp";
		var captured = CaptureBatch(overflowIndices: []);

		// Act
		var result = await _sut.SendBatchAsync([message], CancellationToken.None);

		// Assert
		result.IsCompleteSuccess.ShouldBeTrue();
		captured.Value![0].ScheduledEnqueueTime.ShouldBe(default(DateTimeOffset));
	}

	#endregion

	#region Batch results stay correlated to their inputs

	// Results used to be appended in two passes -- batch successes first, then overflow outcomes -- and
	// a failed overflow result carried no identity at all, so a caller could not tell which inputs to
	// retry once the positions had shifted.

	[Fact]
	public async Task Correlate_results_by_input_position_when_an_earlier_entry_overflows()
	{
		// Arrange: the FIRST input overflows and fails on the individual path, while the second fits.
		_ = CaptureBatch(overflowIndices: [0]);
		A.CallTo(() => _fakeSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("individual send rejected"));

		// Act
		var result = await _sut.SendBatchAsync([Immediate("A"), Immediate("B")], CancellationToken.None);

		// Assert
		result.TotalMessages.ShouldBe(2);
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
		result.Results.Count.ShouldBe(2);

		result.Results[0].IsSuccess.ShouldBeFalse();
		result.Results[0].MessageId.ShouldBe("A");
		result.Results[0].Error.ShouldNotBeNull();

		result.Results[1].IsSuccess.ShouldBeTrue();
		result.Results[1].MessageId.ShouldBe("B");
	}

	[Fact]
	public async Task Identify_every_failed_input_when_several_overflow_entries_fail()
	{
		// Arrange
		_ = CaptureBatch(overflowIndices: [0, 2]);
		A.CallTo(() => _fakeSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("individual send rejected"));

		// Act
		var result = await _sut.SendBatchAsync(
			[Immediate("A"), Immediate("B"), Immediate("C"), Immediate("D")],
			CancellationToken.None);

		// Assert: a caller selecting retry candidates gets exactly the inputs that failed.
		var retryCandidates = result.Results.Where(static r => !r.IsSuccess).Select(static r => r.MessageId).ToList();
		retryCandidates.ShouldBe(["A", "C"]);
		result.Results.Select(static r => r.MessageId).ShouldBe(["A", "B", "C", "D"]);
		result.SuccessCount.ShouldBe(2);
		result.FailureCount.ShouldBe(2);
	}

	[Fact]
	public async Task Correlate_results_when_every_entry_overflows()
	{
		// Arrange
		_ = CaptureBatch(overflowIndices: [0, 1, 2]);
		A.CallTo(() => _fakeSender.SendMessageAsync(A<ServiceBusMessage>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _sut.SendBatchAsync(
			[Immediate("A"), Immediate("B"), Immediate("C")],
			CancellationToken.None);

		// Assert
		result.IsCompleteSuccess.ShouldBeTrue();
		result.Results.Select(static r => r.MessageId).ShouldBe(["A", "B", "C"]);
	}

	[Fact]
	public async Task Correlate_results_when_the_whole_batch_send_throws()
	{
		// Arrange
		A.CallTo(() => _fakeSender.SendBatchAsync(A<IReadOnlyList<ServiceBusMessage>>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("batch rejected"));

		// Act
		var result = await _sut.SendBatchAsync([Immediate("A"), Immediate("B")], CancellationToken.None);

		// Assert
		result.FailureCount.ShouldBe(2);
		result.Results.Select(static r => r.MessageId).ShouldBe(["A", "B"]);
		result.Results.ShouldAllBe(r => !r.IsSuccess && r.Error != null);
	}

	[Fact]
	public async Task Correlate_results_for_a_batch_in_which_nothing_overflows()
	{
		// Liveness: the happy path keeps one identifiable result per input, in input order.
		_ = CaptureBatch(overflowIndices: []);

		// Act
		var result = await _sut.SendBatchAsync(
			[Immediate("A"), Immediate("B"), Immediate("C")],
			CancellationToken.None);

		// Assert
		result.IsCompleteSuccess.ShouldBeTrue();
		result.Results.Count.ShouldBe(3);
		result.Results.Select(static r => r.MessageId).ShouldBe(["A", "B", "C"]);
	}

	#endregion

	#region Helpers

	private static TransportMessage Immediate(string id) =>
		new() { Id = id, Body = "payload"u8.ToArray(), ContentType = "application/json" };

	private static TransportMessage Scheduled(string id, DateTimeOffset dueAt)
	{
		var message = Immediate(id);
		message.Properties[TransportTelemetryConstants.PropertyKeys.ScheduledTime] =
			dueAt.ToString("O", CultureInfo.InvariantCulture);
		return message;
	}

	private Captured CaptureBatch(IReadOnlyList<int> overflowIndices)
	{
		var captured = new Captured();
		A.CallTo(() => _fakeSender.SendBatchAsync(A<IReadOnlyList<ServiceBusMessage>>._, A<CancellationToken>._))
			.Invokes((IReadOnlyList<ServiceBusMessage> batch, CancellationToken _) => captured.Value = [.. batch])
			.Returns(Task.FromResult(overflowIndices));
		return captured;
	}

	private sealed class Captured
	{
		public IReadOnlyList<ServiceBusMessage>? Value { get; set; }
	}

	#endregion
}
