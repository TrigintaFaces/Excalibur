// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DatabaseConfigShould
{
	private static readonly string[] ExpectedCaptureInstances = ["dbo_Orders", "dbo_Products"];

	[Fact]
	public void CreateWithRequiredProperties()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.DatabaseName.ShouldBe("TestDb");
		config.DatabaseConnectionIdentifier.ShouldBe("db-conn");
		config.StateConnectionIdentifier.ShouldBe("state-conn");
	}

	[Fact]
	public void HaveDefaultStopOnMissingTableHandler()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.StopOnMissingTableHandler.ShouldBe(DatabaseConfigDefaults.CdcDefaultStopOnMissingTableHandler);
	}

	[Fact]
	public void HaveDefaultCaptureInstances()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.CaptureInstances.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultBatchTimeInterval()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.BatchTimeInterval.ShouldBe(DatabaseConfigDefaults.CdcDefaultBatchTimeInterval);
	}

	[Fact]
	public void HaveDefaultQueueSize()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.QueueSize.ShouldBe(DatabaseConfigDefaults.CdcDefaultQueueSize);
	}

	[Fact]
	public void HaveDefaultProducerBatchSize()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.ProducerBatchSize.ShouldBe(DatabaseConfigDefaults.CdcDefaultProducerBatchSize);
	}

	[Fact]
	public void HaveDefaultConsumerBatchSize()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.ConsumerBatchSize.ShouldBe(DatabaseConfigDefaults.CdcDefaultConsumerBatchSize);
	}

	[Fact]
	public void AllowCustomCaptureInstances()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			CaptureInstances = ["dbo_Orders", "dbo_Products"]
		};

		config.CaptureInstances.ShouldBe(ExpectedCaptureInstances);
	}

	[Fact]
	public void ThrowWhenCaptureInstancesIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			CaptureInstances = null!
		});
	}

	[Fact]
	public void ThrowWhenBatchTimeIntervalIsZero()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			BatchTimeInterval = 0
		});
	}

	[Fact]
	public void ThrowWhenQueueSizeIsNegative()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			QueueSize = -1
		});
	}

	[Fact]
	public void ThrowWhenProducerBatchSizeIsZero()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			ProducerBatchSize = 0
		});
	}

	[Fact]
	public void ThrowWhenConsumerBatchSizeIsZero()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			ConsumerBatchSize = 0
		});
	}

	[Fact]
	public void HaveNullRecoveryOptionsByDefault()
	{
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.RecoveryOptions.ShouldBeNull();
	}

	[Fact]
	public void AllowCustomRecoveryOptions()
	{
		var recoveryOptions = new CdcRecoveryOptions();
		var config = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			RecoveryOptions = recoveryOptions
		};

		config.RecoveryOptions.ShouldBeSameAs(recoveryOptions);
	}
}
