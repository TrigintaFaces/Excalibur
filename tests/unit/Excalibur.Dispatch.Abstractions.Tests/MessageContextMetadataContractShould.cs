// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Abstractions.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Pattern", "Contract")]
public sealed class MessageContextMetadataContractShould
{
    private const string MessageTypeKey = "__MessageType";
    private const string RoutingDefaultKey = "__MessageTypeIsRoutingDefault";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveExplicitNullRegardlessOfPriorMaterialization(bool materializeFirst)
    {
        var context = new MessageContext();
        if (materializeFirst)
        {
            context.ContainsItem("missing").ShouldBeFalse();
        }

        context.SetMessageType(null);

        context.Items.ContainsKey(MessageTypeKey).ShouldBeTrue();
        context.Items[MessageTypeKey].ShouldBeNull();
        context.GetMessageType().ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HonorRemovalOfMaterializedMetadata(bool clearAll)
    {
        var context = new MessageContext();
        context.SetMessageType("orders");
        context.MarkMessageTypeAsRoutingDefault();
        if (clearAll)
        {
            context.Items.Clear();
        }
        else
        {
            context.Items.Remove(MessageTypeKey).ShouldBeTrue();
            context.Items.Remove(RoutingDefaultKey).ShouldBeTrue();
        }

        context.GetMessageType().ShouldBeNull();
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();

        context.SetMessageType("replacement");
        context.MarkMessageTypeAsRoutingDefault();
        context.Items[MessageTypeKey].ShouldBe("replacement");
        context.Items[RoutingDefaultKey].ShouldBe(true);
    }

    [Fact]
    public void HonorDirectDictionaryReplacements()
    {
        var context = new MessageContext();
        context.SetMessageType("original");
        context.MarkMessageTypeAsRoutingDefault();
        context.Items[MessageTypeKey] = 42;
        context.Items[RoutingDefaultKey] = "true";
        context.GetMessageType().ShouldBeNull();
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();

        context.Items[MessageTypeKey] = "replacement";
        context.Items[RoutingDefaultKey] = false;
        context.GetMessageType().ShouldBe("replacement");
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();

        context.Items[MessageTypeKey] = null!;
        context.GetMessageType().ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearDeferredAndMaterializedMetadataOnReset(bool materializeFirst)
    {
        var context = new MessageContext();
        context.SetMessageType(null);
        context.MarkMessageTypeAsRoutingDefault();
        if (materializeFirst)
        {
            context.Items.ContainsKey(RoutingDefaultKey).ShouldBeTrue();
        }

        context.Reset();

        context.GetMessageType().ShouldBeNull();
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();
        context.Items.ShouldBeEmpty();
        context.SetMessageType(null);
        context.Items.ContainsKey(MessageTypeKey).ShouldBeTrue();
    }

    [Fact]
    public void HonorSubclassInterfaceDictionaryForEveryMetadataOperation()
    {
        var derived = new CustomItemsContext();
        IMessageContext context = derived;
        context.SetMessageType(null);
        context.Items.ContainsKey(MessageTypeKey).ShouldBeTrue();
        context.Items[MessageTypeKey] = "custom";
        context.GetMessageType().ShouldBe("custom");
        context.Items[MessageTypeKey.ToUpperInvariant()] = "case-insensitive";
        context.GetMessageType().ShouldBe("case-insensitive");
        context.MarkMessageTypeAsRoutingDefault();
        context.Items[RoutingDefaultKey].ShouldBe(true);
        context.IsMessageTypeRoutingDefault().ShouldBeTrue();
        context.Items.Remove(RoutingDefaultKey).ShouldBeTrue();
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();
        derived.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeepMessageTypeAndRoutingMarkerIndependent(bool removeMessageType)
    {
        var context = new MessageContext();
        context.SetMessageType("orders");
        context.MarkMessageTypeAsRoutingDefault();
        context.Items.Remove(removeMessageType ? MessageTypeKey : RoutingDefaultKey).ShouldBeTrue();

        context.GetMessageType().ShouldBe(removeMessageType ? null : "orders");
        context.IsMessageTypeRoutingDefault().ShouldBe(removeMessageType);
    }

    private sealed class CustomItemsContext : MessageContext, IMessageContext
    {
        IDictionary<string, object> IMessageContext.Items { get; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CopyExplicitNullToIndependentChildDictionary()
    {
        var parent = new MessageContext();
        parent.SetMessageType(null);
        parent.MarkMessageTypeAsRoutingDefault();

        var child = parent.CreateChildContext();

        child.Items.ContainsKey(MessageTypeKey).ShouldBeTrue();
        child.Items[MessageTypeKey].ShouldBeNull();
        child.IsMessageTypeRoutingDefault().ShouldBeTrue();
        child.Items.Remove(MessageTypeKey).ShouldBeTrue();
        child.Items.Remove(RoutingDefaultKey).ShouldBeTrue();
        parent.Items.ContainsKey(MessageTypeKey).ShouldBeTrue();
        parent.IsMessageTypeRoutingDefault().ShouldBeTrue();
    }

    [Fact]
    public void HonorInheritedDictionaryInOrdinarySubclass()
    {
        IMessageContext context = new InheritedContext();
        context.SetMessageType(null);
        context.Items.ContainsKey(MessageTypeKey).ShouldBeTrue();
        context.SetMessageType("orders");
        context.MarkMessageTypeAsRoutingDefault();
        context.GetMessageType().ShouldBe("orders");
        context.IsMessageTypeRoutingDefault().ShouldBeTrue();
        context.Items.Clear();
        context.GetMessageType().ShouldBeNull();
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();
    }

    private sealed class InheritedContext : MessageContext;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoveDeferredMetadataBeforeObservingDictionary(bool removeMessageType)
    {
        var context = new MessageContext();
        context.SetMessageType("orders");
        context.MarkMessageTypeAsRoutingDefault();

        context.RemoveItem(removeMessageType ? MessageTypeKey : RoutingDefaultKey);

        context.GetMessageType().ShouldBe(removeMessageType ? null : "orders");
        context.IsMessageTypeRoutingDefault().ShouldBe(removeMessageType);
        context.Items.ContainsKey(MessageTypeKey).ShouldBe(!removeMessageType);
        context.Items.ContainsKey(RoutingDefaultKey).ShouldBe(removeMessageType);
        context.SetMessageType("again");
        context.MarkMessageTypeAsRoutingDefault();
        context.GetMessageType().ShouldBe("again");
        context.IsMessageTypeRoutingDefault().ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveGenericHelperMetadataSemantics(bool materializeFirst)
    {
        var context = new MessageContext();
        context.SetMessageType(null);
        context.MarkMessageTypeAsRoutingDefault();
        if (materializeFirst)
        {
            context.Items.ContainsKey(MessageTypeKey).ShouldBeTrue();
        }

        context.ContainsItem(MessageTypeKey).ShouldBeTrue();
        context.GetItem<string>(MessageTypeKey).ShouldBeNull();
        context.GetItem(MessageTypeKey, "fallback").ShouldBe("fallback");
        context.GetItem<int>(MessageTypeKey).ShouldBe(0);
        context.GetItem(RoutingDefaultKey, false).ShouldBeTrue();
        context.GetItem(RoutingDefaultKey, "wrong-type").ShouldBe("wrong-type");
        var marker = context.GetItem<object>(RoutingDefaultKey);
        ReferenceEquals(marker, context.Items[RoutingDefaultKey]).ShouldBeTrue();

        context.RemoveItem(MessageTypeKey);
        context.ContainsItem(MessageTypeKey).ShouldBeFalse();
        context.IsMessageTypeRoutingDefault().ShouldBeTrue();
        context.RemoveItem(RoutingDefaultKey);
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();
        context.Items.ShouldBeEmpty();
    }

    [Fact]
    public void PreserveConcreteNullKeyExceptions()
    {
        var context = new MessageContext();
        Should.Throw<ArgumentNullException>(() => context.ContainsItem(null!)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => context.GetItem<object>(null!)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => context.GetItem(null!, "default")).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => context.RemoveItem(null!)).ParamName.ShouldBe("key");
        context.SetMessageType(null);
        context.Items.ContainsKey(MessageTypeKey).ShouldBeTrue();
    }

    [Fact]
    public void LeaveCustomDictionaryNullKeyPolicyInControl()
    {
        var context = A.Fake<IMessageContext>();
        var items = A.Fake<IDictionary<string, object>>();
        A.CallTo(() => context.Items).Returns(items);
        A.CallTo(() => items.ContainsKey(null!)).Returns(true);
        A.CallTo(() => items.Remove(null!)).Returns(true);

        context.ContainsItem(null!).ShouldBeTrue();
        context.GetItem<object>(null!).ShouldBeNull();
        context.GetItem(null!, "fallback").ShouldBe("fallback");
        context.RemoveItem(null!);

        A.CallTo(() => items.ContainsKey(null!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => items.Remove(null!)).MustHaveHappenedOnceExactly();
    }
}
