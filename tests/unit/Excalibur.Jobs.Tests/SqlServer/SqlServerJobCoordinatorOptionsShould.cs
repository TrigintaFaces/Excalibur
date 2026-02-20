// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.SqlServer;

namespace Excalibur.Jobs.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqlServerJobCoordinatorOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new SqlServerJobCoordinatorOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
		options.SchemaName.ShouldBe("Jobs");
		options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.InstanceTtl.ShouldBe(TimeSpan.FromMinutes(5));
		options.CompletionRetention.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var options = new SqlServerJobCoordinatorOptions();

		// Act
		options.ConnectionString = "Server=.;Database=Jobs;Trusted_Connection=True";

		// Assert
		options.ConnectionString.ShouldBe("Server=.;Database=Jobs;Trusted_Connection=True");
	}

	[Fact]
	public void AllowSettingSchemaName()
	{
		// Arrange
		var options = new SqlServerJobCoordinatorOptions();

		// Act
		options.SchemaName = "CustomSchema";

		// Assert
		options.SchemaName.ShouldBe("CustomSchema");
	}

	[Fact]
	public void AllowSettingLockTimeout()
	{
		// Arrange
		var options = new SqlServerJobCoordinatorOptions();

		// Act
		options.LockTimeout = TimeSpan.FromMinutes(1);

		// Assert
		options.LockTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void AllowSettingInstanceTtl()
	{
		// Arrange
		var options = new SqlServerJobCoordinatorOptions();

		// Act
		options.InstanceTtl = TimeSpan.FromMinutes(10);

		// Assert
		options.InstanceTtl.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowSettingCompletionRetention()
	{
		// Arrange
		var options = new SqlServerJobCoordinatorOptions();

		// Act
		options.CompletionRetention = TimeSpan.FromDays(1);

		// Assert
		options.CompletionRetention.ShouldBe(TimeSpan.FromDays(1));
	}
}
