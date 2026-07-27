// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Locks the architecture ruling that a message's kind is determined BY THE INTERFACE it implements,
/// not by any per-type <c>Kind</c> property. The inert <c>Kind</c> declarations were removed because
/// classification runs through <see cref="DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(System.Type)"/>,
/// which inspects the implemented interfaces. If a future change re-introduces an authoritative,
/// per-type <c>Kind</c> that diverges from the interface-based classification, these arms stay pinned to
/// the interface contract and will RED.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MessageKindClassificationByInterfaceShould
{
	/// <summary>Implements the non-generic action interface only.</summary>
	private sealed class ActionMessage : IDispatchAction;

	/// <summary>Implements the generic action interface only.</summary>
	private sealed class GenericActionMessage : IDispatchAction<string>;

	/// <summary>Implements the event interface only.</summary>
	private sealed class EventMessage : IDispatchEvent;

	/// <summary>Implements only the bare marker — no kind interface.</summary>
	private sealed class KindlessMessage : IDispatchMessage;

	[Fact]
	public void ClassifyAnIDispatchActionAsAction()
	{
		var kinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(ActionMessage));

		kinds.ShouldBe(
			MessageKinds.Action,
			"a type implementing IDispatchAction is classified by its interface, not a Kind property");
	}

	[Fact]
	public void ClassifyAGenericIDispatchActionAsAction()
	{
		var kinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(GenericActionMessage));

		kinds.ShouldBe(
			MessageKinds.Action,
			"a type implementing IDispatchAction<T> is classified by its interface, not a Kind property");
	}

	[Fact]
	public void ClassifyAnIDispatchEventAsEvent()
	{
		var kinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(EventMessage));

		kinds.ShouldBe(
			MessageKinds.Event,
			"a type implementing IDispatchEvent is classified by its interface, not a Kind property");
	}

	[Fact]
	public void ClassifyAKindlessMessageAsAll()
	{
		var kinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(KindlessMessage));

		kinds.ShouldBe(
			MessageKinds.All,
			"a bare IDispatchMessage carries no kind interface, so it fails closed to All — every "
			+ "middleware applies (matching KindlessMessageFailsOpenShould)");
	}
}
