// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

using Excalibur.Compliance.SqlServer;

namespace Excalibur.Dispatch.Security.Tests.Compliance.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerKeyEscrowServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SqlServerKeyEscrowServiceCollectionExtensionsShould
{
	#region AddSqlServerKeyEscrow with Action<SqlServerKeyEscrowOptions>

	[Fact]
	public void AddSqlServerKeyEscrow_ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerKeyEscrow(options => { }));
	}

	[Fact]
	public void AddSqlServerKeyEscrow_ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerKeyEscrow((Action<SqlServerKeyEscrowOptions>)null!));
	}

	[Fact]
	public void AddSqlServerKeyEscrow_ReturnServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqlServerKeyEscrow(options =>
		{
			options.ConnectionString = "Server=localhost;Database=test;";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddSqlServerKeyEscrow_RegisterIKeyEscrowService()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(A.Fake<IEncryptionProvider>());
		_ = services.AddLogging();

		// Act
		_ = services.AddSqlServerKeyEscrow(options =>
		{
			options.ConnectionString = "Server=localhost;Database=test;";
		});

		// Assert
		var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IKeyEscrowService));
		_ = descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(SqlServerKeyEscrowService));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddSqlServerKeyEscrow_ConfigureOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(A.Fake<IEncryptionProvider>());
		_ = services.AddLogging();
		const string expectedConnectionString = "Server=myserver;Database=mydb;";

		// Act
		_ = services.AddSqlServerKeyEscrow(options =>
		{
			options.ConnectionString = expectedConnectionString;
			options.Schema = "custom";
			options.TableName = "CustomEscrow";
		});

		// Build provider and resolve options
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>();

		// Assert
		options.Value.ConnectionString.ShouldBe(expectedConnectionString);
		options.Value.Schema.ShouldBe("custom");
		options.Value.TableName.ShouldBe("CustomEscrow");
	}

	[Fact]
	public void AddSqlServerKeyEscrow_NotReplaceExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var fakeService = A.Fake<IKeyEscrowService>();
		_ = services.AddSingleton(fakeService);
		_ = services.AddSingleton(A.Fake<IEncryptionProvider>());
		_ = services.AddLogging();

		// Act
		_ = services.AddSqlServerKeyEscrow(options =>
		{
			options.ConnectionString = "Server=localhost;Database=test;";
		});

		// Build provider and resolve
		var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IKeyEscrowService>();

		// Assert - should return the first registration (fake)
		resolved.ShouldBeSameAs(fakeService);
	}

	#endregion AddSqlServerKeyEscrow with Action<SqlServerKeyEscrowOptions>

	#region AddSqlServerKeyEscrow with connection string

	[Fact]
	public void AddSqlServerKeyEscrowWithConnectionString_ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerKeyEscrow("Server=localhost;Database=test;"));
	}

	[Fact]
	public void AddSqlServerKeyEscrowWithConnectionString_ThrowArgumentException_WhenConnectionStringIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerKeyEscrow((string)null!));
	}

	[Fact]
	public void AddSqlServerKeyEscrowWithConnectionString_ThrowArgumentException_WhenConnectionStringIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerKeyEscrow(string.Empty));
	}

	[Fact]
	public void AddSqlServerKeyEscrowWithConnectionString_ThrowArgumentException_WhenConnectionStringIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerKeyEscrow("   "));
	}

	[Fact]
	public void AddSqlServerKeyEscrowWithConnectionString_ReturnServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqlServerKeyEscrow("Server=localhost;Database=test;");

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddSqlServerKeyEscrowWithConnectionString_ConfigureConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(A.Fake<IEncryptionProvider>());
		_ = services.AddLogging();
		const string connectionString = "Server=myserver;Database=mydb;";

		// Act
		_ = services.AddSqlServerKeyEscrow(connectionString);

		// Build provider and resolve options
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>();

		// Assert
		options.Value.ConnectionString.ShouldBe(connectionString);
	}

	[Fact]
	public void AddSqlServerKeyEscrowWithConnectionString_UseDefaultsForOtherOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(A.Fake<IEncryptionProvider>());
		_ = services.AddLogging();

		// Act
		_ = services.AddSqlServerKeyEscrow("Server=localhost;Database=test;");

		// Build provider and resolve options
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerKeyEscrowOptions>>();

		// Assert - other options should have defaults
		options.Value.Schema.ShouldBe("compliance");
		options.Value.TableName.ShouldBe("KeyEscrow");
		options.Value.TokensTableName.ShouldBe("RecoveryTokens");
		options.Value.CommandTimeoutSeconds.ShouldBe(30);
	}

	#endregion AddSqlServerKeyEscrow with connection string
}
