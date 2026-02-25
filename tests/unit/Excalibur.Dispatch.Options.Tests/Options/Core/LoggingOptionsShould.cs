// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="LoggingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class LoggingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_EnhancedLogging_IsFalse()
	{
		// Arrange & Act
		var options = new LoggingOptions();

		// Assert
		options.EnhancedLogging.ShouldBeFalse();
	}

	[Fact]
	public void Default_IncludeCorrelationIds_IsTrue()
	{
		// Arrange & Act
		var options = new LoggingOptions();

		// Assert
		options.IncludeCorrelationIds.ShouldBeTrue();
	}

	[Fact]
	public void Default_IncludeExecutionContext_IsTrue()
	{
		// Arrange & Act
		var options = new LoggingOptions();

		// Assert
		options.IncludeExecutionContext.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void EnhancedLogging_CanBeSet()
	{
		// Arrange
		var options = new LoggingOptions();

		// Act
		options.EnhancedLogging = true;

		// Assert
		options.EnhancedLogging.ShouldBeTrue();
	}

	[Fact]
	public void IncludeCorrelationIds_CanBeSet()
	{
		// Arrange
		var options = new LoggingOptions();

		// Act
		options.IncludeCorrelationIds = false;

		// Assert
		options.IncludeCorrelationIds.ShouldBeFalse();
	}

	[Fact]
	public void IncludeExecutionContext_CanBeSet()
	{
		// Arrange
		var options = new LoggingOptions();

		// Act
		options.IncludeExecutionContext = false;

		// Assert
		options.IncludeExecutionContext.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new LoggingOptions
		{
			EnhancedLogging = true,
			IncludeCorrelationIds = false,
			IncludeExecutionContext = false,
		};

		// Assert
		options.EnhancedLogging.ShouldBeTrue();
		options.IncludeCorrelationIds.ShouldBeFalse();
		options.IncludeExecutionContext.ShouldBeFalse();
	}

	#endregion
}
