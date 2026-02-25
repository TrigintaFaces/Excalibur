// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch.Compliance;

namespace Excalibur.Tests.Compliance.SqlServer.Erasure;

/// <summary>
/// Unit tests for <see cref="SqlServerErasureStoreServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance.Erasure")]
public sealed class SqlServerErasureStoreDIShould : UnitTestBase
{
	[Fact]
	public void AddSqlServerErasureStore_WithDelegate_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerErasureStore(_ => { }));
	}

	[Fact]
	public void AddSqlServerErasureStore_WithDelegate_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerErasureStore((Action<SqlServerErasureStoreOptions>)null!));
	}

	[Fact]
	public void AddSqlServerErasureStore_WithDelegate_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddSqlServerErasureStore(opts =>
		{
			opts.ConnectionString = "Server=test;Database=testdb;Integrated Security=true";
		});

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(SqlServerErasureStore));
		services.ShouldContain(s => s.ServiceType == typeof(IErasureStore));
		services.ShouldContain(s => s.ServiceType == typeof(IErasureCertificateStore));
		services.ShouldContain(s => s.ServiceType == typeof(IErasureQueryStore));
	}

	[Fact]
	public void AddSqlServerErasureStore_WithDelegate_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.AddSqlServerErasureStore(opts =>
		{
			opts.ConnectionString = "Server=test;Database=testdb;Integrated Security=true";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddSqlServerErasureStore_WithConnectionString_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerErasureStore("Server=test"));
	}

	[Fact]
	public void AddSqlServerErasureStore_WithConnectionString_ThrowsArgumentException_WhenConnectionStringIsEmpty()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddSqlServerErasureStore(string.Empty));
	}

	[Fact]
	public void AddSqlServerErasureStore_WithConnectionString_ThrowsArgumentException_WhenConnectionStringIsWhitespace()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddSqlServerErasureStore("   "));
	}

	[Fact]
	public void AddSqlServerErasureStore_WithConnectionString_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddSqlServerErasureStore("Server=test;Database=testdb;Integrated Security=true");

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(IErasureStore));
	}

	[Fact]
	public void AddSqlServerErasureStoreFromConfiguration_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerErasureStoreFromConfiguration("Compliance"));
	}

	[Fact]
	public void AddSqlServerErasureStoreFromConfiguration_ThrowsArgumentException_WhenNameIsEmpty()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddSqlServerErasureStoreFromConfiguration(string.Empty));
	}

	[Fact]
	public void AddSqlServerErasureStoreFromConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddSqlServerErasureStoreFromConfiguration("Compliance");

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(IErasureStore));
		services.ShouldContain(s => s.ServiceType == typeof(IErasureCertificateStore));
		services.ShouldContain(s => s.ServiceType == typeof(IErasureQueryStore));
	}

	[Fact]
	public void AddSqlServerErasureStoreFromConfiguration_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.AddSqlServerErasureStoreFromConfiguration("Compliance");

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddSqlServerErasureStore_DoesNotRegisterDuplicates_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddSqlServerErasureStore(opts =>
			opts.ConnectionString = "Server=test;Database=db1;Integrated Security=true");
		services.AddSqlServerErasureStore(opts =>
			opts.ConnectionString = "Server=test;Database=db2;Integrated Security=true");

		// Assert - TryAdd should prevent duplicates
		var erasureStoreCount = services.Count(s => s.ServiceType == typeof(IErasureStore));
		erasureStoreCount.ShouldBe(1);
	}
}
