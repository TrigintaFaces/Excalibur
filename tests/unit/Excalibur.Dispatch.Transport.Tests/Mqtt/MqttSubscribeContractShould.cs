// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
}
