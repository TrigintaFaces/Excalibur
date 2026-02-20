// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

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
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new TimeoutOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
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

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
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
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMinutes(1),
			ThrowOnTimeout = false,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.ThrowOnTimeout.ShouldBeFalse();
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void DefaultTimeout_CanBeZero()
	{
		// Arrange
		var options = new TimeoutOptions();

		// Act
		options.DefaultTimeout = TimeSpan.Zero;

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void DefaultTimeout_CanBeNegative()
	{
		// Note: Negative values represent infinite timeout in some contexts
		var options = new TimeoutOptions();

		// Act
		options.DefaultTimeout = TimeSpan.FromMilliseconds(-1);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMilliseconds(-1));
	}

	#endregion
}
