// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport.Mqtt;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>
/// Regression lock for the B1 MQTT advertised-but-inert remediation (bead <c>jxx9mu</c>): the
/// <c>UseSharedSubscription</c> competing-consumer control and the <c>MaxPayloadBytes</c> guard must be
/// genuinely wired, not false-safety. CI-runnable, no broker — binds the extracted subscribe-topic-filter
/// and payload-limit seams of the real <c>MqttTransportReceiver</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Transport", "Mqtt")]
public sealed class MqttSubscribeContractShould
{
    [Fact]
    public void ApplyMqtt5SharedSubscriptionPrefix_WhenUseSharedSubscriptionEnabled()
    {
        var options = new MqttOptions
        {
            Topic = "orders",
            UseSharedSubscription = true,
            SharedSubscriptionGroup = "workers",
        };

        // RED on the pre-fix bare-topic-always: without the $share/{group}/ prefix the shared subscription
        // silently degrades to fan-out (every subscriber gets every message = N× duplicate processing).
        MqttTransportReceiver.BuildTopicFilter(options).ShouldBe("$share/workers/orders");
    }

    [Fact]
    public void UseBareTopic_WhenSharedSubscriptionDisabled()
    {
        var options = new MqttOptions { Topic = "orders", UseSharedSubscription = false };

        // Honest pub/sub boundary: no $share prefix, plain topic.
        MqttTransportReceiver.BuildTopicFilter(options).ShouldBe("orders");
    }

    [Fact]
    public void RejectPayload_ExceedingMaxPayloadBytes()
    {
        var options = new MqttOptions { MaxPayloadBytes = 100 };

        // Fail-closed: an oversized inbound payload is rejected (settled/dropped), not buffered.
        MqttTransportReceiver.ExceedsPayloadLimit(options, payloadLength: 101).ShouldBeTrue();
        MqttTransportReceiver.ExceedsPayloadLimit(options, payloadLength: 100).ShouldBeFalse();
    }

    [Fact]
    public void NeverRejectPayload_WhenNoLimitConfigured()
    {
        var options = new MqttOptions { MaxPayloadBytes = null };

        // No cap configured -> the guard is inert by design (nothing to enforce), never a false reject.
        MqttTransportReceiver.ExceedsPayloadLimit(options, payloadLength: int.MaxValue).ShouldBeFalse();
    }

    /// <summary>
    /// SAFETY. Two deliveries must never share an acknowledgement-map key, however similar their content.
    /// </summary>
    /// <remarks>
    /// The delivery id used to be the hex of the message's correlation data. A correlation id is SHARED by
    /// related messages by design, so two distinct deliveries routinely collided: the second replaced the
    /// first in the pending-acknowledgement map, and acknowledging the first acknowledged the SECOND — one
    /// message settled without being processed, the other left with no handle and redelivered on session
    /// resume. Making the id independent of content is what makes that collision unrepresentable, so this
    /// arm binds the property at its source rather than probing for one lucky collision.
    /// </remarks>
    [Fact]
    public void NeverIssueTheSameDeliveryIdTwice()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 1000; i++)
        {
            // Every id must be NEW. A single repeat is a message settled against another's handle.
            ids.Add(MqttTransportReceiver.CreateDeliveryId()).ShouldBeTrue();
        }

        ids.Count.ShouldBe(1000);
    }

    /// <summary>
    /// PRECISION. The correlation itself must survive — it moved to the field whose contract is
    /// correlation, rather than being destroyed along with the collision.
    /// </summary>
    /// <remarks>
    /// The fix would be a regression if it dropped the producer's correlation id: it was previously
    /// recoverable (hex-decoded from the delivery id) and the receiver never populated
    /// <c>CorrelationId</c>. The sender writes it with UTF-8, so this reverses exactly that.
    /// </remarks>
    [Fact]
    public void RoundTripTheProducersCorrelationId()
    {
        var written = System.Text.Encoding.UTF8.GetBytes("order-42");

        MqttTransportReceiver.DecodeCorrelationId(written).ShouldBe("order-42");
    }

    /// <summary>
    /// PRECISION. "No correlation set" is reported as <see langword="null"/>, not as an empty string.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public void ReportNoCorrelationAsNull(byte[]? correlationData)
    {
        // The field is nullable precisely so a consumer can tell "the producer set none" from a value.
        MqttTransportReceiver.DecodeCorrelationId(correlationData).ShouldBeNull();
    }
}
