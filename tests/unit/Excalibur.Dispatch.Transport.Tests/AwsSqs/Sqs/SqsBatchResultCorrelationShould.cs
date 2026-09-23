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
/// A caller recovering from a partial batch failure has to know which inputs failed. These arms hold the
/// batch result to a positional contract -- Results[i] is the outcome of messages[i] -- because without
/// it the only safe recovery is to resend the whole batch, and on an at-least-once transport that
/// manufactures duplicates of messages that were already delivered.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class SqsBatchResultCorrelationShould : IAsyncDisposable
{
	private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";

	private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
	private readonly SqsTransportSender _sender;

	public SqsBatchResultCorrelationShould() =>
		_sender = new SqsTransportSender(_fakeSqs, QueueUrl, NullLogger<SqsTransportSender>.Instance);

	public async ValueTask DisposeAsync()
	{
		await _sender.DisposeAsync();
		_fakeSqs.Dispose();
	}

	/// <summary>
	/// SAFETY, and the exact reported case: the first input fails, the second succeeds, and the SDK
	/// reports the success first. Appending results in arrival order would put the success at index 0 and
	/// send the caller to retry the wrong message.
	/// </summary>
	[Fact]
	public async Task IdentifyTheFailedInputWhenTheFirstEntryFails()
	{
		RespondWith(
			successful: [Success("1", "sqs-b")],
			failed: [Failure("0", "InternalError", senderFault: false)]);

		var result = await _sender.SendBatchAsync(Messages("a", "b"), CancellationToken.None);

		result.Results.Count.ShouldBe(2);
		result.Results[0].IsSuccess.ShouldBeFalse();
		result.Results[0].Error!.Code.ShouldBe("InternalError");
		result.Results[1].IsSuccess.ShouldBeTrue();
		result.Results[1].MessageId.ShouldBe("sqs-b");

		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
		result.TotalMessages.ShouldBe(2);
	}

	/// <summary>
	/// LIVENESS. The positional contract is not satisfied by marking everything failed: an all-success
	/// batch reports every input as successful, with each input's own broker message ID against it.
	/// </summary>
	[Fact]
	public async Task ReportEveryInputSuccessfulWhenTheWholeBatchSucceeds()
	{
		RespondWith(
			successful: [Success("2", "sqs-c"), Success("0", "sqs-a"), Success("1", "sqs-b")],
			failed: []);

		var result = await _sender.SendBatchAsync(Messages("a", "b", "c"), CancellationToken.None);

		result.IsCompleteSuccess.ShouldBeTrue();
		result.Results.Select(r => r.MessageId).ShouldBe(["sqs-a", "sqs-b", "sqs-c"]);
	}

	/// <summary>
	/// SAFETY. When everything fails, every input carries its own error and none is reported delivered.
	/// </summary>
	[Fact]
	public async Task ReportEveryInputFailedWhenTheWholeBatchFails()
	{
		RespondWith(
			successful: [],
			failed:
			[
				Failure("1", "InvalidParameterValue", senderFault: true),
				Failure("0", "ServiceUnavailable", senderFault: false),
			]);

		var result = await _sender.SendBatchAsync(Messages("a", "b"), CancellationToken.None);

		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(2);
		result.Results[0].Error!.Code.ShouldBe("ServiceUnavailable");
		result.Results[0].Error!.IsRetryable.ShouldBeTrue();
		result.Results[1].Error!.Code.ShouldBe("InvalidParameterValue");
		result.Results[1].Error!.IsRetryable.ShouldBeFalse();
	}

	/// <summary>
	/// SAFETY. The correlation has to survive chunking: SQS takes ten entries per call, so a larger batch
	/// is split and each chunk's entry IDs must still resolve to the right global input index.
	/// </summary>
	[Fact]
	public async Task CorrelateResultsAcrossMultipleChunks()
	{
		// Every input whose index is divisible by 7 fails; the rest succeed.
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.ReturnsLazily((SendMessageBatchRequest req, CancellationToken _) =>
			{
				var successful = new List<SendMessageBatchResultEntry>();
				var failed = new List<BatchResultErrorEntry>();
				foreach (var entry in req.Entries)
				{
					var index = int.Parse(entry.Id, CultureInfo.InvariantCulture);
					if (index % 7 == 0)
					{
						failed.Add(Failure(entry.Id, "InternalError", senderFault: false));
					}
					else
					{
						successful.Add(Success(entry.Id, $"sqs-{index}"));
					}
				}

				// Reverse both arrays: nothing in the SQS contract promises response order.
				successful.Reverse();
				failed.Reverse();
				return Task.FromResult(new SendMessageBatchResponse { Successful = successful, Failed = failed });
			});

		var messages = new List<TransportMessage>();
		for (var i = 0; i < 23; i++)
		{
			messages.Add(new TransportMessage { Body = Encoding.UTF8.GetBytes($"m-{i}") });
		}

		var result = await _sender.SendBatchAsync(messages, CancellationToken.None);

		result.Results.Count.ShouldBe(23);
		for (var i = 0; i < 23; i++)
		{
			if (i % 7 == 0)
			{
				result.Results[i].IsSuccess.ShouldBeFalse($"input {i} was failed by the broker");
			}
			else
			{
				result.Results[i].IsSuccess.ShouldBeTrue($"input {i} was accepted by the broker");
				result.Results[i].MessageId.ShouldBe($"sqs-{i}");
			}
		}

		result.SuccessCount.ShouldBe(19);
		result.FailureCount.ShouldBe(4);
	}

	/// <summary>
	/// SAFETY. An exception after a chunk has already completed must not discard that chunk's per-input
	/// outcomes: the delivered messages stay delivered and only the unsent inputs are reported failed.
	/// </summary>
	[Fact]
	public async Task KeepCompletedChunkResultsWhenALaterChunkThrows()
	{
		var call = 0;
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.ReturnsLazily((SendMessageBatchRequest req, CancellationToken _) =>
			{
				call++;
				if (call > 1)
				{
					throw new AmazonSQSException("boom") { ErrorCode = "ServiceUnavailable" };
				}

				return Task.FromResult(new SendMessageBatchResponse
				{
					Successful = [.. req.Entries.ConvertAll(e => Success(e.Id, $"sqs-{e.Id}"))],
					Failed = [],
				});
			});

		var messages = new List<TransportMessage>();
		for (var i = 0; i < 15; i++)
		{
			messages.Add(new TransportMessage { Body = Encoding.UTF8.GetBytes($"m-{i}") });
		}

		var result = await _sender.SendBatchAsync(messages, CancellationToken.None);

		result.Results.Count.ShouldBe(15);
		result.SuccessCount.ShouldBe(10);
		result.FailureCount.ShouldBe(5);

		for (var i = 0; i < 10; i++)
		{
			result.Results[i].IsSuccess.ShouldBeTrue();
			result.Results[i].MessageId.ShouldBe($"sqs-{i}");
		}

		for (var i = 10; i < 15; i++)
		{
			result.Results[i].IsSuccess.ShouldBeFalse();
			result.Results[i].Error!.IsRetryable.ShouldBeTrue();
		}
	}

	/// <summary>
	/// SAFETY. An input the broker returned no entry for has an unknown outcome, not a known-good one. It
	/// must be reported as a retryable failure rather than silently omitted from the results, which would
	/// leave the caller believing a message it never heard about was delivered.
	/// </summary>
	[Fact]
	public async Task ReportAnInputWithNoResultEntryAsRetryablyFailed()
	{
		RespondWith(successful: [Success("0", "sqs-a")], failed: []);

		var result = await _sender.SendBatchAsync(Messages("a", "b"), CancellationToken.None);

		result.Results.Count.ShouldBe(2);
		result.Results[0].IsSuccess.ShouldBeTrue();
		result.Results[1].IsSuccess.ShouldBeFalse();
		result.Results[1].Error!.IsRetryable.ShouldBeTrue();
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
	}

	/// <summary>
	/// SAFETY. An entry ID the sender never issued must not be attributed to an input. Accepting it would
	/// mark some other message delivered on the strength of a result that is not about it.
	/// </summary>
	[Fact]
	public async Task NotAttributeAnUnknownEntryIdToAnInput()
	{
		RespondWith(
			successful: [Success("0", "sqs-a"), Success("99", "sqs-ghost"), Success("not-a-number", "sqs-junk")],
			failed: []);

		var result = await _sender.SendBatchAsync(Messages("a", "b"), CancellationToken.None);

		result.Results.Count.ShouldBe(2);
		result.Results[0].MessageId.ShouldBe("sqs-a");
		result.Results[1].IsSuccess.ShouldBeFalse();
		result.Results.ShouldAllBe(r => r.MessageId != "sqs-ghost" && r.MessageId != "sqs-junk");
	}

	/// <summary>
	/// Boundary. An empty batch reports nothing and calls nothing.
	/// </summary>
	[Fact]
	public async Task ReturnAnEmptyResultForAnEmptyBatch()
	{
		var result = await _sender.SendBatchAsync([], CancellationToken.None);

		result.TotalMessages.ShouldBe(0);
		result.Results.Count.ShouldBe(0);
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static SendMessageBatchResultEntry Success(string id, string messageId) =>
		new() { Id = id, MessageId = messageId };

	private static BatchResultErrorEntry Failure(string id, string code, bool senderFault) =>
		new() { Id = id, Code = code, Message = code, SenderFault = senderFault };

	private static List<TransportMessage> Messages(params string[] bodies) =>
		[.. bodies.Select(b => new TransportMessage { Body = Encoding.UTF8.GetBytes(b) })];

	private void RespondWith(
		List<SendMessageBatchResultEntry> successful,
		List<BatchResultErrorEntry> failed) =>
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageBatchResponse { Successful = successful, Failed = failed });
}
