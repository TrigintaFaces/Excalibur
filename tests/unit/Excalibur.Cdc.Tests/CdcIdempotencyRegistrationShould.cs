// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Cdc.Tests;

/// <summary>
/// Binds the CDC idempotency filter's REACHABILITY from a host that configures CDC through the
/// config-driven job path, which could not register one at all before these overloads existed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this locks.</b> The change-event processor resolves its filter optionally —
/// <c>GetService&lt;ICdcIdempotencyFilter&gt;()</c> — so a host with none registered gets CDC with no
/// deduplication and nothing reports it. The only public way to supply one used to be an extension on
/// <c>ICdcBuilder</c>, which the job path never constructs, and both the interface and its implementations
/// are internal, so a host could not register them by hand either. CDC through
/// <c>AddSqlServerCdcJob</c> therefore had no reachable path to durable dedupe.
/// </para>
/// <para>
/// The first arm is the one that matters: it asserts the ABSENCE, so it documents the silent-degradation
/// hazard rather than only the fix. If a future change starts registering a filter by default, that arm
/// fails and forces the decision to be made deliberately instead of inherited.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CdcIdempotencyRegistrationShould
{
	[Fact]
	public void ResolveNoFilterWhenNoneIsRegistered()
	{
		var services = new ServiceCollection();
		// The filters take ILogger<T>. A real host always has logging; a bare collection does not.
		_ = services.AddLogging();

		using var provider = services.BuildServiceProvider();

		// The degraded state, asserted deliberately: CDC runs without deduplication and says nothing.
		// This is what the job path got before the IServiceCollection overloads existed.
		provider.GetService<ICdcIdempotencyFilter>().ShouldBeNull(
			"a host that registers no filter must resolve none -- if this starts returning an instance, "
			+ "deduplication became a silent default and that is a decision, not an improvement");
	}

	[Fact]
	public void ResolveTheSqlServerFilterFromTheServiceCollectionOverload()
	{
		var services = new ServiceCollection();
		// The filters take ILogger<T>. A real host always has logging; a bare collection does not.
		_ = services.AddLogging();

		// No ICdcBuilder anywhere: this is exactly what a config-driven job-path host can call.
		// A factory, so the arm proves RESOLUTION without needing a reachable server: nothing opens a
		// connection until an operation runs.
		_ = services.AddSqlServerCdcIdempotencyFilter(
			_ => () => new Microsoft.Data.SqlClient.SqlConnection("Server=unused;Database=unused;"));

		using var provider = services.BuildServiceProvider();

		provider.GetService<ICdcIdempotencyFilter>()
			.ShouldBeOfType<SqlServerCdcIdempotencyFilter>(
				"the job path must be able to reach the durable filter without constructing a CDC builder");
	}

	[Fact]
	public void ResolveTheInMemoryFilterFromTheServiceCollectionOverload()
	{
		var services = new ServiceCollection();
		// The filters take ILogger<T>. A real host always has logging; a bare collection does not.
		_ = services.AddLogging();

		_ = services.AddInMemoryCdcIdempotencyFilter();

		using var provider = services.BuildServiceProvider();

		provider.GetService<ICdcIdempotencyFilter>()
			.ShouldBeOfType<InMemoryCdcIdempotencyFilter>();
	}

	[Fact]
	public void LetTheDurableFilterReplaceTheInMemoryOne()
	{
		var services = new ServiceCollection();
		// The filters take ILogger<T>. A real host always has logging; a bare collection does not.
		_ = services.AddLogging();

		// Order matters and the asymmetry is deliberate: the in-memory registration is TryAdd, the SQL
		// Server one is AddSingleton, so a durable choice wins over a process-local default however the
		// two are sequenced. Asserted because it is the shape a host hits when a shared bootstrap adds
		// the in-memory filter and an environment-specific one adds the durable filter afterwards.
		_ = services.AddInMemoryCdcIdempotencyFilter();
		// A factory, so the arm proves RESOLUTION without needing a reachable server: nothing opens a
		// connection until an operation runs.
		_ = services.AddSqlServerCdcIdempotencyFilter(
			_ => () => new Microsoft.Data.SqlClient.SqlConnection("Server=unused;Database=unused;"));

		using var provider = services.BuildServiceProvider();

		provider.GetService<ICdcIdempotencyFilter>()
			.ShouldBeOfType<SqlServerCdcIdempotencyFilter>(
				"a durable filter must not be suppressed by an in-memory one registered earlier");
	}

	[Fact]
	public void ApplyConfiguredSchemaAndTableToTheFilterOptions()
	{
		var services = new ServiceCollection();
		// The filters take ILogger<T>. A real host always has logging; a bare collection does not.
		_ = services.AddLogging();

		_ = services.AddSqlServerCdcIdempotencyFilter(
			"Server=unused;Database=unused;",
			options =>
			{
				options.SchemaName = "Audit";
				options.TableName = "SeenChanges";
			});

		using var provider = services.BuildServiceProvider();

		// The options must actually carry through, because overriding them obliges the operator to rename
		// the objects in the shipped schema script to match -- a silently-ignored override would send them
		// to rename the wrong table.
		var options = provider
			.GetRequiredService<Microsoft.Extensions.Options.IOptions<SqlServerCdcIdempotencyFilterOptions>>()
			.Value;

		options.SchemaName.ShouldBe("Audit");
		options.TableName.ShouldBe("SeenChanges");
	}
}
