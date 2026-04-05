// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.IdentityMap.Builders;
using Excalibur.Data.IdentityMap.SqlServer;
using Excalibur.Data.IdentityMap.SqlServer.Builders;

namespace Excalibur.Data.IdentityMap.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="IdentityMapBuilderSqlServerExtensions" />.
/// </summary>
[Trait("Component", "IdentityMap")]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class IdentityMapBuilderSqlServerExtensionsShould
{
	#region UseSqlServer

	[Fact]
	public void UseSqlServer_RegistersSqlServerIdentityMapStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = A.Fake<IIdentityMapBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseSqlServer(sql =>
			sql.ConnectionString("Server=.;Database=Test;Trusted_Connection=True;"));

		// Assert
		using var provider = services.BuildServiceProvider();
		var store = provider.GetService<IIdentityMapStore>();
		_ = store.ShouldNotBeNull();
		_ = store.ShouldBeOfType<SqlServerIdentityMapStore>();
	}

	[Fact]
	public void UseSqlServer_RegistersSqlServerIdentityMapOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IIdentityMapBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseSqlServer(sql =>
			sql.ConnectionString("Server=.;Database=Test;")
			   .SchemaName("custom")
			   .TableName("MyMap")
			   .CommandTimeout(TimeSpan.FromSeconds(60))
			   .MaxBatchSize(200));

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerIdentityMapOptions>>();
		options.Value.ConnectionString.ShouldBe("Server=.;Database=Test;");
		options.Value.SchemaName.ShouldBe("custom");
		options.Value.TableName.ShouldBe("MyMap");
		options.Value.CommandTimeoutSeconds.ShouldBe(60);
		options.Value.MaxBatchSize.ShouldBe(200);
	}

	[Fact]
	public void UseSqlServer_RegistersOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IIdentityMapBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseSqlServer(sql =>
			sql.ConnectionString("Server=.;Database=Test;"));

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<SqlServerIdentityMapOptions>>();
		validators.ShouldContain(v => v is SqlServerIdentityMapOptionsValidator);
	}

	[Fact]
	public void UseSqlServer_UsesTryAdd_DoesNotOverrideExistingStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var fakeStore = A.Fake<IIdentityMapStore>();
		services.AddSingleton(fakeStore);
		var builder = A.Fake<IIdentityMapBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseSqlServer(sql =>
			sql.ConnectionString("Server=.;Database=Test;"));

		// Assert
		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IIdentityMapStore>();
		resolved.ShouldBeSameAs(fakeStore);
	}

	[Fact]
	public void UseSqlServer_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IIdentityMapBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseSqlServer(sql =>
			sql.ConnectionString("Server=.;Database=Test;"));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseSqlServer_ThrowsOnNullBuilder()
	{
		IIdentityMapBuilder builder = null!;

		_ = Should.Throw<ArgumentNullException>(
			() => builder.UseSqlServer(_ => { }));
	}

	[Fact]
	public void UseSqlServer_ThrowsOnNullConfigure()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IIdentityMapBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		_ = Should.Throw<ArgumentNullException>(
			() => builder.UseSqlServer(null!));
	}

	#endregion
}
