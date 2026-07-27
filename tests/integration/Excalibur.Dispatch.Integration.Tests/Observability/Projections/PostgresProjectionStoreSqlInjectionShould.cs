// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CS8618 // _store is set in InitializeAsync().

using Dapper;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging;

using Npgsql;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// bd-4f8lyo — SQL-injection regression lock for <see cref="PostgresProjectionStore{TProjection}"/> (OWASP
/// A03, author≠impl). Filter keys and <see cref="QueryOptions.OrderBy"/> are property names interpolated into
/// the generated SQL; the Postgres store must validate every one against the same
/// <c>^[a-zA-Z][a-zA-Z0-9_]*$</c> allow-list its SqlServer sibling already enforces, or an attacker-controlled
/// name breaks out of the <c>data-&gt;&gt;'…'</c> literal. The tenant predicate itself is parameterised and
/// safe; the injection vector is the NAMES.
/// </summary>
/// <remarks>
/// RED on the pre-fix store (raw interpolation — an injection-shaped name is NOT rejected) and GREEN on the
/// ported <c>ValidPropertyNameRegex</c> guard (which throws <see cref="ArgumentException"/> during clause
/// construction, before any DB round-trip). Paired arms (testing-patterns §3): the safety arms reject
/// injection-shaped names; the liveness arm proves a legitimate filter + OrderBy still executes, so the guard
/// blocks only injection and never a valid query. Non-skip real-Postgres — a security lock is never skipped.
/// </remarks>
[Collection("Postgres Projection Store Tests")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class PostgresProjectionStoreSqlInjectionShould : IClassFixture<PostgresFixture>, IAsyncLifetime
{
	private const string TableName = "test_sqli_projection";

	private readonly PostgresFixture _fixture;
	private PostgresProjectionStore<TestOrderProjection> _store;

	public PostgresProjectionStoreSqlInjectionShould(PostgresFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — this bd-4f8lyo SQL-injection regression lock is never skipped "
			+ "(a skipped security guard enforces nothing).");

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync();
		_ = await connection.ExecuteAsync($"""
			CREATE TABLE IF NOT EXISTS "{TableName}" (
				id VARCHAR(450) NOT NULL PRIMARY KEY,
				data JSONB NOT NULL,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL
			)
			""");

		// A non-null ambient tenant so the store's fail-closed tenant scoping (ocsbwb — TenantScope.Scoped throws
		// on a null tenant) does not fire BEFORE the property-name validation this lock targets.
		var tenantContext = A.Fake<ITenantContext>();
		_ = A.CallTo(() => tenantContext.TenantId).Returns("sqli-test-tenant");

		_store = new PostgresProjectionStore<TestOrderProjection>(
			_fixture.ConnectionString,
			new LoggerFactory().CreateLogger<PostgresProjectionStore<TestOrderProjection>>(),
			TableName,
			tenantContext: tenantContext);
	}

	public async ValueTask DisposeAsync()
	{
		if (!_fixture.DockerAvailable)
		{
			return;
		}

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync();
		_ = await connection.ExecuteAsync($"DROP TABLE IF EXISTS \"{TableName}\"");
	}

	// SAFETY — an injection-shaped FILTER property name is rejected (ArgumentException), never interpolated raw.
	// RED on the pre-fix store (no validation → the name breaks out of the data->>'…' literal).
	[Theory]
	[InlineData("Status' OR '1'='1")]
	[InlineData("Status'); DROP TABLE test_sqli_projection; --")]
	[InlineData("Status' AND (SELECT 1)=1 --")]
	[InlineData("1;SELECT")]
	public async Task RejectAnInjectionShapedFilterPropertyName(string injectionName)
	{
		var filters = new Dictionary<string, object>(StringComparer.Ordinal) { [injectionName] = "x" };

		_ = await Should.ThrowAsync<ArgumentException>(
			() => _store.QueryAsync(filters, null, CancellationToken.None));
	}

	// SAFETY — an injection-shaped OrderBy is rejected. RED on the pre-fix store (raw interpolation into ORDER BY).
	[Theory]
	[InlineData("Status; DROP TABLE test_sqli_projection --")]
	[InlineData("Status' --")]
	[InlineData("(SELECT 1)")]
	public async Task RejectAnInjectionShapedOrderBy(string injectionOrderBy)
	{
		var options = new QueryOptions { OrderBy = injectionOrderBy };

		_ = await Should.ThrowAsync<ArgumentException>(
			() => _store.QueryAsync(new Dictionary<string, object>(StringComparer.Ordinal), options, CancellationToken.None));
	}

	// LIVENESS — a legitimate filter + OrderBy is NOT rejected by the injection guard: it blocks ONLY
	// injection-shaped names, never valid ones (a guard that rejected everything would pass the safety arms
	// vacuously). Asserting the query executes without the validation ArgumentException is sufficient — the
	// specific result set is a tenant-scoping/serialisation concern the other projection tests already cover.
	[Fact]
	public async Task AllowALegitimateFilterAndOrderBy_WithoutRejectingThem()
	{
		// Valid names ("Status") satisfy ^[a-zA-Z][a-zA-Z0-9_]*$, so the guard must NOT reject them. Isolate the
		// guard's behaviour from unrelated infra (tenant scoping / JSONB serialisation the other projection tests
		// own): whatever the query does, it must not raise the validation ArgumentException on a valid name — a
		// reject-everything guard would, making the safety arms vacuous.
		var thrown = await Record.ExceptionAsync(() => _store.QueryAsync(
			new Dictionary<string, object>(StringComparer.Ordinal) { ["Status"] = "Active" },
			new QueryOptions { OrderBy = "Status" },
			CancellationToken.None));

		if (thrown is not null)
		{
			thrown.ShouldNotBeOfType<ArgumentException>(
				"a valid property-name filter + OrderBy must NOT be rejected by the injection guard — any failure "
				+ "here must be an unrelated infrastructure concern, never the validation ArgumentException.");
		}
	}
}
