// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="TimeoutManager"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class TimeoutManagerShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var logger = A.Fake<ILogger<TimeoutManager>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new TimeoutManager(null!, logger));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new TimeoutManager(options, null!));
	}

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();

		// Act
		var manager = new TimeoutManager(options, logger);

		// Assert
		_ = manager.ShouldNotBeNull();
	}

	#endregion

	#region DefaultTimeout Tests

	[Fact]
	public void DefaultTimeout_ReturnsConfiguredValue()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { DefaultTimeout = TimeSpan.FromSeconds(45) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act & Assert
		manager.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(45));
	}

	#endregion

	#region GetTimeout Tests

	[Fact]
	public void GetTimeout_WithNullOperationName_ThrowsArgumentException()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => manager.GetTimeout(null!));
	}

	[Fact]
	public void GetTimeout_WithEmptyOperationName_ThrowsArgumentException()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => manager.GetTimeout(string.Empty));
	}

	[Fact]
	public void GetTimeout_WithWhitespaceOperationName_ThrowsArgumentException()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => manager.GetTimeout("   "));
	}

	[Fact]
	public void GetTimeout_ForDatabaseQuery_ReturnsDatabaseTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { DatabaseTimeout = TimeSpan.FromSeconds(20) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("Database.Query");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void GetTimeout_ForHttpGet_ReturnsHttpTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { HttpTimeout = TimeSpan.FromSeconds(120) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("Http.Get");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(120));
	}

	[Fact]
	public void GetTimeout_ForQueueSend_ReturnsMessageQueueTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { MessageQueueTimeout = TimeSpan.FromSeconds(90) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("Queue.Send");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(90));
	}

	[Fact]
	public void GetTimeout_ForCacheGet_ReturnsCacheTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { CacheTimeout = TimeSpan.FromSeconds(3) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("Cache.Get");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void GetTimeout_ForUnknownOperation_ReturnsDefaultTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { DefaultTimeout = TimeSpan.FromSeconds(30) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("SomeUnknownOperation");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void GetTimeout_ForCustomRegisteredOperation_ReturnsCustomTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);
		manager.RegisterTimeout("CustomOperation", TimeSpan.FromSeconds(99));

		// Act
		var timeout = manager.GetTimeout("CustomOperation");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(99));
	}

	[Fact]
	public void GetTimeout_ForSqlPattern_ReturnsDatabaseTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { DatabaseTimeout = TimeSpan.FromSeconds(15) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("ExecuteSqlCommand");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void GetTimeout_ForApiPattern_ReturnsHttpTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { HttpTimeout = TimeSpan.FromSeconds(100) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("CallExternalApi");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(100));
	}

	[Fact]
	public void GetTimeout_ForMessagePattern_ReturnsMessageQueueTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { MessageQueueTimeout = TimeSpan.FromSeconds(60) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("ProcessMessage");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void GetTimeout_ForRedisPattern_ReturnsCacheTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions { CacheTimeout = TimeSpan.FromSeconds(5) });
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var timeout = manager.GetTimeout("RedisLookup");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	#endregion

	#region RegisterTimeout Tests

	[Fact]
	public void RegisterTimeout_WithNullOperationName_ThrowsArgumentException()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => manager.RegisterTimeout(null!, TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public void RegisterTimeout_WithEmptyOperationName_ThrowsArgumentException()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => manager.RegisterTimeout(string.Empty, TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public void RegisterTimeout_WithZeroTimeout_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => manager.RegisterTimeout("op", TimeSpan.Zero));
	}

	[Fact]
	public void RegisterTimeout_WithNegativeTimeout_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => manager.RegisterTimeout("op", TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void RegisterTimeout_WithValidParameters_RegistersTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		manager.RegisterTimeout("MyOperation", TimeSpan.FromSeconds(45));
		var timeout = manager.GetTimeout("MyOperation");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void RegisterTimeout_CalledTwice_UpdatesTimeout()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		manager.RegisterTimeout("MyOperation", TimeSpan.FromSeconds(30));
		manager.RegisterTimeout("MyOperation", TimeSpan.FromSeconds(60));
		var timeout = manager.GetTimeout("MyOperation");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	#endregion

	#region IsSlowOperation Tests

	[Fact]
	public void IsSlowOperation_WhenBelowThreshold_ReturnsFalse()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(10),
			SlowOperationThreshold = 0.8
		});
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act - 5 seconds elapsed, threshold is 80% of 10s = 8s
		var result = manager.IsSlowOperation("TestOp", TimeSpan.FromSeconds(5));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsSlowOperation_WhenAtThreshold_ReturnsTrue()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(10),
			SlowOperationThreshold = 0.8,
			LogTimeoutWarnings = true
		});
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act - 8 seconds elapsed, threshold is 80% of 10s = 8s
		var result = manager.IsSlowOperation("TestOp", TimeSpan.FromSeconds(8));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsSlowOperation_WhenAboveThreshold_ReturnsTrue()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(10),
			SlowOperationThreshold = 0.8,
			LogTimeoutWarnings = true
		});
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act - 9 seconds elapsed, threshold is 80% of 10s = 8s
		var result = manager.IsSlowOperation("TestOp", TimeSpan.FromSeconds(9));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsSlowOperation_WhenLoggingDisabled_StillReturnsTrue()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(10),
			SlowOperationThreshold = 0.8,
			LogTimeoutWarnings = false
		});
		var logger = A.Fake<ILogger<TimeoutManager>>();
		var manager = new TimeoutManager(options, logger);

		// Act
		var result = manager.IsSlowOperation("TestOp", TimeSpan.FromSeconds(9));

		// Assert
		result.ShouldBeTrue();
	}

	#endregion
}
