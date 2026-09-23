// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.SQS;
using Amazon.SQS.Model;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Runs the CloudEvents transport conformance suite against the SQS transport.
/// </summary>
/// <remarks>
/// <para>
/// Everything asserted lives in the kit. This class supplies only the three things that are genuinely
/// SQS-specific: the receiver a consumer of this transport resolves, a way to put a message in front of
/// it, and this transport's own encoding. That split is the point — a transport cannot drift into a
/// weaker version of the contract, because it does not own the contract.
/// </para>
/// <para>
/// <b>No infrastructure.</b> The vendor client is faked and the registration is otherwise exactly what a
/// consumer gets: the transport registers its client with <c>TryAddKeyedSingleton</c>, so a fake
/// registered first wins and every other part of the wiring is untouched.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSqsCloudEventConformanceTests : CloudEventTransportConformanceTests
{
	private const string TransportName = "sqs-conformance";

	private readonly List<Message> _inbound = [];

	/// <inheritdoc />
	protected override Task<ITransportReceiver> CreateReceiverAsync()
	{
		var client = A.Fake<IAmazonSQS>();

		_ = A.CallTo(() => client.ReceiveMessageAsync(
				A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.ReturnsLazily(() => new ReceiveMessageResponse { Messages = [.. _inbound] });

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddKeyedSingleton(TransportName, client);
		_ = services.AddAwsSqsTransport(TransportName, _ => { });

		var provider = services.BuildServiceProvider();

		return Task.FromResult(provider.GetRequiredKeyedService<ITransportReceiver>(TransportName));
	}

	/// <inheritdoc />
	protected override Task SeedMessagesAsync(
		ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
	{
		_inbound.Clear();

		foreach (var message in messages)
		{
			_inbound.Add(new Message
			{
				MessageId = message.Id,
				ReceiptHandle = "receipt-" + message.Id,
				Body = System.Text.Encoding.UTF8.GetString(message.Body.Span),
				MessageAttributes = message.Properties.ToDictionary(
					static pair => pair.Key,
					static pair => new MessageAttributeValue
					{
						DataType = "String",
						StringValue = pair.Value?.ToString() ?? string.Empty,
					},
					StringComparer.Ordinal),
			});
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<TransportReceivedMessage> EncodeAsync(
		CloudEvent cloudEvent, CloudEventMode mode)
	{
		var adapter = new AwsSqsCloudEventAdapter(
			Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
			{
				DefaultSource = new Uri("https://conformance.excalibur.test"),
			}),
			NullLogger<AwsSqsCloudEventAdapter>.Instance);

		var request = await adapter.ToTransportMessageAsync(cloudEvent, mode, CancellationToken.None);

		return new TransportReceivedMessage
		{
			Id = "sqs-wire-1",
			Body = System.Text.Encoding.UTF8.GetBytes(request.MessageBody ?? string.Empty),
			EnqueuedAt = DateTimeOffset.UtcNow,
			Properties = request.MessageAttributes.ToDictionary(
				static pair => pair.Key,
				static pair => (object)(pair.Value.StringValue ?? string.Empty),
				StringComparer.Ordinal),
		};
	}

	/// <summary>
	/// LIVENESS. Binary-mode round trip through SQS's own send and receive paths.
	/// </summary>
	[Fact]
	public Task Round_trip_a_binary_mode_event() =>
		VerifyBinaryModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// LIVENESS. Structured-mode round trip, which is a different wire shape and a different decode path.
	/// </summary>
	[Fact]
	public Task Round_trip_a_structured_mode_event() =>
		VerifyStructuredModeRoundTripsThroughItsOwnReceiver();

	/// <summary>
	/// SAFETY. Ordinary SQS traffic is not reported as a CloudEvent.
	/// </summary>
	[Fact]
	public Task Leave_ordinary_traffic_alone() =>
		VerifyOrdinaryTrafficIsNotReportedAsCloudEvent();

	/// <summary>
	/// SAFETY. A mode this transport declines is not decoded anyway.
	/// </summary>
	[Fact]
	public Task Decline_no_mode_it_cannot_decode() =>
		VerifyDeclinedModesDoNotDecode();
}
