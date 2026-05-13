// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcIdempotencyBuilderExtensions"/> — SqlServer filter overloads.
/// Verifies DI registration behavior for the SQL Server CDC idempotency filter.
/// </summary>
/// <remarks>
/// Sprint 826 — bd-cgqeih: SqlServer CDC idempotency filter DI extensions.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcIdempotencyBuilderExtensionsSqlServerShould : UnitTestBase
{
	[Fact]
	public void RegisterSqlServerIdempotencyFilter()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		// Register a fake IDbConnection (required by SqlServerCdcIdempotencyFilter constructor)
		services.AddSingleton<System.Data.IDbConnection>(A.Fake<System.Data.IDbConnection>());

		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseSqlServerIdempotencyFilter();

		// Assert — returns the builder for fluent chaining
		result.ShouldBe(builder);

		// Assert — ICdcIdempotencyFilter is registered as SqlServerCdcIdempotencyFilter
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICdcIdempotencyFilter));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(SqlServerCdcIdempotencyFilter));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterOptionsValidator_WithValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<System.Data.IDbConnection>(A.Fake<System.Data.IDbConnection>());

		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.UseSqlServerIdempotencyFilter();

		// Assert — IValidateOptions<SqlServerCdcIdempotencyFilterOptions> is registered
		var validatorDescriptor = services.FirstOrDefault(
			d => d.ServiceType == typeof(IValidateOptions<SqlServerCdcIdempotencyFilterOptions>));
		validatorDescriptor.ShouldNotBeNull();
	}

	[Fact]
	public void ApplyConfigureDelegate_WhenOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<System.Data.IDbConnection>(A.Fake<System.Data.IDbConnection>());

		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.UseSqlServerIdempotencyFilter(opts =>
		{
			opts.SchemaName = "CustomSchema";
			opts.RetentionPeriod = TimeSpan.FromHours(48);
			opts.CleanupBatchSize = 5000;
		});

		// Assert — build the service provider and check options are configured
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<SqlServerCdcIdempotencyFilterOptions>>().Value;
		options.SchemaName.ShouldBe("CustomSchema");
		options.RetentionPeriod.ShouldBe(TimeSpan.FromHours(48));
		options.CleanupBatchSize.ShouldBe(5000);
	}

	[Fact]
	public void ReplacePriorInMemoryRegistration()
	{
		// Arrange — register in-memory filter first
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<System.Data.IDbConnection>(A.Fake<System.Data.IDbConnection>());

		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.UseInMemoryIdempotencyFilter();

		// Act — SqlServer filter uses AddSingleton (replaces TryAdd)
		builder.UseSqlServerIdempotencyFilter();

		// Assert — both registrations exist (last wins in DI resolution)
		var registrations = services
			.Where(d => d.ServiceType == typeof(ICdcIdempotencyFilter))
			.ToList();
		registrations.Count.ShouldBeGreaterThanOrEqualTo(2);

		// The last registration should be SqlServerCdcIdempotencyFilter
		var lastRegistration = registrations.Last();
		lastRegistration.ImplementationType.ShouldBe(typeof(SqlServerCdcIdempotencyFilter));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_Parameterless()
	{
		Should.Throw<ArgumentNullException>(
			() => CdcIdempotencyBuilderExtensions.UseSqlServerIdempotencyFilter(null!));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_WithConfigure()
	{
		Should.Throw<ArgumentNullException>(
			() => CdcIdempotencyBuilderExtensions.UseSqlServerIdempotencyFilter(null!, _ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		Should.Throw<ArgumentNullException>(
			() => builder.UseSqlServerIdempotencyFilter(null!));
	}
}
