// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="TracingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class TracingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new TracingOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_SamplingRatio_IsOne()
	{
		// Arrange & Act
		var options = new TracingOptions();

		// Assert
		options.SamplingRatio.ShouldBe(1.0);
	}

	[Fact]
	public void Default_IncludeSensitiveData_IsFalse()
	{
		// Arrange & Act
		var options = new TracingOptions();

		// Assert
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new TracingOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void SamplingRatio_CanBeSet()
	{
		// Arrange
		var options = new TracingOptions();

		// Act
		options.SamplingRatio = 0.5;

		// Assert
		options.SamplingRatio.ShouldBe(0.5);
	}

	[Fact]
	public void IncludeSensitiveData_CanBeSet()
	{
		// Arrange
		var options = new TracingOptions();

		// Act
		options.IncludeSensitiveData = true;

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new TracingOptions
		{
			Enabled = true,
			SamplingRatio = 0.25,
			IncludeSensitiveData = true,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.SamplingRatio.ShouldBe(0.25);
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void SamplingRatio_CanBeZero()
	{
		// Arrange
		var options = new TracingOptions();

		// Act
		options.SamplingRatio = 0.0;

		// Assert
		options.SamplingRatio.ShouldBe(0.0);
	}

	[Fact]
	public void SamplingRatio_CanExceedOne()
	{
		// Note: Values > 1 might not be semantically valid, but the type allows them
		var options = new TracingOptions();

		// Act
		options.SamplingRatio = 2.0;

		// Assert
		options.SamplingRatio.ShouldBe(2.0);
	}

	#endregion
}
