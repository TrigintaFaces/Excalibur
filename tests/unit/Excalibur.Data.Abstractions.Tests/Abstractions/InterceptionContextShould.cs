// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="InterceptionContext"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "Interception")]
public sealed class InterceptionContextShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_InitializesPropertiesDictionary()
	{
		// Act
		var context = new InterceptionContext();

		// Assert
		context.Properties.ShouldNotBeNull();
		context.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void Create_SetsStartTime_ToApproximatelyNow()
	{
		// Arrange
		var before = DateTime.UtcNow;

		// Act
		var context = new InterceptionContext();

		// Assert
		var after = DateTime.UtcNow;
		context.StartTime.ShouldBeGreaterThanOrEqualTo(before);
		context.StartTime.ShouldBeLessThanOrEqualTo(after);
	}

	#endregion

	#region ProviderName Tests

	[Fact]
	public void ProviderName_InitiallyNull()
	{
		// Act
		var context = new InterceptionContext();

		// Assert
		context.ProviderName.ShouldBeNull();
	}

	[Fact]
	public void ProviderName_CanBeSet()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		context.ProviderName = "SqlServer";

		// Assert
		context.ProviderName.ShouldBe("SqlServer");
	}

	#endregion

	#region OperationType Tests

	[Fact]
	public void OperationType_InitiallyNull()
	{
		// Act
		var context = new InterceptionContext();

		// Assert
		context.OperationType.ShouldBeNull();
	}

	[Fact]
	public void OperationType_CanBeSet()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		context.OperationType = "Query";

		// Assert
		context.OperationType.ShouldBe("Query");
	}

	#endregion

	#region StartTime Tests

	[Fact]
	public void StartTime_IsReadOnly()
	{
		// Arrange
		var context = new InterceptionContext();
		var initialStartTime = context.StartTime;

		// Act - create new context to get different start time
		Thread.Sleep(10);
		var context2 = new InterceptionContext();

		// Assert
		context.StartTime.ShouldBe(initialStartTime);
		context2.StartTime.ShouldBeGreaterThan(initialStartTime);
	}

	#endregion

	#region ElapsedTime Tests

	[Fact]
	public void ElapsedTime_ReturnsPositiveTimeSpan()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		Thread.Sleep(10);
		var elapsed = context.ElapsedTime;

		// Assert
		elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(5));
	}

	[Fact]
	public void ElapsedTime_Increases_OverTime()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		var elapsed1 = context.ElapsedTime;
		Thread.Sleep(20);
		var elapsed2 = context.ElapsedTime;

		// Assert
		elapsed2.ShouldBeGreaterThan(elapsed1);
	}

	#endregion

	#region ShouldCache Tests

	[Fact]
	public void ShouldCache_InitiallyFalse()
	{
		// Act
		var context = new InterceptionContext();

		// Assert
		context.ShouldCache.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCache_CanBeSetToTrue()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		context.ShouldCache = true;

		// Assert
		context.ShouldCache.ShouldBeTrue();
	}

	#endregion

	#region CacheKey Tests

	[Fact]
	public void CacheKey_InitiallyNull()
	{
		// Act
		var context = new InterceptionContext();

		// Assert
		context.CacheKey.ShouldBeNull();
	}

	[Fact]
	public void CacheKey_CanBeSet()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		context.CacheKey = "customer:123";

		// Assert
		context.CacheKey.ShouldBe("customer:123");
	}

	#endregion

	#region CacheDuration Tests

	[Fact]
	public void CacheDuration_InitiallyNull()
	{
		// Act
		var context = new InterceptionContext();

		// Assert
		context.CacheDuration.ShouldBeNull();
	}

	[Fact]
	public void CacheDuration_CanBeSet()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		context.CacheDuration = TimeSpan.FromMinutes(5);

		// Assert
		context.CacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region Properties Tests

	[Fact]
	public void Properties_CanAddItems()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		context.Properties["key1"] = "value1";
		context.Properties["key2"] = 42;

		// Assert
		context.Properties.Count.ShouldBe(2);
		context.Properties["key1"].ShouldBe("value1");
		context.Properties["key2"].ShouldBe(42);
	}

	[Fact]
	public void Properties_UsesOrdinalComparison()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		context.Properties["Key"] = "value1";
		context.Properties["key"] = "value2";

		// Assert - ordinal comparison means these are different keys
		context.Properties.Count.ShouldBe(2);
	}

	#endregion

	#region CorrelationId Tests

	[Fact]
	public void CorrelationId_InitiallyNull()
	{
		// Act
		var context = new InterceptionContext();

		// Assert
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CorrelationId_CanBeSet()
	{
		// Arrange
		var context = new InterceptionContext();
		var correlationId = Guid.NewGuid().ToString();

		// Act
		context.CorrelationId = correlationId;

		// Assert
		context.CorrelationId.ShouldBe(correlationId);
	}

	#endregion

	#region UserIdentity Tests

	[Fact]
	public void UserIdentity_InitiallyNull()
	{
		// Act
		var context = new InterceptionContext();

		// Assert
		context.UserIdentity.ShouldBeNull();
	}

	[Fact]
	public void UserIdentity_CanBeSet()
	{
		// Arrange
		var context = new InterceptionContext();

		// Act
		context.UserIdentity = "user@example.com";

		// Assert
		context.UserIdentity.ShouldBe("user@example.com");
	}

	#endregion
}
