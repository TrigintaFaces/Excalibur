// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy stores ValueTask)

using System.Globalization;
using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

/// <summary>
/// SQS defines the FIFO sequence number as a 128-bit decimal string, and observed values exceed
/// <see cref="long.MaxValue"/>. It is optional metadata reported after the broker has already accepted the
/// message, so reading it must never be able to turn a delivered message into a reported failure -- a
/// caller that honours our result would then retry something SQS already has.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class SqsSequenceNumberOverflowShould : IAsyncDisposable
{
	/// <summary>2^64, which exceeds <see cref="long.MaxValue"/> but is a legal 128-bit sequence number.</summary>
	private const string OverflowingSequenceNumber = "18446744073709551616";

	private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue.fifo";

	private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
	private readonly SqsTransportSender _sender;

	public SqsSequenceNumberOverflowShould() =>
		_sender = new SqsTransportSender(_fakeSqs, QueueUrl, NullLogger<SqsTransportSender>.Instance);

	public async ValueTask DisposeAsync()
	{
		await _sender.DisposeAsync();
		_fakeSqs.Dispose();
	}

	/// <summary>
	/// SAFETY, and the exact reported defect. The broker accepted the message; the result must say so.
	/// </summary>
	[Fact]
	public async Task ReportSuccessWhenTheSequenceNumberExceedsInt64()
	{
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse
			{
				MessageId = "sqs-fifo-1",
				SequenceNumber = OverflowingSequenceNumber,
			});

		var result = await _sender.SendAsync(Message("a"), CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		result.MessageId.ShouldBe("sqs-fifo-1");
		result.Error.ShouldBeNull();
		result.SequenceNumber.ShouldBeNull();
	}

	/// <summary>
	/// LIVENESS. The fix must not be "never read the sequence number": a value that fits is still
	/// reported, so the metadata a FIFO caller relies on is not thrown away wholesale.
	/// </summary>
	[Fact]
	public async Task StillReportASequenceNumberThatFits()
	{
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-fifo-2", SequenceNumber = "12345" });

		var result = await _sender.SendAsync(Message("a"), CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		result.SequenceNumber.ShouldBe(12345L);
	}

	/// <summary>
	/// Boundary. Exactly <see cref="long.MaxValue"/> fits and must be reported; one more does not.
	/// </summary>
	[Fact]
	public async Task ReportTheLargestSequenceNumberThatFitsAndDropTheNextOne()
	{
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse
			{
				MessageId = "sqs-max",
				SequenceNumber = long.MaxValue.ToString(CultureInfo.InvariantCulture),
			});

		var atMax = await _sender.SendAsync(Message("a"), CancellationToken.None);
		atMax.SequenceNumber.ShouldBe(long.MaxValue);

		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse
			{
				MessageId = "sqs-over",
				SequenceNumber = ((ulong)long.MaxValue + 1).ToString(CultureInfo.InvariantCulture),
			});

		var overMax = await _sender.SendAsync(Message("a"), CancellationToken.None);
		overMax.IsSuccess.ShouldBeTrue();
		overMax.SequenceNumber.ShouldBeNull();
	}

	/// <summary>
	/// Boundary. A standard (non-FIFO) queue returns no sequence number at all, which is not a failure.
	/// </summary>
	[Fact]
	public async Task ReportSuccessWhenThereIsNoSequenceNumber()
	{
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-std" });

		var result = await _sender.SendAsync(Message("a"), CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		result.SequenceNumber.ShouldBeNull();
	}

	/// <summary>
	/// SAFETY. In a batch the damage was wider: the conversion threw partway through the response, so the
	/// accepted entry it was reading AND every entry after it were reported as failures. All three
	/// messages here were accepted and all three must be reported accepted.
	/// </summary>
	[Fact]
	public async Task ReportEveryAcceptedBatchEntrySuccessfulWhenOneSequenceNumberOverflows()
	{
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageBatchResponse
			{
				Successful =
				[
					new SendMessageBatchResultEntry { Id = "0", MessageId = "sqs-0", SequenceNumber = OverflowingSequenceNumber },
					new SendMessageBatchResultEntry { Id = "1", MessageId = "sqs-1", SequenceNumber = "42" },
					new SendMessageBatchResultEntry { Id = "2", MessageId = "sqs-2", SequenceNumber = OverflowingSequenceNumber },
				],
				Failed = [],
			});

		var result = await _sender.SendBatchAsync(
			[Message("a"), Message("b"), Message("c")],
			CancellationToken.None);

		result.IsCompleteSuccess.ShouldBeTrue();
		result.SuccessCount.ShouldBe(3);
		result.FailureCount.ShouldBe(0);

		result.Results[0].SequenceNumber.ShouldBeNull();
		result.Results[1].SequenceNumber.ShouldBe(42L);
		result.Results[2].SequenceNumber.ShouldBeNull();
		result.Results.Select(r => r.MessageId).ShouldBe(["sqs-0", "sqs-1", "sqs-2"]);
	}

	/// <summary>
	/// SAFETY. A batch that genuinely mixes an accepted entry with a rejected one must still report each
	/// correctly when the accepted entry's sequence number overflows.
	/// </summary>
	[Fact]
	public async Task KeepMixedBatchOutcomesAccurateWhenASequenceNumberOverflows()
	{
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageBatchResponse
			{
				Successful =
				[
					new SendMessageBatchResultEntry { Id = "0", MessageId = "sqs-0", SequenceNumber = OverflowingSequenceNumber },
				],
				Failed =
				[
					new BatchResultErrorEntry { Id = "1", Code = "InternalError", Message = "boom", SenderFault = false },
				],
			});

		var result = await _sender.SendBatchAsync([Message("a"), Message("b")], CancellationToken.None);

		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
		result.Results[0].IsSuccess.ShouldBeTrue();
		result.Results[0].SequenceNumber.ShouldBeNull();
		result.Results[1].IsSuccess.ShouldBeFalse();
	}

	private static TransportMessage Message(string body) =>
		new() { Body = Encoding.UTF8.GetBytes(body) };
}
