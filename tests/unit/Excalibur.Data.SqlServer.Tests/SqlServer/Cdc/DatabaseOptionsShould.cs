// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DatabaseConfigShould
{
	private static readonly string[] ExpectedCaptureInstances = ["dbo_Orders", "dbo_Products"];

	[Fact]
	public void CreateWithRequiredProperties()
	{
		var config = new DatabaseOptions
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
		var config = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.StopOnMissingTableHandler.ShouldBe(DatabaseOptionsDefaults.CdcDefaultStopOnMissingTableHandler);
	}

	[Fact]
	public void HaveDefaultCaptureInstances()
	{
		var config = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.CaptureInstances.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultQueueSize()
	{
		var config = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.QueueSize.ShouldBe(DatabaseOptionsDefaults.CdcDefaultQueueSize);
	}

	[Fact]
	public void HaveDefaultProducerBatchSize()
	{
		var config = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.ProducerBatchSize.ShouldBe(DatabaseOptionsDefaults.CdcDefaultProducerBatchSize);
	}

	[Fact]
	public void HaveDefaultConsumerBatchSize()
	{
		var config = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.ConsumerBatchSize.ShouldBe(DatabaseOptionsDefaults.CdcDefaultConsumerBatchSize);
	}

	[Fact]
	public void AllowCustomCaptureInstances()
	{
		var config = new DatabaseOptions
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
		Should.Throw<ArgumentNullException>(() => new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			CaptureInstances = null!
		});
	}

	[Fact]
	public void ThrowWhenQueueSizeIsNegative()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new DatabaseOptions
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
		Should.Throw<ArgumentOutOfRangeException>(() => new DatabaseOptions
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
		Should.Throw<ArgumentOutOfRangeException>(() => new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			ConsumerBatchSize = 0
		});
	}

	[Fact]
	public void HaveDefaultCaptureInstanceToTableNameMap()
	{
		var config = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn"
		};

		config.CaptureInstanceToTableNameMap.ShouldNotBeNull();
		config.CaptureInstanceToTableNameMap.ShouldBeEmpty();
	}

	[Fact]
	public void AllowCustomCaptureInstanceToTableNameMap()
	{
		var map = new Dictionary<string, string>
		{
			["sales_Customers"] = "sales.Customers",
			["dbo_Orders"] = "dbo.Orders"
		};

		var config = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			CaptureInstanceToTableNameMap = map.AsReadOnly()
		};

		config.CaptureInstanceToTableNameMap.Count.ShouldBe(2);
		config.CaptureInstanceToTableNameMap["sales_Customers"].ShouldBe("sales.Customers");
		config.CaptureInstanceToTableNameMap["dbo_Orders"].ShouldBe("dbo.Orders");
	}

	[Fact]
	public void HaveNullRecoveryOptionsByDefault()
	{
		var config = new DatabaseOptions
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
		var config = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "db-conn",
			StateConnectionIdentifier = "state-conn",
			RecoveryOptions = recoveryOptions
		};

		config.RecoveryOptions.ShouldBeSameAs(recoveryOptions);
	}
}
