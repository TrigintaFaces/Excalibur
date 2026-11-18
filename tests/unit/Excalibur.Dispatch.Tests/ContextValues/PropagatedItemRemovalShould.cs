// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>Checks exact removal and child-context ownership.</summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PropagatedItemRemovalShould
{
    [Fact]
    public void RemoveOnlyExactMarkedKeyWithoutChangingScratchOrOtherValues()
    {
        var context = new MessageContext();
        context.SetItem("origin", "scratch");
        context.SetPropagatedItem("origin", "owned");
        context.SetPropagatedItem("Origin", "different-case");
        var view = context.GetPropagatedItems();
        context.RemovePropagatedItem("origin").ShouldBeTrue();
        context.RemovePropagatedItem("origin").ShouldBeFalse();
        view.ContainsKey("origin").ShouldBeFalse();
        view["Origin"].ShouldBe("different-case");
        context.GetItem<string>("origin").ShouldBe("scratch");
    }

    [Fact]
    public void LeaveUnmarkedContextFeaturesUnchanged()
    {
        var context = new MessageContext();
        var count = context.Features.Count;
        context.RemovePropagatedItem("absent").ShouldBeFalse();
        context.Features.Count.ShouldBe(count);
        context.GetPropagatedItems().Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveFromChildWithoutChangingParentRetryOrSibling()
    {
        var parent = new MessageContext();
        parent.SetPropagatedItem("owned-origin", "original");
        parent.SetPropagatedItem("tenant-custom", "tenantA ");
        var child = parent.CreateChildContext();
        var sibling = parent.CreateChildContext();
        child.RemovePropagatedItem("owned-origin").ShouldBeTrue();
        child.GetPropagatedItem("owned-origin").ShouldBeNull();
        parent.GetPropagatedItem("owned-origin").ShouldBe("original");
        sibling.GetPropagatedItem("owned-origin").ShouldBe("original");
        child.GetPropagatedItem("tenant-custom").ShouldBe("tenantA ");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectInvalidKey(string? key)
    {
        var context = new MessageContext();
        Should.Throw<ArgumentException>(() => context.RemovePropagatedItem(key!));
    }

    [Fact]
    public void RejectNullContext()
    {
        Should.Throw<ArgumentNullException>(() => MessageContextPropagationExtensions.RemovePropagatedItem(null!, "key"));
    }
}
