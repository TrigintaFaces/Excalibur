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

		// Assert - ConnectionString defaults to empty (must be configured by consumer)
		options.Connection.ConnectionString.ShouldNotBeNull();
	}

	[Fact]
	public void ConnectionStringCanBeSet()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.Connection.ConnectionString = "Server=localhost;Database=TestDb;";

		// Assert
		options.Connection.ConnectionString.ShouldBe("Server=localhost;Database=TestDb;");
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
		options.Pooling.MaxPoolSize = 200;

		// Assert
		options.Pooling.MaxPoolSize.ShouldBe(200);
	}

	[Fact]
	public void ApplicationNameCanBeSet()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.Connection.ApplicationName = "MyApplication";

		// Assert
		options.Connection.ApplicationName.ShouldBe("MyApplication");
	}

	[Fact]
	public void EnablePoolingCanBeDisabled()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.Pooling.EnablePooling = false;

		// Assert
		options.Pooling.EnablePooling.ShouldBeFalse();
	}

	[Fact]
	public void RetryCountCanBeCustomized()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.RetryCount = 5;

		// Assert
		options.RetryCount.ShouldBe(5);
	}
}
