// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Core;

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
	public void Default_MessageTypeTimeouts_IsEmpty()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		_ = options.MessageTypeTimeouts.ShouldNotBeNull();
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
		options.DefaultTimeout = TimeSpan.FromMinutes(5);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(5));
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

	#endregion

	#region MessageTypeTimeouts Tests

	[Fact]
	public void MessageTypeTimeouts_CanAddEntry()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.MessageTypeTimeouts.Add("MyMessage", TimeSpan.FromMinutes(2));

		// Assert
		options.MessageTypeTimeouts.Count.ShouldBe(1);
		options.MessageTypeTimeouts["MyMessage"].ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void MessageTypeTimeouts_CanAddMultipleEntries()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.MessageTypeTimeouts.Add("FastMessage", TimeSpan.FromSeconds(5));
		options.MessageTypeTimeouts.Add("SlowMessage", TimeSpan.FromMinutes(10));

		// Assert
		options.MessageTypeTimeouts.Count.ShouldBe(2);
	}

	[Fact]
	public void MessageTypeTimeouts_UsesOrdinalComparison()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.MessageTypeTimeouts.Add("MyMessage", TimeSpan.FromSeconds(10));
		options.MessageTypeTimeouts.Add("MYMESSAGE", TimeSpan.FromSeconds(20));

		// Assert - Should be treated as different keys (case-sensitive)
		options.MessageTypeTimeouts.Count.ShouldBe(2);
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
			ThrowOnTimeout = false,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.ThrowOnTimeout.ShouldBeFalse();
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_ThrowsWhenDefaultTimeoutIsZero()
	{
		// Arrange
		var options = new TimeoutOptions { DefaultTimeout = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsWhenDefaultTimeoutIsNegative()
	{
		// Arrange
		var options = new TimeoutOptions { DefaultTimeout = TimeSpan.FromMilliseconds(-1) };

		// Act & Assert
		Should.Throw<ArgumentException>(() => options.Validate());
	}

	[Fact]
	public void Validate_SucceedsWithValidDefaults()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act & Assert -- should not throw
		options.Validate();
	}

	#endregion
}
