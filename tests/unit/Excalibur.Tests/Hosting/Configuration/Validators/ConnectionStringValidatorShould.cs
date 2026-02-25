// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration.Validators;

namespace Excalibur.Tests.Hosting.Configuration.Validators;

[Trait("Category", "Unit")]
public sealed class ConnectionStringValidatorShould
{
	[Fact]
	public async Task ValidateSqlServerConnectionStringSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Integrated Security=true;",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task ValidateSqlServerConnectionStringWithUserPassword()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;User ID=sa;Password=P@ssw0rd;",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task FailWhenSqlServerConnectionStringMissingServer()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Database=TestDb;Integrated Security=true;",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(static e => e.Message.Contains("missing server"));
	}

	[Fact]
	public async Task FailWhenSqlServerConnectionStringMissingDatabase()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Integrated Security=true;",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(static e => e.Message.Contains("missing database"));
	}

	[Fact]
	public async Task FailWhenSqlServerConnectionStringMissingAuthentication()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(static e => e.Message.Contains("authentication"));
	}

	[Fact]
	public async Task ValidatePostgresConnectionStringSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=testdb;Username=postgres;Password=password",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Postgres);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task FailWhenPostgresConnectionStringMissingHost()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Database=testdb;Username=postgres;Password=password",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Postgres);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(static e => e.Message.Contains("missing host"));
	}

	[Fact]
	public async Task ValidateMySqlConnectionStringSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=testdb;User=root;Password=password",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MySql);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task ValidateSqliteConnectionStringSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "Data Source=app.db" })
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.Sqlite);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task ValidateMongoDbConnectionStringSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "mongodb://localhost:27017/testdb",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MongoDb);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task FailWhenMongoDbConnectionStringHasInvalidScheme()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "http://localhost:27017/testdb",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.MongoDb);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(static e => e.Message.Contains("mongodb://"));
	}

	[Fact]
	public async Task ValidateRedisConnectionStringSuccessfully()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = "localhost:6379" })
			.Build();

		var validator = new ConnectionStringValidator("Redis", DatabaseProvider.Redis);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task ValidateRedisConnectionStringWithConfiguration()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Redis"] = "localhost:6379,password=P@ssw0rd,ssl=true",
			})
			.Build();

		var validator = new ConnectionStringValidator("Redis", DatabaseProvider.Redis);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task FailWhenConnectionStringIsMissing()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(static e => e.Message.Contains("missing or empty"));
	}

	[Fact]
	public async Task FailWhenConnectionStringIsEmpty()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "" })
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(static e => e.Message.Contains("missing or empty"));
	}

	[Fact]
	public async Task FailWhenConnectionStringHasInvalidFormat()
	{
		// Arrange
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = "This is not a valid connection string",
			})
			.Build();

		var validator = new ConnectionStringValidator("DefaultConnection", DatabaseProvider.SqlServer);

		// Act
		var result = await validator.ValidateAsync(configuration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
	}
}
