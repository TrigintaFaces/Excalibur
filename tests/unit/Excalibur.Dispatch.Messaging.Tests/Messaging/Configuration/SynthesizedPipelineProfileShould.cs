// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="SynthesizedPipelineProfile"/> message classification.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait(TraitNames.Feature, TestFeatures.Configuration)]
public sealed class SynthesizedPipelineProfileShould
{
	[Fact]
	public void TreatGenericActionMessageAsAction()
	{
		// Arrange -- the message declares only IDispatchAction<TResponse>. Classification must still see it as an
		// Action: the generic interface derives from the non-generic one, so an assignability check covers both.
		// Were that not so, a request/response message would be judged incompatible with an action-only profile.
		var profile = CreateProfile(MessageKinds.Action);

		// Act
		var result = profile.IsCompatible(new GenericActionMessage());

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void TreatNonGenericActionMessageAsAction()
	{
		// Arrange
		var profile = CreateProfile(MessageKinds.Action);

		// Act
		var result = profile.IsCompatible(new ActionMessage());

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void RejectActionMessageForEventOnlyProfile()
	{
		// Arrange
		var profile = CreateProfile(MessageKinds.Event);

		// Act
		var result = profile.IsCompatible(new GenericActionMessage());

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenMessageIsNull()
	{
		// Arrange
		var profile = CreateProfile(MessageKinds.All);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => profile.IsCompatible(null!));
	}

	private static SynthesizedPipelineProfile CreateProfile(MessageKinds supportedMessageKinds) =>
		new(
			"Synthesized",
			"Synthesized for tests",
			[],
			isStrict: false,
			supportedMessageKinds,
			includedCount: 0,
			omittedCount: 0);

	private sealed class ActionMessage : IDispatchAction;

	private sealed class GenericActionMessage : IDispatchAction<string>;
}
