// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="ConnectionStringValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConnectionStringValidatorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringKeyIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new ConnectionStringValidator(null!, DatabaseProvider.SqlServer));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringKeyIsEmpty()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new ConnectionStringValidator("", DatabaseProvider.SqlServer));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringKeyIsWhitespace()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new ConnectionStringValidator("   ", DatabaseProvider.SqlServer));
	}

	[Fact]
	public void SetConfigurationNameFromConnectionStringKey()
	{
		// Arrange
		const string key = "DefaultConnection";

		// Act
		var validator = new ConnectionStringValidator(key, DatabaseProvider.SqlServer);

		// Assert
		validator.ConfigurationName.ShouldBe($"ConnectionString:{key}");
	}

	[Fact]
	public void UseCustomConfigurationName_WhenProvided()
	{
		// Arrange
		const string key = "DefaultConnection";
		const string customName = "CustomConfigName";

		// Act
		var validator = new ConnectionStringValidator(key, DatabaseProvider.SqlServer, configurationName: customName);

		// Assert
		validator.ConfigurationName.ShouldBe(customName);
	}

	[Fact]
	public void SetPriorityTo10()
	{
		// Act
		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Assert
		validator.Priority.ShouldBe(10);
	}

	#endregion

	#region Missing Connection String Tests

	[Fact]
	public async Task ReturnFailure_WhenConnectionStringIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing or empty"));
	}

	[Fact]
	public async Task ReturnFailure_WhenConnectionStringIsEmpty()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = ""
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing or empty"));
	}

	#endregion

	#region SQL Server Connection String Tests

	[Fact]
	public async Task ReturnSuccess_WhenSqlServerConnectionStringIsValid_WithIntegratedSecurity()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Integrated Security=true"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnSuccess_WhenSqlServerConnectionStringIsValid_WithUserPassword()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;User ID=sa;Password=secret"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenSqlServerUsesDataSource()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Data Source=localhost;Initial Catalog=TestDb;Integrated Security=true"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenSqlServerUsesTrustedConnection()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Trusted_Connection=true"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenSqlServerMissingServer()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Database=TestDb;Integrated Security=true"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing server/data source"));
	}

	[Fact]
	public async Task ReturnFailure_WhenSqlServerMissingDatabase()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Integrated Security=true"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing database name"));
	}

	[Fact]
	public async Task ReturnFailure_WhenSqlServerMissingAuthentication()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing authentication"));
	}

	#endregion

	#region Postgres Connection String Tests

	[Fact]
	public async Task ReturnSuccess_WhenPostgresConnectionStringIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=TestDb;Username=postgres"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Postgres);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenPostgresUsesServerAndUserID()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;User ID=postgres"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Postgres);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenPostgresMissingHost()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Database=TestDb;Username=postgres"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Postgres);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing host/server"));
	}

	[Fact]
	public async Task ReturnFailure_WhenPostgresMissingDatabase()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Host=localhost;Username=postgres"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Postgres);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing database name"));
	}

	[Fact]
	public async Task ReturnFailure_WhenPostgresMissingUsername()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=TestDb"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Postgres);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing username"));
	}

	#endregion

	#region MySQL Connection String Tests

	[Fact]
	public async Task ReturnSuccess_WhenMySqlConnectionStringIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;User=root"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MySql);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenMySqlUsesUid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=TestDb;Uid=root"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MySql);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenMySqlMissingServer()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Database=TestDb;User=root"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MySql);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing server/host"));
	}

	[Fact]
	public async Task ReturnFailure_WhenMySqlMissingDatabase()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;User=root"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MySql);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing database name"));
	}

	[Fact]
	public async Task ReturnFailure_WhenMySqlMissingUser()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MySql);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing user"));
	}

	#endregion

	#region SQLite Connection String Tests

	[Fact]
	public async Task ReturnSuccess_WhenSqliteConnectionStringIsValid_WithDataSource()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Data Source=database.db"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Sqlite);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenSqliteConnectionStringIsValid_WithFilename()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Filename=database.db"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Sqlite);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenSqliteMissingDataSource()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Mode=ReadWrite"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Sqlite);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing data source"));
	}

	#endregion

	#region MongoDB Connection String Tests

	[Fact]
	public async Task ReturnSuccess_WhenMongoDbConnectionStringIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "mongodb://localhost:27017/testdb"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MongoDb);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenMongoDbConnectionStringUsesMongoDbSrv()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "mongodb+srv://user:pass@cluster.mongodb.net/testdb"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MongoDb);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenMongoDbConnectionStringDoesNotStartWithMongoDb()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "http://localhost:27017/testdb"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MongoDb);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("must start with 'mongodb://'"));
	}

	#endregion

	#region Redis Connection String Tests

	[Fact]
	public async Task ReturnSuccess_WhenRedisConnectionStringIsValidSimpleFormat()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "localhost:6379"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Redis);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenRedisConnectionStringUsesConfigurationFormat()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "localhost:6379,password=secret,ssl=true"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Redis);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenRedisConnectionStringUsesIpAddress()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "127.0.0.1:6379,ssl=false"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Redis);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenRedisSimpleFormatMissingPort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "localhost"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Redis);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing port"));
	}

	[Fact]
	public async Task ReturnFailure_WhenRedisConfigurationFormatMissingEndpoint()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "password=secret,ssl=true"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Redis);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing endpoint"));
	}

	#endregion

	#region Invalid Format Tests

	[Fact]
	public async Task ReturnFailure_WhenConnectionStringHasInvalidFormat()
	{
		// Arrange - Using an invalid connection string format that can't be parsed
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=\"unclosed quote"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid connection string format"));
	}

	#endregion

	#region TestConnection Option Tests

	[Fact]
	public async Task AcceptTestConnectionParameter()
	{
		// Arrange - testConnection: true but format is valid, so no actual connection test fails
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Integrated Security=true"
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer, testConnection: true);

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert - The test connection is a stub, so format validation passes
		result.IsValid.ShouldBeTrue();
	}

	#endregion
}
