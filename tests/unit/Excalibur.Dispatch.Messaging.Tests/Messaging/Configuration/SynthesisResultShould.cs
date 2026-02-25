// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="SynthesisResult"/>.
/// </summary>
/// <remarks>
/// Tests the pipeline profile synthesis result class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class SynthesisResultShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		var profiles = new Dictionary<string, IPipelineProfile>();
		var mappings = new Dictionary<MessageKinds, string>();
		var issues = Array.Empty<ValidationIssue>();

		// Act
		var result = new SynthesisResult(profiles, mappings, issues);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Profiles.ShouldBe(profiles);
		result.Mappings.ShouldBe(mappings);
		result.ValidationIssues.ShouldBe(issues);
	}

	[Fact]
	public void Constructor_WithNullProfiles_ThrowsArgumentNullException()
	{
		// Arrange
		var mappings = new Dictionary<MessageKinds, string>();
		var issues = Array.Empty<ValidationIssue>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SynthesisResult(null!, mappings, issues));
	}

	[Fact]
	public void Constructor_WithNullMappings_ThrowsArgumentNullException()
	{
		// Arrange
		var profiles = new Dictionary<string, IPipelineProfile>();
		var issues = Array.Empty<ValidationIssue>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SynthesisResult(profiles, null!, issues));
	}

	[Fact]
	public void Constructor_WithNullValidationIssues_ThrowsArgumentNullException()
	{
		// Arrange
		var profiles = new Dictionary<string, IPipelineProfile>();
		var mappings = new Dictionary<MessageKinds, string>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SynthesisResult(profiles, mappings, null!));
	}

	#endregion

	#region Profiles Property Tests

	[Fact]
	public void Profiles_ReturnsCorrectProfiles()
	{
		// Arrange
		var profile = A.Fake<IPipelineProfile>();
		_ = A.CallTo(() => profile.Name).Returns("TestProfile");

		var profiles = new Dictionary<string, IPipelineProfile>
		{
			["TestProfile"] = profile,
		};
		var result = new SynthesisResult(profiles, new Dictionary<MessageKinds, string>(), []);

		// Act
		var returnedProfiles = result.Profiles;

		// Assert
		returnedProfiles.Count.ShouldBe(1);
		returnedProfiles.ShouldContainKey("TestProfile");
	}

	[Fact]
	public void Profiles_WithMultipleProfiles_ReturnsAll()
	{
		// Arrange
		var profile1 = A.Fake<IPipelineProfile>();
		var profile2 = A.Fake<IPipelineProfile>();
		_ = A.CallTo(() => profile1.Name).Returns("Profile1");
		_ = A.CallTo(() => profile2.Name).Returns("Profile2");

		var profiles = new Dictionary<string, IPipelineProfile>
		{
			["Profile1"] = profile1,
			["Profile2"] = profile2,
		};
		var result = new SynthesisResult(profiles, new Dictionary<MessageKinds, string>(), []);

		// Act & Assert
		result.Profiles.Count.ShouldBe(2);
	}

	#endregion

	#region Mappings Property Tests

	[Fact]
	public void Mappings_ReturnsCorrectMappings()
	{
		// Arrange
		var mappings = new Dictionary<MessageKinds, string>
		{
			[MessageKinds.Action] = "CommandProfile",
			[MessageKinds.None] = "QueryProfile",
		};
		var result = new SynthesisResult(new Dictionary<string, IPipelineProfile>(), mappings, []);

		// Act
		var returnedMappings = result.Mappings;

		// Assert
		returnedMappings.Count.ShouldBe(2);
		returnedMappings[MessageKinds.Action].ShouldBe("CommandProfile");
		returnedMappings[MessageKinds.None].ShouldBe("QueryProfile");
	}

	#endregion

	#region ValidationIssues Property Tests

	[Fact]
	public void ValidationIssues_ReturnsCorrectIssues()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Warning, "Warning 1"),
			new ValidationIssue(ValidationSeverity.Error, "Error 1"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act
		var returnedIssues = result.ValidationIssues;

		// Assert
		returnedIssues.Length.ShouldBe(2);
	}

	#endregion

	#region HasErrors Property Tests

	[Fact]
	public void HasErrors_WithNoErrors_ReturnsFalse()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Info, "Info"),
			new ValidationIssue(ValidationSeverity.Warning, "Warning"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act & Assert
		result.HasErrors.ShouldBeFalse();
	}

	[Fact]
	public void HasErrors_WithErrors_ReturnsTrue()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Warning, "Warning"),
			new ValidationIssue(ValidationSeverity.Error, "Error"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act & Assert
		result.HasErrors.ShouldBeTrue();
	}

	[Fact]
	public void HasErrors_WithEmptyIssues_ReturnsFalse()
	{
		// Arrange
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			[]);

		// Act & Assert
		result.HasErrors.ShouldBeFalse();
	}

	#endregion

	#region HasWarnings Property Tests

	[Fact]
	public void HasWarnings_WithNoWarnings_ReturnsFalse()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Info, "Info"),
			new ValidationIssue(ValidationSeverity.Error, "Error"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act & Assert
		result.HasWarnings.ShouldBeFalse();
	}

	[Fact]
	public void HasWarnings_WithWarnings_ReturnsTrue()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Warning, "Warning"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act & Assert
		result.HasWarnings.ShouldBeTrue();
	}

	[Fact]
	public void HasWarnings_WithEmptyIssues_ReturnsFalse()
	{
		// Arrange
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			[]);

		// Act & Assert
		result.HasWarnings.ShouldBeFalse();
	}

	#endregion

	#region Errors Property Tests

	[Fact]
	public void Errors_ReturnsOnlyErrors()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Info, "Info"),
			new ValidationIssue(ValidationSeverity.Warning, "Warning"),
			new ValidationIssue(ValidationSeverity.Error, "Error 1"),
			new ValidationIssue(ValidationSeverity.Error, "Error 2"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act
		var errors = result.Errors.ToList();

		// Assert
		errors.Count.ShouldBe(2);
		errors.ShouldAllBe(e => e.Severity == ValidationSeverity.Error);
	}

	[Fact]
	public void Errors_WithNoErrors_ReturnsEmpty()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Warning, "Warning"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act
		var errors = result.Errors.ToList();

		// Assert
		errors.ShouldBeEmpty();
	}

	#endregion

	#region Warnings Property Tests

	[Fact]
	public void Warnings_ReturnsOnlyWarnings()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Info, "Info"),
			new ValidationIssue(ValidationSeverity.Warning, "Warning 1"),
			new ValidationIssue(ValidationSeverity.Warning, "Warning 2"),
			new ValidationIssue(ValidationSeverity.Error, "Error"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act
		var warnings = result.Warnings.ToList();

		// Assert
		warnings.Count.ShouldBe(2);
		warnings.ShouldAllBe(w => w.Severity == ValidationSeverity.Warning);
	}

	[Fact]
	public void Warnings_WithNoWarnings_ReturnsEmpty()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Error, "Error"),
		};
		var result = new SynthesisResult(
			new Dictionary<string, IPipelineProfile>(),
			new Dictionary<MessageKinds, string>(),
			issues);

		// Act
		var warnings = result.Warnings.ToList();

		// Assert
		warnings.ShouldBeEmpty();
	}

	#endregion

	#region Full Integration Tests

	[Fact]
	public void FullSynthesisResult_WithAllProperties_WorksCorrectly()
	{
		// Arrange
		var profile = A.Fake<IPipelineProfile>();
		_ = A.CallTo(() => profile.Name).Returns("DefaultProfile");
		_ = A.CallTo(() => profile.Description).Returns("Default pipeline profile");

		var profiles = new Dictionary<string, IPipelineProfile>
		{
			["DefaultProfile"] = profile,
		};

		var mappings = new Dictionary<MessageKinds, string>
		{
			[MessageKinds.Action] = "DefaultProfile",
			[MessageKinds.None] = "DefaultProfile",
			[MessageKinds.Event] = "DefaultProfile",
		};

		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Warning, "No dedicated command profile"),
			new ValidationIssue(ValidationSeverity.Info, "Using default profile for all message kinds"),
		};

		// Act
		var result = new SynthesisResult(profiles, mappings, issues);

		// Assert
		result.Profiles.Count.ShouldBe(1);
		result.Mappings.Count.ShouldBe(3);
		result.ValidationIssues.Length.ShouldBe(2);
		result.HasErrors.ShouldBeFalse();
		result.HasWarnings.ShouldBeTrue();
		result.Errors.Count().ShouldBe(0);
		result.Warnings.Count().ShouldBe(1);
	}

	#endregion
}
