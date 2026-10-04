// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Pipeline")]
[Trait("Pattern", "Regression")]
public sealed class ProfileSelectionFreshnessShould
{
	[Fact]
	public void ReevaluateInstanceMatchersForEachMessage()
	{
		var registry = EmptyRegistry();
		var profile = A.Fake<IPipelineProfile>(options => options.Implements<IPipelineProfileMatcher>());
		A.CallTo(() => profile.Name).Returns("conditional");
		A.CallTo(() => ((IPipelineProfileMatcher)profile).IsCompatible(A<IDispatchMessage>._))
			.ReturnsLazily((IDispatchMessage message) => ((Command)message).Selected);
		registry.RegisterProfile(profile);
		registry.SelectProfile(new Command(true)).ShouldBeSameAs(profile);
		registry.SelectProfile(new Command(false)).ShouldBeNull();
	}

	[Fact]
	public void ObserveRemovedAndNewlyRegisteredProfiles()
	{
		var registry = EmptyRegistry();
		var profile = new PipelineProfile("first", "first", [], isStrict: false, supportedMessageKinds: MessageKinds.Action);
		registry.RegisterProfile(profile);
		registry.SelectProfile(new Command(true)).ShouldBeSameAs(profile);
		registry.RemoveProfile("first").ShouldBeTrue();
		registry.SelectProfile(new Command(true)).ShouldBeNull();
		var replacement = new PipelineProfile("replacement", "replacement", [], isStrict: false, supportedMessageKinds: MessageKinds.Action);
		registry.RegisterProfile(replacement);
		registry.SelectProfile(new Command(true)).ShouldBeSameAs(replacement);
	}

	private static PipelineProfileRegistry EmptyRegistry()
	{
		var registry = new PipelineProfileRegistry();
		foreach (var name in registry.GetProfileNames()) registry.RemoveProfile(name);
		return registry;
	}
	private sealed record Command(bool Selected) : IDispatchAction;
}
