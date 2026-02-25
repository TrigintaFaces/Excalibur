// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sanitization;

/// <summary>
/// Unit tests for <see cref="TelemetrySanitizerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TelemetrySanitizerOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedValues()
	{
		// Arrange & Act
		var options = new TelemetrySanitizerOptions();

		// Assert
		options.IncludeRawPii.ShouldBeFalse();
		options.SensitiveTagNames.ShouldNotBeNull();
		options.SensitiveTagNames.ShouldNotBeEmpty();
		options.SuppressedTagNames.ShouldNotBeNull();
		options.SuppressedTagNames.ShouldNotBeEmpty();
	}

	[Fact]
	public void SensitiveTagNames_ContainsCommonPiiTags()
	{
		// Arrange & Act
		var options = new TelemetrySanitizerOptions();

		// Assert
		options.SensitiveTagNames.ShouldContain("user.id");
		options.SensitiveTagNames.ShouldContain("user.name");
		options.SensitiveTagNames.ShouldContain("auth.user_id");
		options.SensitiveTagNames.ShouldContain("tenant.id");
	}

	[Fact]
	public void SuppressedTagNames_ContainsHighlySensitiveTags()
	{
		// Arrange & Act
		var options = new TelemetrySanitizerOptions();

		// Assert
		options.SuppressedTagNames.ShouldContain("auth.email");
		options.SuppressedTagNames.ShouldContain("auth.token");
	}

	[Fact]
	public void IncludeRawPii_CanBeEnabled()
	{
		// Arrange
		var options = new TelemetrySanitizerOptions { IncludeRawPii = true };

		// Assert
		options.IncludeRawPii.ShouldBeTrue();
	}

	[Fact]
	public void SensitiveTagNames_CanBeCustomized()
	{
		// Arrange
		var options = new TelemetrySanitizerOptions
		{
			SensitiveTagNames = ["custom.field1", "custom.field2"],
		};

		// Assert
		options.SensitiveTagNames.Count.ShouldBe(2);
		options.SensitiveTagNames.ShouldContain("custom.field1");
	}

	[Fact]
	public void SuppressedTagNames_CanBeCustomized()
	{
		// Arrange
		var options = new TelemetrySanitizerOptions
		{
			SuppressedTagNames = ["secret.key"],
		};

		// Assert
		options.SuppressedTagNames.Count.ShouldBe(1);
		options.SuppressedTagNames.ShouldContain("secret.key");
	}
}
