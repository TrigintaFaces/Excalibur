// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="TimeoutOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class TimeoutOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_DefaultTimeout_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_ActionTimeout_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		options.ActionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_EventTimeout_IsTenSeconds()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		options.EventTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void Default_DocumentTimeout_IsSixtySeconds()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		options.DocumentTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void Default_MessageTypeTimeouts_IsNotNull()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		_ = options.MessageTypeTimeouts.ShouldNotBeNull();
	}

	[Fact]
	public void Default_MessageTypeTimeouts_IsEmpty()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		options.MessageTypeTimeouts.ShouldBeEmpty();
	}

	[Fact]
	public void Default_ThrowOnTimeout_IsTrue()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		options.ThrowOnTimeout.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void DefaultTimeout_CanBeSet()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.DefaultTimeout = TimeSpan.FromMinutes(2);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void ActionTimeout_CanBeSet()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.ActionTimeout = TimeSpan.FromMinutes(1);

		// Assert
		options.ActionTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void EventTimeout_CanBeSet()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.EventTimeout = TimeSpan.FromSeconds(5);

		// Assert
		options.EventTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void DocumentTimeout_CanBeSet()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.DocumentTimeout = TimeSpan.FromMinutes(5);

		// Assert
		options.DocumentTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void ThrowOnTimeout_CanBeSet()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.ThrowOnTimeout = false;

		// Assert
		options.ThrowOnTimeout.ShouldBeFalse();
	}

	[Fact]
	public void MessageTypeTimeouts_CanAddEntries()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.MessageTypeTimeouts["LongRunningCommand"] = TimeSpan.FromMinutes(10);
		options.MessageTypeTimeouts["QuickQuery"] = TimeSpan.FromSeconds(5);

		// Assert
		options.MessageTypeTimeouts.Count.ShouldBe(2);
		options.MessageTypeTimeouts["LongRunningCommand"].ShouldBe(TimeSpan.FromMinutes(10));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new TimeoutOptions
		{
			Enabled = false,
			DefaultTimeout = TimeSpan.FromMinutes(1),
			ActionTimeout = TimeSpan.FromMinutes(2),
			EventTimeout = TimeSpan.FromSeconds(15),
			DocumentTimeout = TimeSpan.FromMinutes(3),
			ThrowOnTimeout = false,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.ActionTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.EventTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.DocumentTimeout.ShouldBe(TimeSpan.FromMinutes(3));
		options.ThrowOnTimeout.ShouldBeFalse();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_WithValidOptions_DoesNotThrow()
	{
		// Arrange
		var options = new TimeoutOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(30),
			ActionTimeout = TimeSpan.FromSeconds(30),
			EventTimeout = TimeSpan.FromSeconds(10),
			DocumentTimeout = TimeSpan.FromSeconds(60),
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroDefaultTimeout_ThrowsArgumentException()
	{
		// Arrange
		var options = new TimeoutOptions
		{
			DefaultTimeout = TimeSpan.Zero,
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithNegativeActionTimeout_ThrowsArgumentException()
	{
		// Arrange
		var options = new TimeoutOptions
		{
			ActionTimeout = TimeSpan.FromSeconds(-1),
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithZeroMessageTypeTimeout_ThrowsArgumentException()
	{
		// Arrange
		var options = new TimeoutOptions();
		options.MessageTypeTimeouts["TestMessage"] = TimeSpan.Zero;

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => options.Validate());
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForFastProcessing_HasShortTimeouts()
	{
		// Act
		var options = new TimeoutOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(5),
			ActionTimeout = TimeSpan.FromSeconds(5),
			EventTimeout = TimeSpan.FromSeconds(2),
			DocumentTimeout = TimeSpan.FromSeconds(10),
		};

		// Assert
		options.DefaultTimeout.ShouldBeLessThan(TimeSpan.FromSeconds(30));
		options.EventTimeout.ShouldBeLessThan(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void Options_ForLongRunningOperations_HasLongTimeouts()
	{
		// Act
		var options = new TimeoutOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(5),
			ActionTimeout = TimeSpan.FromMinutes(10),
			DocumentTimeout = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.DefaultTimeout.ShouldBeGreaterThan(TimeSpan.FromSeconds(30));
		options.DocumentTimeout.ShouldBeGreaterThan(TimeSpan.FromMinutes(1));
	}

	#endregion
}
