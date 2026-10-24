// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Pattern", "Regression")]
public sealed class GenericMessageEnvelopeIdentityShould
{
    [MessageName("envelope.identity.regression")]
    private sealed class NamedMessage : IDispatchEvent;

    private sealed class UnnamedMessage : IDispatchEvent;

    [Fact]
    public void StoreTheRuntimeMessagesDeclaredNameEvenThroughAnInterface()
    {
        var metadata = new MessageMetadata("message", "correlation", null, null, null, null, "application/json", "1", "1");
        using var envelope = new MessageEnvelope<IDispatchMessage>(new NamedMessage(), metadata);
        var inner = typeof(MessageEnvelope<IDispatchMessage>).GetField("_inner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(envelope).ShouldBeOfType<MessageEnvelope>();
        inner.MessageType.ShouldBe("envelope.identity.regression");
    }

    [Fact]
    public void RefuseToInventAnIdentityForAnUnnamedMessage()
    {
        var metadata = new MessageMetadata("message", "correlation", null, null, null, null, "application/json", "1", "1");
        using var envelope = new MessageEnvelope<IDispatchMessage>(new UnnamedMessage(), metadata);
        var inner = typeof(MessageEnvelope<IDispatchMessage>).GetField("_inner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(envelope).ShouldBeOfType<MessageEnvelope>();
        inner.MessageType.ShouldBeNull();
    }

    [Fact]
    public void PreserveTheInboundIdentityAcrossMetadataReplacement()
    {
        var metadata = new MessageMetadata("message", "correlation", null, null, null, null, "application/json", "1", "1");
        var context = new MessageContext();
        context.SetMessageType("external.identity");
        using var envelope = new MessageEnvelope<IDispatchMessage>(new NamedMessage(), metadata, context);
        using var replaced = envelope.WithMetadata(metadata);
        var inner = typeof(MessageEnvelope<IDispatchMessage>).GetField("_inner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(replaced).ShouldBeOfType<MessageEnvelope>();
        inner.MessageType.ShouldBe("external.identity");
        replaced.Context.ShouldBeSameAs(context);
        context.GetMessageType().ShouldBe("external.identity");
    }
}
