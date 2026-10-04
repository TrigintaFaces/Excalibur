// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Binds the SQL Server CDC idempotency filter's registration on <see cref="ICdcBuilder"/> by RESOLVING
/// it from a real service provider.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms previously certified a capability that could not work in any host, and the way they did
/// it is worth keeping in view.</b> Each one registered a fake <c>IDbConnection</c> — with a comment saying
/// it was "required by the constructor" — and then asserted <c>descriptor.ImplementationType</c>. Nothing
/// in the framework registers an <c>IDbConnection</c>, so the test supplied the one dependency a real host
/// never would, and asserted the SHAPE of the registration rather than resolving it. Both halves had to be
/// true for the defect to survive: had any arm called <c>GetService&lt;ICdcIdempotencyFilter&gt;()</c>
/// without the fake, it would have failed from the day it was written.
/// </para>
/// <para>
/// So every arm here now resolves the service from a built provider. The connection source is a parameter
/// of the registration, which is what removed the hidden dependency; a factory is passed so resolution can
/// be proven without a reachable server, because nothing opens a connection until an operation runs.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcIdempotencyBuilderExtensionsSqlServerShould : UnitTestBase
{
	private const string UnusedConnectionString = "Server=unused;Database=unused;";

	private static (ICdcBuilder Builder, ServiceCollection Services) NewBuilder()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// No fake IDbConnection. If one is ever needed again to make these arms pass, the hidden
		// dependency has come back.
		var builder = A.Fake<ICdcBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		return (builder, services);
	}

	[Fact]
	public void ResolveTheSqlServerFilterFromARealProvider()
	{
		var (builder, services) = NewBuilder();

		var result = builder.UseSqlServerIdempotencyFilter(UnusedConnectionString);

		result.ShouldBe(builder, "the extension returns the builder for fluent chaining");

		using var provider = services.BuildServiceProvider();

		provider.GetService<ICdcIdempotencyFilter>()
			.ShouldBeOfType<SqlServerCdcIdempotencyFilter>(
				"registration is worthless unless the service actually resolves");
	}

	[Fact]
	public void ResolveTheSqlServerFilterFromAConnectionFactory()
	{
		var (builder, services) = NewBuilder();

		_ = builder.UseSqlServerIdempotencyFilter(
			_ => () => new Microsoft.Data.SqlClient.SqlConnection(UnusedConnectionString));

		using var provider = services.BuildServiceProvider();

		provider.GetService<ICdcIdempotencyFilter>()
			.ShouldBeOfType<SqlServerCdcIdempotencyFilter>();
	}

	[Fact]
	public void RegisterOptionsValidator_WithValidateOnStart()
	{
		var (builder, services) = NewBuilder();

		_ = builder.UseSqlServerIdempotencyFilter(UnusedConnectionString);

		var validatorDescriptor = services.FirstOrDefault(
			d => d.ServiceType == typeof(IValidateOptions<SqlServerCdcIdempotencyFilterOptions>));
		validatorDescriptor.ShouldNotBeNull();
	}

	[Fact]
	public void ApplyConfigureDelegate_WhenOverloadUsed()
	{
		var (builder, services) = NewBuilder();

		_ = builder.UseSqlServerIdempotencyFilter(UnusedConnectionString, opts =>
		{
			opts.SchemaName = "CustomSchema";
			opts.RetentionPeriod = TimeSpan.FromHours(48);
			opts.CleanupBatchSize = 5000;
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcIdempotencyFilterOptions>>().Value;

		options.SchemaName.ShouldBe("CustomSchema");
		options.RetentionPeriod.ShouldBe(TimeSpan.FromHours(48));
		options.CleanupBatchSize.ShouldBe(5000);
	}

	[Fact]
	public void ReplacePriorInMemoryRegistration()
	{
		var (builder, services) = NewBuilder();

		_ = builder.UseInMemoryIdempotencyFilter();
		_ = builder.UseSqlServerIdempotencyFilter(UnusedConnectionString);

		using var provider = services.BuildServiceProvider();

		// Asserted by RESOLUTION rather than by inspecting the last descriptor: what matters is which
		// filter the processor gets, and a descriptor-order assertion can hold while resolution differs.
		provider.GetService<ICdcIdempotencyFilter>()
			.ShouldBeOfType<SqlServerCdcIdempotencyFilter>(
				"a durable filter must not be suppressed by an in-memory one registered earlier");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(
			() => CdcIdempotencyBuilderExtensions.UseSqlServerIdempotencyFilter(
				null!, UnusedConnectionString));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConnectionFactoryIsNull()
	{
		var (builder, _) = NewBuilder();

		_ = Should.Throw<ArgumentNullException>(
			() => builder.UseSqlServerIdempotencyFilter(
				(Func<IServiceProvider, Func<System.Data.IDbConnection>>)null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringIsBlank()
	{
		var (builder, _) = NewBuilder();

		// The registration cannot usefully proceed without a database, and failing here is the point of
		// taking the connection as a parameter: the error lands at the wrong call, not at the first change.
		_ = Should.Throw<ArgumentException>(
			() => builder.UseSqlServerIdempotencyFilter("   "));
	}
}
