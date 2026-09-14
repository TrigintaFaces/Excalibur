// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// w7avib "second arm" -- RED-by-construction, scoped to what today's API can even express. Every mapper's
/// <c>FromTransportMessageAsync</c> returns a non-nullable <c>CloudEvent</c>: there is no way to say
/// "not a CloudEvent, pass through" with the current signature. So a message with none of the CloudEvents
/// markers falls through the mode detector to <c>Options.DefaultMode</c> (Structured) and is handed to the
/// structured-mode JSON decoder regardless. This proves that happens for real, on a real mapper -- not a
/// hypothetical. It is the evidence for the interface split (ICloudEventEncoder/ICloudEventDecoder) rather
/// than a test the fix is expected to satisfy; it should be DELETED, not turned green, once
/// <c>FromTransportMessageAsync</c> is replaced by a decode primitive that can return null.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMqCloudEventDecodeDefectShould
{
	[Fact]
	public async Task FromTransportMessageAsync_CannotDeclineAPlainBusinessMessage()
	{
		var mapper = new RabbitMqCloudEventAdapter(
			Microsoft.Extensions.Options.Options.Create(new CloudEventOptions()),
			rabbitMqOptions: null,
			NullLogger<RabbitMqCloudEventAdapter>.Instance);

		// An ordinary framework-serialized business message: no ce-* headers, no
		// application/cloudevents+json content type -- nothing that marks it as a CloudEvent.
		var plainBusinessPayload = JsonSerializer.SerializeToUtf8Bytes(new { orderId = 42, total = 19.99m });
		IBasicProperties properties = new RabbitMqBasicProperties
		{
			ContentType = "application/json",
			Headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
		};

		// The only two outcomes today: throw (destroying the message) or silently fabricate a CloudEvent
		// from data that was never one. Neither is "not a CloudEvent, pass through untouched" -- because
		// the current API has no way to say that. This assertion documents which one actually happens;
		// if it starts throwing NO exception and returning a garbage CloudEvent instead (or vice versa),
		// that is still the same defect, just a different failure shape, and this test's body -- not its
		// existence -- should be updated to match, until it is replaced by the real decoder.
		var exception = await Should.ThrowAsync<Exception>(
			() => mapper.FromTransportMessageAsync((properties, plainBusinessPayload), CancellationToken.None));

		// A JSON parse/format failure, not a clean "this isn't a CloudEvent" signal -- proving the
		// decoder cannot decline, only fail loudly or (on more CloudEvents-shaped-but-still-foreign JSON)
		// succeed wrongly.
		exception.ShouldNotBeNull();
	}
}
