// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer;

/// <summary>
/// Unit tests for SqlServerProviderOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServerProviderOptions")]
public sealed class SqlServerProviderOptionsShould : UnitTestBase
{
	[Fact]
	public void CreateWithDefaultConnectionString()
	{
		// Arrange & Act
		var options = new SqlServerProviderOptions();

		// Assert
		options.ConnectionString.ShouldNotBeEmpty();
	}

	[Fact]
	public void ConnectionStringCanBeSet()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.ConnectionString = "Server=localhost;Database=TestDb;";

		// Assert
		options.ConnectionString.ShouldBe("Server=localhost;Database=TestDb;");
	}

	[Fact]
	public void EnableMarsCanBeSet()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.EnableMars = true;

		// Assert
		options.EnableMars.ShouldBeTrue();
	}

	[Fact]
	public void CommandTimeoutCanBeCustomized()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.CommandTimeout = 60;

		// Assert
		options.CommandTimeout.ShouldBe(60);
	}

	[Fact]
	public void MaxPoolSizeCanBeCustomized()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.MaxPoolSize = 200;

		// Assert
		options.MaxPoolSize.ShouldBe(200);
	}

	[Fact]
	public void ApplicationNameCanBeSet()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.ApplicationName = "MyApplication";

		// Assert
		options.ApplicationName.ShouldBe("MyApplication");
	}

	[Fact]
	public void EnablePoolingCanBeDisabled()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.EnablePooling = false;

		// Assert
		options.EnablePooling.ShouldBeFalse();
	}

	[Fact]
	public void MaxRetryCountCanBeCustomized()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.MaxRetryCount = 5;

		// Assert
		options.MaxRetryCount.ShouldBe(5);
	}

	[Fact]
	public void RetryDelayCanBeCustomized()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.RetryDelay = TimeSpan.FromSeconds(5);

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}
}
