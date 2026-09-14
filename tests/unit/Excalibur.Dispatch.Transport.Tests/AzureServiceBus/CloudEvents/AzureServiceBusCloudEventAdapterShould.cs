// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

/// <summary>
/// Locks the wire format this adapter EMITS.
/// </summary>
/// <remarks>
/// <para>
/// This adapter is an encoder: its only public operation writes a Service Bus message. So the property
/// worth binding is the one a consumer's other systems parse - the application-property names on the
/// wire - and not any internal helper that produces them.
/// </para>
/// <para>
/// <b>Why the negative arm is not redundant.</b> Asserting the new spelling is present passes against an
/// adapter that emits BOTH spellings, which is a real failure mode for a prefix migration: the AMQP
/// binding requires one separator for every attribute on a single message, so a message carrying both is
/// non-conformant even though every individual attribute looks right. Presence and absence are two
/// different requirements and both are asserted.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AzureServiceBusCloudEventAdapterShould
{
    private const string AmqpPrefix = "cloudEvents_";

    private const string LegacyPrefix = "ce-";

    private readonly AzureServiceBusCloudEventAdapter _adapter = new(
        Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
        {
            DefaultSource = new Uri("https://test.excalibur.io"),
            DefaultMode = CloudEventMode.Binary,
        }),
        Microsoft.Extensions.Options.Options.Create(new AzureServiceBusCloudEventOptions()),
        NullLogger<AzureServiceBusCloudEventAdapter>.Instance);

    /// <summary>
    /// LIVENESS. Every CloudEvents attribute reaches the wire under the AMQP binding's prefix.
    /// </summary>
    [Fact]
    public async Task Emit_binary_mode_attributes_under_the_amqp_prefix()
    {
        var cloudEvent = Event();

        var message = await _adapter.ToTransportMessageAsync(
            cloudEvent, CloudEventMode.Binary, CancellationToken.None);

        message.ApplicationProperties.ShouldContainKeyAndValue(AmqpPrefix + "specversion", "1.0");
        message.ApplicationProperties.ShouldContainKeyAndValue(AmqpPrefix + "type", "orders.created");
        message.ApplicationProperties.ShouldContainKeyAndValue(AmqpPrefix + "id", "evt-1");
        message.ApplicationProperties.ShouldContainKeyAndValue(
            AmqpPrefix + "source", "https://source.excalibur.io/");
    }

    /// <summary>
    /// SAFETY. No attribute reaches the wire under the superseded prefix.
    /// </summary>
    /// <remarks>
    /// The pair to the arm above. A message carrying a mix of spellings satisfies that one and is still
    /// wrong, because a conformant reader keyed to one separator sees a partial event rather than an
    /// invalid one - it decodes, with attributes silently missing.
    /// </remarks>
    [Fact]
    public async Task Emit_no_attribute_under_the_superseded_prefix()
    {
        var cloudEvent = Event();

        var message = await _adapter.ToTransportMessageAsync(
            cloudEvent, CloudEventMode.Binary, CancellationToken.None);

        var stragglers = message.ApplicationProperties.Keys
            .Where(key => key.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        stragglers.ShouldBeEmpty(
            "the AMQP binding requires a single separator for every attribute on one message, so an "
            + "attribute left under the old spelling is not merely stale - it is invisible to a reader "
            + "keyed to the new one, which decodes the event with that attribute silently absent");
    }

    private static CloudEvent Event() =>
        new(CloudEventsSpecVersion.V1_0)
        {
            Id = "evt-1",
            Type = "orders.created",
            Source = new Uri("https://source.excalibur.io"),
            Data = "payload",
            DataContentType = "text/plain",
        };
}
