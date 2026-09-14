// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineProfileRegistry"/> public class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait(TraitNames.Feature, TestFeatures.Configuration)]
public sealed class PipelineProfileRegistryShould
{
	private readonly PipelineProfileRegistry _sut;

	public PipelineProfileRegistryShould()
	{
		_sut = new PipelineProfileRegistry();
	}

	[Fact]
	public void ImplementIPipelineProfileRegistry()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IPipelineProfileRegistry>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert -- PipelineProfileRegistry is internal (Sprint 641: Public API Surface Reduction)
		typeof(PipelineProfileRegistry).IsPublic.ShouldBeFalse();
		typeof(PipelineProfileRegistry).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void RegisterDefaultProfilesOnConstruction()
	{
		// Assert - Default profiles should be registered. The registry now wires the full
		// DefaultPipelineProfiles set (lowercase/hyphenated keys incl. the revived
		// "default" profile) in place of the old empty capitalized Strict/InternalEvent
		// shells. Document/Minimal remain registered under their original names.
		_sut.GetProfile(DefaultPipelineProfiles.Default).ShouldNotBeNull();
		_sut.GetProfile(DefaultPipelineProfiles.Strict).ShouldNotBeNull();
		_sut.GetProfile(DefaultPipelineProfiles.InternalEvent).ShouldNotBeNull();
		_sut.GetProfile(DefaultPipelineProfiles.Direct).ShouldNotBeNull();
		_sut.GetProfile("Document").ShouldNotBeNull();
		_sut.GetProfile("Minimal").ShouldNotBeNull();
	}

	[Fact]
	public void AcceptApplicabilityStrategyInConstructor()
	{
		// Arrange
		var strategy = A.Fake<IMiddlewareApplicabilityStrategy>();

		// Act
		var registry = new PipelineProfileRegistry(strategy);

		// Assert
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptNullApplicabilityStrategy()
	{
		// Act
		var registry = new PipelineProfileRegistry(null);

		// Assert
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterProfile()
	{
		// Arrange
		var profile = new PipelineProfile("CustomProfile", MessageKinds.All);

		// Act
		_sut.RegisterProfile(profile);

		// Assert
		_sut.GetProfile("CustomProfile").ShouldBe(profile);
	}

	[Fact]
	public void ThrowWhenRegisteringNullProfile()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.RegisterProfile(null!));
	}

	[Fact]
	public void ThrowWhenRegisteringDuplicateProfileName()
	{
		// Arrange
		var profile1 = new PipelineProfile("Duplicate", MessageKinds.All);
		var profile2 = new PipelineProfile("Duplicate", MessageKinds.Action);
		_sut.RegisterProfile(profile1);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			_sut.RegisterProfile(profile2));
	}

	[Fact]
	public void GetProfileByName()
	{
		// Arrange
		var profile = new PipelineProfile("TestProfile", MessageKinds.All);
		_sut.RegisterProfile(profile);

		// Act
		var result = _sut.GetProfile("TestProfile");

		// Assert
		result.ShouldBe(profile);
	}

	[Fact]
	public void ReturnNullForNonExistentProfile()
	{
		// Act
		var result = _sut.GetProfile("NonExistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetProfileNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.GetProfile(null!));
	}

	[Fact]
	public void ThrowWhenGetProfileNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.GetProfile(string.Empty));
	}

	[Fact]
	public void ThrowWhenGetProfileNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.GetProfile("   "));
	}

	[Fact]
	public void GetAllProfiles()
	{
		// Arrange
		var customProfile = new PipelineProfile("Custom", MessageKinds.All);
		_sut.RegisterProfile(customProfile);

		// Act
		var profiles = _sut.GetAllProfiles().ToList();

		// Assert
		profiles.ShouldNotBeEmpty();
		profiles.ShouldContain(customProfile);
		profiles.Count.ShouldBeGreaterThan(4); // Default profiles + custom
	}

	[Fact]
	public void GetProfileNames()
	{
		// Arrange
		var customProfile = new PipelineProfile("Custom", MessageKinds.All);
		_sut.RegisterProfile(customProfile);

		// Act
		var names = _sut.GetProfileNames().ToList();

		// Assert - default profiles use the DefaultPipelineProfiles lowercase/hyphenated
		// keys (incl. the revived "default"); Document/Minimal keep their names.
		names.ShouldContain(DefaultPipelineProfiles.Default);
		names.ShouldContain(DefaultPipelineProfiles.Strict);
		names.ShouldContain(DefaultPipelineProfiles.InternalEvent);
		names.ShouldContain(DefaultPipelineProfiles.Direct);
		names.ShouldContain("Document");
		names.ShouldContain("Minimal");
		names.ShouldContain("Custom");
	}

	[Fact]
	public void RemoveProfile()
	{
		// Arrange
		var profile = new PipelineProfile("ToRemove", MessageKinds.All);
		_sut.RegisterProfile(profile);

		// Act
		var result = _sut.RemoveProfile("ToRemove");

		// Assert
		result.ShouldBeTrue();
		_sut.GetProfile("ToRemove").ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenRemovingNonExistentProfile()
	{
		// Act
		var result = _sut.RemoveProfile("NonExistent");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenRemoveProfileNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.RemoveProfile(null!));
	}

	[Fact]
	public void ThrowWhenRemoveProfileNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.RemoveProfile(string.Empty));
	}

	[Fact]
	public void ThrowWhenRemoveProfileNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.RemoveProfile("   "));
	}

	[Fact]
	public void SelectProfileForActionMessage()
	{
		// Arrange
		var message = A.Fake<IDispatchAction<string>>();

		// Act
		var profile = _sut.SelectProfile(message);

		// Assert
		profile.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenSelectProfileMessageIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sut.SelectProfile(null!));
	}

	[Fact]
	public void SetDefaultProfile()
	{
		// Act & Assert - Should not throw (uses the registered lowercase profile key)
		Should.NotThrow(() => _sut.SetDefaultProfile(DefaultPipelineProfiles.Strict));
	}

	[Fact]
	public void ThrowWhenSetDefaultProfileNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.SetDefaultProfile(null!));
	}

	[Fact]
	public void ThrowWhenSetDefaultProfileNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.SetDefaultProfile(string.Empty));
	}

	[Fact]
	public void ThrowWhenSetDefaultProfileNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_sut.SetDefaultProfile("   "));
	}

	[Fact]
	public void ThrowWhenSetDefaultProfileDoesNotExist()
	{
		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			_sut.SetDefaultProfile("NonExistent"));
	}

	[Fact]
	public void AllowRemovingDefaultProfiles()
	{
		// Act
		var result = _sut.RemoveProfile("Minimal");

		// Assert
		result.ShouldBeTrue();
		_sut.GetProfile("Minimal").ShouldBeNull();
	}

	[Fact]
	public void MaintainProfileOrderInGetAllProfiles()
	{
		// Arrange
		var profile1 = new PipelineProfile("AAA", MessageKinds.All);
		var profile2 = new PipelineProfile("ZZZ", MessageKinds.All);
		_sut.RegisterProfile(profile1);
		_sut.RegisterProfile(profile2);

		// Act
		var profiles = _sut.GetAllProfiles().ToList();

		// Assert
		profiles.ShouldContain(p => p.Name == "AAA");
		profiles.ShouldContain(p => p.Name == "ZZZ");
	}

	#region Profile Selection Cache

	// Excalibur_Dispatch-zvcdsf: the selection cache is deliberately never frozen. A prior
	// freeze-to-FrozenDictionary design measured slower at every message-type count tested and
	// disabled the fall-through for a type first seen after the freeze, forcing that type to re-run
	// the full profile scan on every subsequent dispatch forever. These tests lock the replacement
	// behaviour: a plain warm cache that always re-caches a miss, with no freeze cliff to fall into.

	[Fact]
	public void ReturnConsistentProfileOnRepeatedCalls()
	{
		// Arrange
		var message = A.Fake<IDispatchAction<string>>();

		// Act — multiple calls should return same cached result
		var first = _sut.SelectProfile(message);
		var second = _sut.SelectProfile(message);
		var third = _sut.SelectProfile(message);

		// Assert
		first.ShouldBe(second);
		second.ShouldBe(third);
	}

	[Fact]
	public void CacheAMessageTypeSeenAfterManyOthersInsteadOfRescanningForever()
	{
		// Arrange — a strategy whose call count reveals the full profile scan running, since that
		// scan (SelectProfileCore) is the only caller. A cache hit never reaches it.
		var strategy = A.Fake<IMiddlewareApplicabilityStrategy>();
		_ = A.CallTo(() => strategy.DetermineMessageKinds(A<IDispatchMessage>._)).Returns(MessageKinds.Action);
		var sut = new PipelineProfileRegistry(strategy);

		// Warm the cache with many other message instances first, the way a long-running process
		// would before encountering a message it has never dispatched before.
		for (var i = 0; i < 50; i++)
		{
			_ = sut.SelectProfile(A.Fake<IDispatchAction<string>>());
		}

		var lateArrival = A.Fake<IDispatchAction<int>>();

		// Act — first call is the cold path (one scan); second must be a cache hit (no scan). Under
		// the deleted freeze design, a message first seen this late could be stranded outside the
		// frozen dictionary and re-scan on every subsequent call.
		_ = sut.SelectProfile(lateArrival);
		_ = sut.SelectProfile(lateArrival);

		// Assert — exactly one scan for this message, not one per call.
		A.CallTo(() => strategy.DetermineMessageKinds(
				A<IDispatchMessage>.That.Matches(m => ReferenceEquals(m, lateArrival))))
			.MustHaveHappenedOnceExactly();
	}

	#endregion
}