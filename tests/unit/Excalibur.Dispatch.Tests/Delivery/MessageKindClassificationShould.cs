// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// "What kinds is this message?" must have one answer. Every entry point that asks it has to reach the same
/// classifier, or profile selection and middleware applicability can disagree about what a message is.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class MessageKindClassificationShould
{
	[Fact]
	public void ClassifyAGenericActionAsAnAction() =>
		DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(GenericAction))
			.ShouldBe(MessageKinds.Action);

	[Fact]
	public void ClassifyAnEventAsAnEvent() =>
		DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(PlainEvent))
			.ShouldBe(MessageKinds.Event);

	/// <summary>
	/// A message may be more than one kind. A classifier that returns on the first match reports only the
	/// first, which is how the copies disagreed with each other.
	/// </summary>
	[Fact]
	public void ReportEveryKindAMessageImplements() =>
		DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(ActionAndEvent))
			.ShouldBe(MessageKinds.Action | MessageKinds.Event);

	/// <summary>
	/// Fail closed: a message that states no kind is treated as every kind, so every middleware applies to
	/// it. The type we know least about gets the most protection, not the least.
	/// </summary>
	[Fact]
	public void TreatAMessageThatStatesNoKindAsEveryKind() =>
		DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(UnclassifiableCommand))
			.ShouldBe(MessageKinds.All);

	/// <summary>
	/// Name is not a kind. Classifying by type-name suffix returned a SINGLE kind, so only that kind's
	/// middleware applied — strictly less protection than the unclassified fall-through gives.
	/// </summary>
	[Fact]
	public void NotClassifyByTypeNameSuffix()
	{
		DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(LooksLikeAnEvent))
			.ShouldBe(MessageKinds.All);

		DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(UnclassifiableCommand))
			.ShouldNotBe(MessageKinds.Action);
	}

	/// <summary>
	/// Profile compatibility is one of the entry points that used to answer this question itself.
	/// </summary>
	[Fact]
	public void AgreeBetweenProfileCompatibilityAndTheClassifier()
	{
		var profile = new PipelineProfile("events-only", MessageKinds.Event);

		profile.IsCompatible(new PlainEvent()).ShouldBeTrue();
		profile.IsCompatible(new GenericAction()).ShouldBeFalse();

		// An unclassified message is every kind, so it reaches this profile too rather than slipping past it.
		profile.IsCompatible(new UnclassifiableCommand()).ShouldBeTrue();
	}

	/// <summary>
	/// Liveness arm: failing closed must not make everything fail. A message that states a kind still routes.
	/// </summary>
	[Fact]
	public void StillAcceptAClassifiableMessageThroughAProfile()
	{
		var profile = new PipelineProfile("everything", MessageKinds.All);

		profile.IsCompatible(new ActionAndEvent()).ShouldBeTrue();
	}

	private sealed record GenericAction : IDispatchAction<string>;

	private sealed record PlainEvent : IDispatchEvent;

	private sealed record ActionAndEvent : IDispatchAction, IDispatchEvent;

	private sealed record UnclassifiableCommand : IDispatchMessage;

	private sealed record LooksLikeAnEvent : IDispatchMessage;
}
