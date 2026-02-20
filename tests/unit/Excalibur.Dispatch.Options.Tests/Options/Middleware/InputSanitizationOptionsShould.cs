// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="InputSanitizationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class InputSanitizationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_Features_PreventXss_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Features.PreventXss.ShouldBeTrue();
	}

	[Fact]
	public void Default_Features_RemoveHtmlTags_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Features.RemoveHtmlTags.ShouldBeTrue();
	}

	[Fact]
	public void Default_Features_PreventSqlInjection_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Features.PreventSqlInjection.ShouldBeTrue();
	}

	[Fact]
	public void Default_Features_PreventPathTraversal_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Features.PreventPathTraversal.ShouldBeTrue();
	}

	[Fact]
	public void Default_Features_RemoveNullBytes_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Features.RemoveNullBytes.ShouldBeTrue();
	}

	[Fact]
	public void Default_Features_NormalizeUnicode_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Features.NormalizeUnicode.ShouldBeTrue();
	}

	[Fact]
	public void Default_Features_TrimWhitespace_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Features.TrimWhitespace.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxStringLength_IsZero()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.MaxStringLength.ShouldBe(0);
	}

	[Fact]
	public void Default_SanitizeContextItems_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.SanitizeContextItems.ShouldBeTrue();
	}

	[Fact]
	public void Default_UseCustomSanitization_IsTrue()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.UseCustomSanitization.ShouldBeTrue();
	}

	[Fact]
	public void Default_ThrowOnSanitizationError_IsFalse()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.ThrowOnSanitizationError.ShouldBeFalse();
	}

	[Fact]
	public void Default_BypassSanitizationForTypes_IsNull()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.BypassSanitizationForTypes.ShouldBeNull();
	}

	[Fact]
	public void Default_ExcludeProperties_IsNull()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.ExcludeProperties.ShouldBeNull();
	}

	[Fact]
	public void Default_Features_IsNotNull()
	{
		// Arrange & Act
		var options = new InputSanitizationOptions();

		// Assert
		options.Features.ShouldNotBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new InputSanitizationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Features_PreventXss_CanBeSet()
	{
		// Arrange
		var options = new InputSanitizationOptions();

		// Act
		options.Features.PreventXss = false;

		// Assert
		options.Features.PreventXss.ShouldBeFalse();
	}

	[Fact]
	public void MaxStringLength_CanBeSet()
	{
		// Arrange
		var options = new InputSanitizationOptions();

		// Act
		options.MaxStringLength = 1000;

		// Assert
		options.MaxStringLength.ShouldBe(1000);
	}

	[Fact]
	public void BypassSanitizationForTypes_CanBeSet()
	{
		// Arrange
		var options = new InputSanitizationOptions();

		// Act
		options.BypassSanitizationForTypes = ["InternalCommand", "SystemEvent"];

		// Assert
		_ = options.BypassSanitizationForTypes.ShouldNotBeNull();
		options.BypassSanitizationForTypes.Length.ShouldBe(2);
	}

	[Fact]
	public void ExcludeProperties_CanBeSet()
	{
		// Arrange
		var options = new InputSanitizationOptions();

		// Act
		options.ExcludeProperties = ["Password", "Token"];

		// Assert
		_ = options.ExcludeProperties.ShouldNotBeNull();
		options.ExcludeProperties.Length.ShouldBe(2);
	}

	[Fact]
	public void Features_CanBeReplaced()
	{
		// Arrange
		var options = new InputSanitizationOptions();

		// Act
		options.Features = new SanitizationFeatures { PreventXss = false };

		// Assert
		options.Features.PreventXss.ShouldBeFalse();
		options.Features.PreventSqlInjection.ShouldBeTrue(); // default
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new InputSanitizationOptions
		{
			Enabled = false,
			Features = new SanitizationFeatures
			{
				PreventXss = false,
				RemoveHtmlTags = false,
				PreventSqlInjection = false,
				PreventPathTraversal = false,
				RemoveNullBytes = false,
				NormalizeUnicode = false,
				TrimWhitespace = false,
			},
			MaxStringLength = 500,
			SanitizeContextItems = false,
			UseCustomSanitization = false,
			ThrowOnSanitizationError = true,
			BypassSanitizationForTypes = ["Test"],
			ExcludeProperties = ["Secret"],
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.Features.PreventXss.ShouldBeFalse();
		options.MaxStringLength.ShouldBe(500);
		options.ThrowOnSanitizationError.ShouldBeTrue();
		_ = options.BypassSanitizationForTypes.ShouldNotBeNull();
		_ = options.ExcludeProperties.ShouldNotBeNull();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighSecurity_HasAllProtectionsEnabled()
	{
		// Act
		var options = new InputSanitizationOptions
		{
			Features = new SanitizationFeatures
			{
				PreventXss = true,
				PreventSqlInjection = true,
				PreventPathTraversal = true,
				RemoveNullBytes = true,
			},
			MaxStringLength = 10000,
			ThrowOnSanitizationError = true,
		};

		// Assert
		options.Features.PreventXss.ShouldBeTrue();
		options.Features.PreventSqlInjection.ShouldBeTrue();
		options.ThrowOnSanitizationError.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForInternalServices_HasRelaxedSettings()
	{
		// Act
		var options = new InputSanitizationOptions
		{
			Enabled = true,
			Features = new SanitizationFeatures
			{
				PreventXss = false,
				PreventSqlInjection = false,
			},
			MaxStringLength = 0,
		};

		// Assert
		options.Features.PreventXss.ShouldBeFalse();
		options.Features.PreventSqlInjection.ShouldBeFalse();
		options.MaxStringLength.ShouldBe(0);
	}

	[Fact]
	public void Options_ForHtmlContent_AllowsHtmlTags()
	{
		// Act
		var options = new InputSanitizationOptions
		{
			Features = new SanitizationFeatures
			{
				RemoveHtmlTags = false,
				PreventXss = true, // Still prevent XSS
			},
		};

		// Assert
		options.Features.RemoveHtmlTags.ShouldBeFalse();
		options.Features.PreventXss.ShouldBeTrue();
	}

	#endregion
}
