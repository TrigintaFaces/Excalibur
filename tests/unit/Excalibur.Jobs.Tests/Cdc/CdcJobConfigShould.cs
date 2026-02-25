// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;

using Excalibur.Data.SqlServer.Cdc;
using Excalibur.Jobs.Cdc;
using Excalibur.Jobs.Core;

namespace Excalibur.Jobs.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcJobConfig"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Cdc")]
public sealed class CdcJobConfigShould
{
	[Fact]
	public void InheritFromJobConfig()
	{
		// Arrange & Act
		var config = new CdcJobConfig { DatabaseConfigs = [] };

		// Assert
		config.ShouldBeAssignableTo<JobConfig>();
	}

	[Fact]
	public void HaveEmptyDatabaseConfigsByDefault()
	{
		// Arrange & Act
		var config = new CdcJobConfig { DatabaseConfigs = [] };

		// Assert
		config.DatabaseConfigs.ShouldNotBeNull();
		config.DatabaseConfigs.ShouldBeEmpty();
	}

	[Fact]
	public void AcceptDatabaseConfigs()
	{
		// Arrange
		var dbConfig = new DatabaseConfig
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "TestDbConnection",
			StateConnectionIdentifier = "TestStateConnection",
		};

		// Act
		var config = new CdcJobConfig
		{
			DatabaseConfigs = [dbConfig],
		};

		// Assert
		config.DatabaseConfigs.Count.ShouldBe(1);
		config.DatabaseConfigs[0].ShouldBe(dbConfig);
	}

	[Fact]
	public void AcceptMultipleDatabaseConfigs()
	{
		// Arrange
		var dbConfig1 = new DatabaseConfig
		{
			DatabaseName = "Db1",
			DatabaseConnectionIdentifier = "Db1Connection",
			StateConnectionIdentifier = "State1Connection",
		};
		var dbConfig2 = new DatabaseConfig
		{
			DatabaseName = "Db2",
			DatabaseConnectionIdentifier = "Db2Connection",
			StateConnectionIdentifier = "State2Connection",
		};

		// Act
		var config = new CdcJobConfig
		{
			DatabaseConfigs = [dbConfig1, dbConfig2],
		};

		// Assert
		config.DatabaseConfigs.Count.ShouldBe(2);
		config.DatabaseConfigs.ShouldContain(dbConfig1);
		config.DatabaseConfigs.ShouldContain(dbConfig2);
	}

	[Fact]
	public void InheritCronScheduleFromJobConfig()
	{
		// Arrange & Act
		var config = new CdcJobConfig
		{
			DatabaseConfigs = [],
			CronSchedule = "0 */5 * * * ?",
		};

		// Assert
		config.CronSchedule.ShouldBe("0 */5 * * * ?");
	}

	[Fact]
	public void InheritJobNameFromJobConfig()
	{
		// Arrange & Act
		var config = new CdcJobConfig
		{
			DatabaseConfigs = [],
			JobName = "CdcProcessorJob",
		};

		// Assert
		config.JobName.ShouldBe("CdcProcessorJob");
	}

	[Fact]
	public void InheritJobGroupFromJobConfig()
	{
		// Arrange & Act
		var config = new CdcJobConfig
		{
			DatabaseConfigs = [],
			JobGroup = "CdcJobs",
		};

		// Assert
		config.JobGroup.ShouldBe("CdcJobs");
	}

	[Fact]
	public void InheritDisabledFromJobConfig()
	{
		// Arrange & Act
		var config = new CdcJobConfig
		{
			DatabaseConfigs = [],
			Disabled = true,
		};

		// Assert
		config.Disabled.ShouldBeTrue();
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var config = new CdcJobConfig
		{
			DatabaseConfigs =
			[
				new DatabaseConfig
				{
					DatabaseName = "TestDb",
					DatabaseConnectionIdentifier = "TestConnection",
					StateConnectionIdentifier = "TestStateConnection",
				},
			],
			CronSchedule = "0 0 * * * ?",
			JobName = "TestCdcJob",
			JobGroup = "TestGroup",
			Disabled = false,
		};

		// Assert
		config.DatabaseConfigs.Count.ShouldBe(1);
		config.CronSchedule.ShouldBe("0 0 * * * ?");
		config.JobName.ShouldBe("TestCdcJob");
		config.JobGroup.ShouldBe("TestGroup");
		config.Disabled.ShouldBeFalse();
	}

	[Fact]
	public void SupportAddingToDatabaseConfigs()
	{
		// Arrange
		var config = new CdcJobConfig { DatabaseConfigs = [] };
		var dbConfig = new DatabaseConfig
		{
			DatabaseName = "AddedDb",
			DatabaseConnectionIdentifier = "AddedConnection",
			StateConnectionIdentifier = "AddedStateConnection",
		};

		// Act
		config.DatabaseConfigs.Add(dbConfig);

		// Assert
		config.DatabaseConfigs.Count.ShouldBe(1);
		config.DatabaseConfigs[0].ShouldBe(dbConfig);
	}

	[Fact]
	public void DatabaseConfigsIsCollectionType()
	{
		// Arrange & Act
		var config = new CdcJobConfig { DatabaseConfigs = [] };

		// Assert
		config.DatabaseConfigs.ShouldBeOfType<Collection<DatabaseConfig>>();
	}
}
