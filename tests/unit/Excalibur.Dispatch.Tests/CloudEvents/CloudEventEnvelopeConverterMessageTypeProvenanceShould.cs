// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

/// <summary>
/// Locks CloudEventEnvelopeConverter's resolution of the CloudEvent <c>type</c> attribute across the
/// two provenances a context's message type can have (Excalibur_Dispatch-sbna1m): a foreign identity
/// received from elsewhere, which must survive a re-emit verbatim, and a routing name the dispatcher
/// defaulted in from the CLR type, which must never leak past the framework's boundary.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CloudEventEnvelopeConverterMessageTypeProvenanceShould
{
	private const string DeclaredName = "contoso.orders.order-placed.v1";

	private readonly CloudEventEnvelopeConverter _sut = new(new CloudEventOptions());

	[Fact]
	public async Task EmitTheDeclaredName_WhenTheContextMessageTypeIsAFrameworkDefaultedRoutingName()
	{
		// Arrange: mirrors what Dispatcher.InitializeContext does -- default the routing name in, then
		// mark it, exactly as the production code path does.
		var envelope = new MessageEnvelope(new DeclaredNameEvent())
		{
			MessageType = typeof(DeclaredNameEvent).FullName,
		};
		envelope.MarkMessageTypeAsRoutingDefault();

		var cloudEvent = await _sut.FromEnvelopeAsync(envelope, CancellationToken.None);

		cloudEvent.Type.ShouldBe(DeclaredName);
		cloudEvent.Type.ShouldNotBe(typeof(DeclaredNameEvent).FullName);
	}

	[Fact]
	public async Task FallBackToTheRoutingName_WhenItIsFrameworkDefaultedButTheMessageDeclaresNone()
	{
		// Liveness: an undeclared type must still emit something rather than a null/throw -- the
		// routing name is the only value available, so it is used as a last resort.
		var envelope = new MessageEnvelope(new UndeclaredNameEvent())
		{
			MessageType = typeof(UndeclaredNameEvent).FullName,
		};
		envelope.MarkMessageTypeAsRoutingDefault();

		var cloudEvent = await _sut.FromEnvelopeAsync(envelope, CancellationToken.None);

		cloudEvent.Type.ShouldBe(typeof(UndeclaredNameEvent).FullName);
	}

	[Fact]
	public async Task PreserveAForeignIdentityVerbatim_EvenWhenTheMessageDeclaresItsOwnName()
	{
		// A receive-then-re-emit round trip: the context carries another organisation's type string,
		// unmarked (nothing in this framework defaulted it). It must win over this process's own
		// declared name for the same CLR type -- rewriting it would silently break interop preservation.
		var envelope = new MessageEnvelope(new DeclaredNameEvent())
		{
			MessageType = "com.acme.order-placed.v3",
		};

		var cloudEvent = await _sut.FromEnvelopeAsync(envelope, CancellationToken.None);

		cloudEvent.Type.ShouldBe("com.acme.order-placed.v3");
	}

	[Fact]
	public async Task DeclareItsOwnName_WhenNothingSetAContextMessageTypeAtAll()
	{
		// No routing default, no foreign identity -- an envelope built without ever touching
		// MessageType. The declared name is stated directly, matching the pre-existing behaviour for
		// this arm.
		var envelope = new MessageEnvelope(new DeclaredNameEvent());

		var cloudEvent = await _sut.FromEnvelopeAsync(envelope, CancellationToken.None);

		cloudEvent.Type.ShouldBe(DeclaredName);
	}

	[MessageName(DeclaredName)]
	private sealed record DeclaredNameEvent : IDispatchEvent;

	private sealed record UndeclaredNameEvent : IDispatchEvent;
}
