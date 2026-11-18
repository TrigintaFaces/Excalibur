// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text.Json;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Aot.Sample.Messages;
using Excalibur.Dispatch.Aot.Sample.Serialization;
using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Aot.Sample;

/// <summary>
/// Encodes and decodes a real CloudEvent through the reflection-free payload path, inside the native
/// executable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> CloudEvents interoperability and Native AOT are both first-class goals, and
/// nothing in this sample set exercised their intersection — so the ahead-of-time publish could report
/// zero trimming warnings while the CloudEvents assembly was never in the trimmed closure at all. A
/// zero from a path that is not reachable is not evidence about that path. This smoke puts the
/// CloudEvents encode and decode on a reachable path so the publish has something to analyse and the
/// native run has something to execute.
/// </para>
/// <para>
/// <b>It uses only the supported reflection-free surface</b>, exactly as a consumer under AOT would:
/// <see cref="CloudEventPayloadSerialization.CreateOptions"/> over the application's source-generated
/// context, then <see cref="CloudEventSerializer"/> and its formatter. It deliberately does NOT call
/// the reflective façade — those members are annotated for consumers precisely because a sample must
/// not demonstrate them under a trimmed publish.
/// </para>
/// </remarks>
internal static class CloudEventsSmoke
{
	private const string PayloadContentType = "application/json";

	public static void Run()
	{
		// The published native executable has dynamic code disabled. Running this smoke on the managed
		// host would exercise a different serializer configuration and prove nothing about AOT, so the
		// smoke refuses rather than reporting a pass it cannot support.
		Require(
			!RuntimeFeature.IsDynamicCodeSupported,
			"dynamic code must be disabled; a native result requires running the published native executable");

		var options = CloudEventPayloadSerialization.CreateOptions(AppJsonSerializerContext.Default);
		var serializer = CloudEventSerializer.CreateWithMetadata(options);

		var payload = new InventoryUpdatedEvent
		{
			Sku = "SKU-AOT-0001",
			NewQuantity = 42,
			Warehouse = "warehouse-north",
			UpdatedAt = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
		};

		VerifyPayloadPathIsReflectionFree(serializer, payload);
		VerifyEnvelopeRoundTrips(serializer, payload);
		VerifyAnUncoveredTypeFailsLoudly(serializer);

		Console.WriteLine(
			"Native CloudEvents: reflection-free payload serialization, structured-mode envelope "
			+ "round-trip, and a loud refusal for an uncovered payload type verified.");
	}

	/// <summary>
	/// LIVENESS. The resolver path actually produces the payload. Without this arm every assertion
	/// below is satisfied by a serializer that emits nothing.
	/// </summary>
	private static void VerifyPayloadPathIsReflectionFree(CloudEventSerializer serializer, InventoryUpdatedEvent payload)
	{
		var json = serializer.SerializePayload(payload);
		Require(
			json.Contains(payload.Sku, StringComparison.Ordinal),
			"the reflection-free payload serialization did not emit the payload");

		var utf8 = serializer.SerializePayloadToUtf8Bytes(payload);
		Require(utf8.Length > 0, "the UTF-8 payload serialization produced no bytes");
	}

	/// <summary>
	/// LIVENESS. A real CloudEvent survives an encode and decode through the CloudEvents formatter
	/// built over the decided options. This is the arm that puts the CloudEvents assembly on a
	/// reachable path, which is the whole point of the file.
	/// </summary>
	private static void VerifyEnvelopeRoundTrips(CloudEventSerializer serializer, InventoryUpdatedEvent payload)
	{
		var sent = new CloudEvent
		{
			Id = "aot-smoke-1",
			Type = "com.excalibur.sample.inventory.updated",
			Source = new Uri("urn:excalibur:aot-sample"),
			Time = payload.UpdatedAt,
			DataContentType = PayloadContentType,
			Data = payload,
		};

		var encoded = serializer.Formatter.EncodeStructuredModeMessage(sent, out var contentType);
		Require(encoded.Length > 0, "structured-mode encoding produced no bytes");

		var received = serializer.Formatter.DecodeStructuredModeMessage(
			encoded.ToArray(),
			contentType ?? new ContentType(CloudEventContentTypes.CloudEventsJson),
			extensionAttributes: null);

		Require(string.Equals(received.Id, sent.Id, StringComparison.Ordinal), "the decoded CloudEvent lost its id");
		Require(string.Equals(received.Type, sent.Type, StringComparison.Ordinal), "the decoded CloudEvent lost its type");
		Require(received.Source == sent.Source, "the decoded CloudEvent lost its source");

		// The formatter decodes JSON data as a JsonElement rather than the original type, so the
		// payload is compared by its text. Handled by shape rather than by cast so the assertion does
		// not depend on which representation the formatter chose.
		var dataText = received.Data switch
		{
			JsonElement element => element.GetRawText(),
			string text => text,
			_ => received.Data?.ToString() ?? string.Empty,
		};

		Require(
			dataText.Contains(payload.Sku, StringComparison.Ordinal),
			"the decoded CloudEvent did not carry the payload through the round-trip");
	}

	/// <summary>
	/// SAFETY. A payload type the resolver does not cover must FAIL, naming the type. The alternative
	/// — emitting a rendered type name as the payload — is the silent corruption this path is designed
	/// to refuse, and it would be invisible to every assertion above.
	/// </summary>
	private static void VerifyAnUncoveredTypeFailsLoudly(CloudEventSerializer serializer)
	{
		var uncovered = new UncoveredPayload("not-in-the-context");

		try
		{
			_ = serializer.SerializePayload(uncovered);
		}
		catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
		{
			Require(
				ex.ToString().Contains(nameof(UncoveredPayload), StringComparison.Ordinal),
				"the refusal did not name the uncovered payload type, so a consumer cannot act on it");

			return;
		}

		throw new InvalidOperationException(
			"a payload type absent from the resolver was serialized instead of refused; the "
			+ "reflection-free path is not actually reflection-free");
	}

	/// <summary>A type deliberately absent from the application's serialization context.</summary>
	private sealed record UncoveredPayload(string Value);

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
