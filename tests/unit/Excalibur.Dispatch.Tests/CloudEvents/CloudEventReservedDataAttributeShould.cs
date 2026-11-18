// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class CloudEventReservedDataAttributeShould
{
    [Theory]
    [InlineData("data")]
    [InlineData("Data")]
    [InlineData("d-a-t-a")]
    public void PreserveThePayloadAndOrdinaryExtensionsWhenAnItemSanitizesToTheReservedDataName(string key)
    {
        var context = new MessageContext();
        context.SetPropagatedItem(key, "must-not-be-an-attribute");
        context.SetPropagatedItem("tenantroute", "eu-west");
        var payload = new ReservedDataEvent();

        var cloudEvent = Should.NotThrow(() => payload.ToCloudEvent(context));

        cloudEvent.Data.ShouldBeSameAs(payload);
        cloudEvent.GetPopulatedAttributes().Select(attribute => attribute.Key.Name).ShouldNotContain("data");
        cloudEvent["tenantroute"].ShouldBe("eu-west");
    }

    [Theory]
    [InlineData("route", "data", "routedata", true)]
    [InlineData("da", "ta", "data", false)]
    public void ApplyTheReservedNameRuleAfterComposingThePrefix(string prefix, string key, string name, bool emitted)
    {
        var payload = new ReservedDataEvent();
        var cloudEvent = new CloudNative.CloudEvents.CloudEvent { Data = payload };

        CloudEventItemAttributes.Apply(cloudEvent, [new KeyValuePair<string, string>(key, "value")], prefix);

        cloudEvent.Data.ShouldBeSameAs(payload);
        if (emitted) { cloudEvent[name].ShouldBe("value"); }
        else { cloudEvent.GetPopulatedAttributes().Select(attribute => attribute.Key.Name).ShouldNotContain(name); }
    }

    [MessageName("contoso.reserved-data.v1")]
    private sealed record ReservedDataEvent : IDispatchEvent;
}
