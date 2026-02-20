// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="TimeoutManager"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class TimeoutManagerShould : UnitTestBase
{
	private static TimeoutManager CreateManager(TimeoutManagerOptions? options = null)
	{
		var opts = options ?? new TimeoutManagerOptions();
		return new TimeoutManager(
			Microsoft.Extensions.Options.Options.Create(opts),
			NullLogger<TimeoutManager>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new TimeoutManager(null!, NullLogger<TimeoutManager>.Instance));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new TimeoutManager(Microsoft.Extensions.Options.Options.Create(new TimeoutManagerOptions()), null!));
	}

	[Fact]
	public void Constructor_WithDefaultOptions_RegistersWellKnownTimeouts()
	{
		// Arrange & Act
		var manager = CreateManager();

		// Assert - well-known timeouts should be registered
		manager.GetTimeout("Database.Query").ShouldBe(TimeSpan.FromSeconds(15));
		manager.GetTimeout("Database.Command").ShouldBe(TimeSpan.FromSeconds(15));
		manager.GetTimeout("Database.Transaction").ShouldBe(TimeSpan.FromSeconds(30));
		manager.GetTimeout("Http.Get").ShouldBe(TimeSpan.FromSeconds(100));
		manager.GetTimeout("Http.Post").ShouldBe(TimeSpan.FromSeconds(100));
		manager.GetTimeout("Queue.Send").ShouldBe(TimeSpan.FromSeconds(60));
		manager.GetTimeout("Cache.Get").ShouldBe(TimeSpan.FromSeconds(5));
	}

	#endregion

	#region DefaultTimeout Tests

	[Fact]
	public void DefaultTimeout_ShouldReturnConfiguredDefault()
	{
		// Arrange
		var options = new TimeoutManagerOptions { DefaultTimeout = TimeSpan.FromSeconds(45) };
		var manager = CreateManager(options);

		// Assert
		manager.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void DefaultTimeout_ShouldBeThirtySecondsWithDefaults()
	{
		// Arrange & Act
		var manager = CreateManager();

		// Assert
		manager.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region GetTimeout Tests

	[Fact]
	public void GetTimeout_WithNullOperationName_ThrowsException()
	{
		// Arrange
		var manager = CreateManager();

		// Act & Assert
		Should.Throw<ArgumentException>(() => manager.GetTimeout(null!));
	}

	[Fact]
	public void GetTimeout_WithEmptyOperationName_ThrowsException()
	{
		// Arrange
		var manager = CreateManager();

		// Act & Assert
		Should.Throw<ArgumentException>(() => manager.GetTimeout(string.Empty));
	}

	[Fact]
	public void GetTimeout_WithWhitespaceOperationName_ThrowsException()
	{
		// Arrange
		var manager = CreateManager();

		// Act & Assert
		Should.Throw<ArgumentException>(() => manager.GetTimeout("   "));
	}

	[Fact]
	public void GetTimeout_WithCustomTimeout_ReturnsCustomValue()
	{
		// Arrange
		var manager = CreateManager();
		manager.RegisterTimeout("MyOperation", TimeSpan.FromSeconds(42));

		// Act
		var timeout = manager.GetTimeout("MyOperation");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(42));
	}

	[Fact]
	public void GetTimeout_WithDatabaseDotPattern_ReturnsDatabaseTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { DatabaseTimeout = TimeSpan.FromSeconds(20) };
		var manager = CreateManager(options);

		// Act - "Database.CustomOp" matches StartsWith("Database.")
		var timeout = manager.GetTimeout("Database.CustomOp");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void GetTimeout_WithQueryPattern_ReturnsDatabaseTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { DatabaseTimeout = TimeSpan.FromSeconds(20) };
		var manager = CreateManager(options);

		// Act - "RunQueryBatch" matches Contains("Query")
		var timeout = manager.GetTimeout("RunQueryBatch");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void GetTimeout_WithSqlPattern_ReturnsDatabaseTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { DatabaseTimeout = TimeSpan.FromSeconds(20) };
		var manager = CreateManager(options);

		// Act - "MySqlOperation" matches Contains("Sql")
		var timeout = manager.GetTimeout("MySqlOperation");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void GetTimeout_WithHttpDotPattern_ReturnsHttpTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { HttpTimeout = TimeSpan.FromSeconds(50) };
		var manager = CreateManager(options);

		// Act - "Http.Custom" matches StartsWith("Http.")
		var timeout = manager.GetTimeout("Http.Custom");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(50));
	}

	[Fact]
	public void GetTimeout_WithApiPattern_ReturnsHttpTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { HttpTimeout = TimeSpan.FromSeconds(50) };
		var manager = CreateManager(options);

		// Act - "CallExternalApi" matches Contains("Api")
		var timeout = manager.GetTimeout("CallExternalApi");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(50));
	}

	[Fact]
	public void GetTimeout_WithRestPattern_ReturnsHttpTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { HttpTimeout = TimeSpan.FromSeconds(50) };
		var manager = CreateManager(options);

		// Act - "InvokeRestEndpoint" matches Contains("Rest")
		var timeout = manager.GetTimeout("InvokeRestEndpoint");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(50));
	}

	[Fact]
	public void GetTimeout_WithQueueDotPattern_ReturnsMessageQueueTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { MessageQueueTimeout = TimeSpan.FromSeconds(90) };
		var manager = CreateManager(options);

		// Act - "Queue.Custom" matches StartsWith("Queue.")
		var timeout = manager.GetTimeout("Queue.Custom");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(90));
	}

	[Fact]
	public void GetTimeout_WithMessagePattern_ReturnsMessageQueueTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { MessageQueueTimeout = TimeSpan.FromSeconds(90) };
		var manager = CreateManager(options);

		// Act - "SendMessage" matches Contains("Message")
		var timeout = manager.GetTimeout("SendMessage");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(90));
	}

	[Fact]
	public void GetTimeout_WithBusPattern_ReturnsMessageQueueTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { MessageQueueTimeout = TimeSpan.FromSeconds(90) };
		var manager = CreateManager(options);

		// Act - "PublishToBus" matches Contains("Bus")
		var timeout = manager.GetTimeout("PublishToBus");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(90));
	}

	[Fact]
	public void GetTimeout_WithCacheDotPattern_ReturnsCacheTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { CacheTimeout = TimeSpan.FromSeconds(3) };
		var manager = CreateManager(options);

		// Act - "Cache.Custom" matches StartsWith("Cache.")
		var timeout = manager.GetTimeout("Cache.Custom");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void GetTimeout_WithRedisPattern_ReturnsCacheTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { CacheTimeout = TimeSpan.FromSeconds(3) };
		var manager = CreateManager(options);

		// Act - "ReadFromRedis" matches Contains("Redis")
		var timeout = manager.GetTimeout("ReadFromRedis");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void GetTimeout_WithMemoryPattern_ReturnsCacheTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { CacheTimeout = TimeSpan.FromSeconds(3) };
		var manager = CreateManager(options);

		// Act - "InMemoryLookup" matches Contains("Memory")
		var timeout = manager.GetTimeout("InMemoryLookup");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void GetTimeout_WithUnknownPattern_ReturnsDefaultTimeout()
	{
		// Arrange
		var options = new TimeoutManagerOptions { DefaultTimeout = TimeSpan.FromSeconds(25) };
		var manager = CreateManager(options);

		// Act
		var timeout = manager.GetTimeout("UnknownOperation");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(25));
	}

	#endregion

	#region RegisterTimeout Tests

	[Fact]
	public void RegisterTimeout_WithNullName_ThrowsException()
	{
		// Arrange
		var manager = CreateManager();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => manager.RegisterTimeout(null!, TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public void RegisterTimeout_WithZeroTimeout_ThrowsException()
	{
		// Arrange
		var manager = CreateManager();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(
			() => manager.RegisterTimeout("Op", TimeSpan.Zero));
	}

	[Fact]
	public void RegisterTimeout_WithNegativeTimeout_ThrowsException()
	{
		// Arrange
		var manager = CreateManager();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(
			() => manager.RegisterTimeout("Op", TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void RegisterTimeout_OverridesExistingTimeout()
	{
		// Arrange
		var manager = CreateManager();
		manager.RegisterTimeout("MyOp", TimeSpan.FromSeconds(10));

		// Act
		manager.RegisterTimeout("MyOp", TimeSpan.FromSeconds(20));

		// Assert
		manager.GetTimeout("MyOp").ShouldBe(TimeSpan.FromSeconds(20));
	}

	#endregion

	#region IsSlowOperation Tests

	[Fact]
	public void IsSlowOperation_BelowThreshold_ReturnsFalse()
	{
		// Arrange
		var options = new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(10),
			SlowOperationThreshold = 0.8,
		};
		var manager = CreateManager(options);

		// Act
		var isSlow = manager.IsSlowOperation("UnknownOp", TimeSpan.FromSeconds(5));

		// Assert
		isSlow.ShouldBeFalse();
	}

	[Fact]
	public void IsSlowOperation_AtThreshold_ReturnsTrue()
	{
		// Arrange
		var options = new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(10),
			SlowOperationThreshold = 0.8,
		};
		var manager = CreateManager(options);

		// Act
		var isSlow = manager.IsSlowOperation("UnknownOp", TimeSpan.FromSeconds(8));

		// Assert
		isSlow.ShouldBeTrue();
	}

	[Fact]
	public void IsSlowOperation_AboveThreshold_ReturnsTrue()
	{
		// Arrange
		var options = new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(10),
			SlowOperationThreshold = 0.8,
		};
		var manager = CreateManager(options);

		// Act
		var isSlow = manager.IsSlowOperation("UnknownOp", TimeSpan.FromSeconds(9));

		// Assert
		isSlow.ShouldBeTrue();
	}

	[Fact]
	public void IsSlowOperation_WithLoggingDisabled_StillReturnsTrue()
	{
		// Arrange
		var options = new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(10),
			SlowOperationThreshold = 0.8,
			LogTimeoutWarnings = false,
		};
		var manager = CreateManager(options);

		// Act
		var isSlow = manager.IsSlowOperation("UnknownOp", TimeSpan.FromSeconds(9));

		// Assert
		isSlow.ShouldBeTrue();
	}

	#endregion
}
