// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.CloudEvents;

/// <summary>
/// Binds the outbound CloudEvents door end to end: the public call a consumer makes, and the bytes the
/// transport is handed as a result.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms exist because the seam was unreachable and every name-based check said otherwise.</b>
/// The encoder read a marker no supported call ever wrote, and the marker's key shared a symbol name with
/// the inbound side while keying a different collection on a different type — so greps, symbol searches
/// and reference counts all reported the send path as wired. Only executing the public call and reading
/// the resulting body can distinguish "wired" from "wired to nothing", which is what these arms do.
/// </para>
/// <para>
/// The pass-through arm is the load-bearing one. The encoding decorator wraps a transport's sender
/// unconditionally, so a message nobody asked to encode must reach the wire byte-identical; an
/// implementation that rewrote every message would satisfy the encode arm and silently corrupt all
/// ordinary traffic.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventTransportMessageExtensionsShould
{
	private static CloudEvent SampleEvent() =>
		new()
		{
			Id = "evt-1",
			Type = "order.placed",
			Source = new Uri("https://example.test/orders"),
			Data = "the payload",
		};

	[Fact]
	public void Attach_the_event_where_the_encoder_reads_it()
	{
		var message = TransportMessage.FromString("original");

		var returned = message.WithCloudEvent(SampleEvent());

		returned.ShouldBeSameAs(message, "the call marks the caller's own message rather than copying it");
		message.HasProperties.ShouldBeTrue();
	}

	[Fact]
	public async Task Publish_a_marked_message_as_a_structured_CloudEvent()
	{
		var inner = new RecordingSender();
		var sender = inner.WithCloudEventEncoding();

		await sender.SendAsync(
			TransportMessage.FromString("original").WithCloudEvent(SampleEvent()),
			CancellationToken.None);

		var sent = inner.Sent.ShouldHaveSingleItem();
		sent.ContentType.ShouldBe("application/cloudevents+json");

		using var envelope = JsonDocument.Parse(sent.Body);
		envelope.RootElement.GetProperty("id").GetString().ShouldBe("evt-1");
		envelope.RootElement.GetProperty("type").GetString().ShouldBe("order.placed");
		envelope.RootElement.GetProperty("specversion").GetString().ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Leave_a_message_nobody_marked_byte_identical()
	{
		var inner = new RecordingSender();
		var sender = inner.WithCloudEventEncoding();
		var message = TransportMessage.FromString("original");
		message.ContentType = "text/plain";

		await sender.SendAsync(message, CancellationToken.None);

		var sent = inner.Sent.ShouldHaveSingleItem();
		sent.ContentType.ShouldBe("text/plain", "an unmarked message must not acquire a CloudEvents content type");
		Encoding.UTF8.GetString(sent.Body.Span).ShouldBe("original");
	}

	[Fact]
	public async Task Consume_the_marker_so_it_never_reaches_provider_metadata()
	{
		var inner = new RecordingSender();
		var sender = inner.WithCloudEventEncoding();

		await sender.SendAsync(
			TransportMessage.FromString("original").WithCloudEvent(SampleEvent()),
			CancellationToken.None);

		// The marker is an instruction to the decorator, not payload. Left in place it would be handed to
		// every provider's metadata mapping as a non-string object, where a broker may reject it outright.
		var sent = inner.Sent.ShouldHaveSingleItem();
		sent.Properties.Values.ShouldNotContain(v => v is CloudEvent);
	}

	private sealed class RecordingSender : ITransportSender
	{
		public List<TransportMessage> Sent { get; } = [];

		public string Destination => "recording";

		public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
		{
			Sent.Add(message);
			return Task.FromResult(new SendResult { IsSuccess = true, MessageId = message.Id });
		}

		public Task<BatchSendResult> SendBatchAsync(
			IReadOnlyList<TransportMessage> messages,
			CancellationToken cancellationToken)
		{
			Sent.AddRange(messages);
			return Task.FromResult(new BatchSendResult());
		}

		public Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
