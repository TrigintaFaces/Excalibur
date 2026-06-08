// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Excalibur.Jobs.Cdc;
using Excalibur.Jobs.Core;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Jobs.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcJobOptions"/>.
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
		var config = new CdcJobOptions { DatabaseConfigs = [] };

		// Assert
		config.ShouldBeAssignableTo<JobOptions>();
	}

	[Fact]
	public void HaveEmptyDatabaseConfigsByDefault()
	{
		// Arrange & Act
		var config = new CdcJobOptions { DatabaseConfigs = [] };

		// Assert
		config.DatabaseConfigs.ShouldNotBeNull();
		config.DatabaseConfigs.ShouldBeEmpty();
	}

	[Fact]
	public void AcceptDatabaseConfigs()
	{
		// Arrange
		var dbConfig = new DatabaseOptions
		{
			DatabaseName = "TestDb",
			DatabaseConnectionIdentifier = "TestDbConnection",
			StateConnectionIdentifier = "TestStateConnection",
		};

		// Act
		var config = new CdcJobOptions
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
		var dbConfig1 = new DatabaseOptions
		{
			DatabaseName = "Db1",
			DatabaseConnectionIdentifier = "Db1Connection",
			StateConnectionIdentifier = "State1Connection",
		};
		var dbConfig2 = new DatabaseOptions
		{
			DatabaseName = "Db2",
			DatabaseConnectionIdentifier = "Db2Connection",
			StateConnectionIdentifier = "State2Connection",
		};

		// Act
		var config = new CdcJobOptions
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
		var config = new CdcJobOptions
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
		var config = new CdcJobOptions
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
		var config = new CdcJobOptions
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
		var config = new CdcJobOptions
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
		var config = new CdcJobOptions
		{
			DatabaseConfigs =
			[
				new DatabaseOptions
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
		var config = new CdcJobOptions { DatabaseConfigs = [] };
		var dbConfig = new DatabaseOptions
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
		var config = new CdcJobOptions { DatabaseConfigs = [] };

		// Assert
		config.DatabaseConfigs.ShouldBeOfType<Collection<DatabaseOptions>>();
	}

	[Fact]
	public void BindTablesFromConfigurationAndDeriveCaptureInstanceMap()
	{
		// Arrange — mirrors a real appsettings.json "Jobs:CdcJob" section using the Tables shape,
		// including a non-dbo schema to prove no schema is assumed.
		var settings = new Dictionary<string, string?>
		{
			["Jobs:CdcJob:JobName"] = "LegacyCdcProcessor",
			["Jobs:CdcJob:DatabaseConfigs:0:DatabaseName"] = "Legacy",
			["Jobs:CdcJob:DatabaseConfigs:0:DatabaseConnectionIdentifier"] = "LegacyCdc",
			["Jobs:CdcJob:DatabaseConfigs:0:StateConnectionIdentifier"] = "LegacyState",
			["Jobs:CdcJob:DatabaseConfigs:0:Tables:0:TableName"] = "Account",
			["Jobs:CdcJob:DatabaseConfigs:0:Tables:0:CaptureInstance"] = "dbo_Account",
			["Jobs:CdcJob:DatabaseConfigs:0:Tables:1:TableName"] = "sales.Order",
			["Jobs:CdcJob:DatabaseConfigs:0:Tables:1:CaptureInstance"] = "sales_Order",
		};

		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		// Act
		var jobConfig = configuration.GetSection(CdcJob.JobConfigSectionName).Get<CdcJobOptions>();

		// Assert
		jobConfig.ShouldNotBeNull();
		var db = jobConfig.DatabaseConfigs.ShouldHaveSingleItem();
		db.Tables.Count.ShouldBe(2);
		db.CaptureInstances.ShouldBe(["dbo_Account", "sales_Order"], ignoreOrder: true);
		db.CaptureInstanceToTableNameMap["dbo_Account"].ShouldBe("Account");
		db.CaptureInstanceToTableNameMap["sales_Order"].ShouldBe("sales.Order");
	}
}
