// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Transactions;

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="TransactionAttribute"/>.
/// </summary>
/// <remarks>
/// Tests the transaction configuration attribute.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class TransactionAttributeShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var attribute = new TransactionAttribute();

		// Assert
		_ = attribute.ShouldNotBeNull();
		attribute.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		attribute.TimeoutSeconds.ShouldBe(30);
	}

	#endregion

	#region IsolationLevel Property Tests

	[Theory]
	[InlineData(IsolationLevel.ReadUncommitted)]
	[InlineData(IsolationLevel.ReadCommitted)]
	[InlineData(IsolationLevel.RepeatableRead)]
	[InlineData(IsolationLevel.Serializable)]
	[InlineData(IsolationLevel.Snapshot)]
	[InlineData(IsolationLevel.Chaos)]
	[InlineData(IsolationLevel.Unspecified)]
	public void IsolationLevel_CanBeSetToVariousLevels(IsolationLevel level)
	{
		// Arrange
		var attribute = new TransactionAttribute();

		// Act
		attribute.IsolationLevel = level;

		// Assert
		attribute.IsolationLevel.ShouldBe(level);
	}

	[Fact]
	public void IsolationLevel_DefaultsToReadCommitted()
	{
		// Arrange & Act
		var attribute = new TransactionAttribute();

		// Assert
		attribute.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
	}

	#endregion

	#region TimeoutSeconds Property Tests

	[Fact]
	public void TimeoutSeconds_CanBeSet()
	{
		// Arrange
		var attribute = new TransactionAttribute();

		// Act
		attribute.TimeoutSeconds = 60;

		// Assert
		attribute.TimeoutSeconds.ShouldBe(60);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(300)]
	[InlineData(int.MaxValue)]
	public void TimeoutSeconds_WithVariousValues_Works(int timeout)
	{
		// Arrange
		var attribute = new TransactionAttribute();

		// Act
		attribute.TimeoutSeconds = timeout;

		// Assert
		attribute.TimeoutSeconds.ShouldBe(timeout);
	}

	[Fact]
	public void TimeoutSeconds_DefaultsTo30()
	{
		// Arrange & Act
		var attribute = new TransactionAttribute();

		// Assert
		attribute.TimeoutSeconds.ShouldBe(30);
	}

	#endregion

	#region Attribute Usage Tests

	[Fact]
	public void AttributeUsage_TargetsClasses()
	{
		// Arrange & Act
		var attributeUsage = typeof(TransactionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault() as AttributeUsageAttribute;

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	#endregion

	#region Property Initializer Tests

	[Fact]
	public void AllProperties_CanBeSetViaObjectInitializer()
	{
		// Arrange & Act
		var attribute = new TransactionAttribute
		{
			IsolationLevel = IsolationLevel.Serializable,
			TimeoutSeconds = 120,
		};

		// Assert
		attribute.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
		attribute.TimeoutSeconds.ShouldBe(120);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void FastOperation_Configuration()
	{
		// Arrange & Act - Fast operation with short timeout
		var attribute = new TransactionAttribute
		{
			IsolationLevel = IsolationLevel.ReadUncommitted,
			TimeoutSeconds = 5,
		};

		// Assert
		attribute.IsolationLevel.ShouldBe(IsolationLevel.ReadUncommitted);
		attribute.TimeoutSeconds.ShouldBe(5);
	}

	[Fact]
	public void CriticalOperation_Configuration()
	{
		// Arrange & Act - Critical operation requiring strong isolation
		var attribute = new TransactionAttribute
		{
			IsolationLevel = IsolationLevel.Serializable,
			TimeoutSeconds = 300,
		};

		// Assert
		attribute.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
		attribute.TimeoutSeconds.ShouldBe(300);
	}

	[Fact]
	public void ReadOnlyOperation_Configuration()
	{
		// Arrange & Act - Read-only operation with snapshot isolation
		var attribute = new TransactionAttribute
		{
			IsolationLevel = IsolationLevel.Snapshot,
			TimeoutSeconds = 60,
		};

		// Assert
		attribute.IsolationLevel.ShouldBe(IsolationLevel.Snapshot);
	}

	#endregion
}
