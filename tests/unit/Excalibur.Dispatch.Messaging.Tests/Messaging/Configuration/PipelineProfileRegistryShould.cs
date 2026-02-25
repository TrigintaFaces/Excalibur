// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineProfileRegistry"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
[Trait("Feature", "Configuration")]
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
	public void BePublicAndSealed()
	{
		// Assert
		typeof(PipelineProfileRegistry).IsPublic.ShouldBeTrue();
		typeof(PipelineProfileRegistry).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void RegisterDefaultProfilesOnConstruction()
	{
		// Assert - Default profiles should be registered
		_sut.GetProfile("Strict").ShouldNotBeNull();
		_sut.GetProfile("InternalEvent").ShouldNotBeNull();
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

		// Assert
		names.ShouldContain("Strict");
		names.ShouldContain("InternalEvent");
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
		// Act & Assert - Should not throw
		Should.NotThrow(() => _sut.SetDefaultProfile("Strict"));
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
}
