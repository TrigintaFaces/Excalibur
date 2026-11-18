// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.SqlServer;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.Data.Saga;

[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
public sealed class SqlServerTimeoutExactTenantShould(SqlServerSagaTimeoutStoreContainerFixture fixture)
	: IClassFixture<SqlServerSagaTimeoutStoreContainerFixture>
{
	private const string Owner = "tenantA";

	[Theory]
	[InlineData("tenantA ")]
	[InlineData("TENANTA")]
	public async Task Cancel_and_cancel_all_require_exact_scope(string alias)
	{
		await InitializeAsync().ConfigureAwait(false);
		var owner = Create(Owner);
		var foreign = Create(alias);
		var first = NewTimeout("shared-saga");
		var second = NewTimeout("shared-saga");
		var aliasRow = NewTimeout("shared-saga");
		await owner.ScheduleTimeoutAsync(first, CancellationToken.None).ConfigureAwait(false);
		await owner.ScheduleTimeoutAsync(second, CancellationToken.None).ConfigureAwait(false);
		await foreign.ScheduleTimeoutAsync(aliasRow, CancellationToken.None).ConfigureAwait(false);
		await foreign.CancelTimeoutAsync(first.SagaId, first.TimeoutId, CancellationToken.None).ConfigureAwait(false);
		var due = await owner.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		due.Select(row => row.TimeoutId).Order().ShouldBe(new[] { first.TimeoutId, second.TimeoutId, aliasRow.TimeoutId }.Order());
		await foreign.CancelAllTimeoutsAsync(first.SagaId, CancellationToken.None).ConfigureAwait(false);
		due = await owner.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		due.Select(row => row.TimeoutId).Order().ShouldBe(new[] { first.TimeoutId, second.TimeoutId }.Order());
		await owner.CancelTimeoutAsync(first.SagaId, first.TimeoutId, CancellationToken.None).ConfigureAwait(false);
		(await owner.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false))
			.Select(row => row.TimeoutId).ShouldBe(new[] { second.TimeoutId });
		await owner.CancelAllTimeoutsAsync(first.SagaId, CancellationToken.None).ConfigureAwait(false);
		(await owner.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false)).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("tenantA ")]
	[InlineData("TENANTA")]
	public async Task Reject_counterfeit_tenant_without_consuming_the_real_claim(string alias)
	{
		await InitializeAsync().ConfigureAwait(false);
		var owner = Create(Owner);
		var foreign = Create(alias);
		var timeout = NewTimeout(Guid.NewGuid().ToString());
		await owner.ScheduleTimeoutAsync(timeout, CancellationToken.None).ConfigureAwait(false);
		var claim = (await foreign.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, CancellationToken.None).ConfigureAwait(false)).Single();
		claim.Timeout.TenantId.ShouldBe(Owner);
		var counterfeit = claim with { Timeout = claim.Timeout with { TenantId = alias } };
		counterfeit.ClaimToken.ShouldBe(claim.ClaimToken);
		(await foreign.MarkDeliveredAsync(counterfeit, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
		(await SqlServerExactTenantEvidence.ReadClaimAsync(fixture.ConnectionString, timeout.TimeoutId).ConfigureAwait(false))
			.ShouldBe(claim.ClaimToken, "refusal must preserve the row AND the original claim token");
		(await owner.MarkDeliveredAsync(claim, CancellationToken.None).ConfigureAwait(false)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
		(await owner.MarkDeliveredAsync(claim, CancellationToken.None).ConfigureAwait(false)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
		(await owner.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false)).ShouldBeEmpty();
	}

	[Fact]
	public async Task Keep_global_due_claim_and_reject_a_superseded_claim_token()
	{
		await InitializeAsync().ConfigureAwait(false);
		var owner = Create(Owner);
		var alias = Create("tenantA ");
		var a = NewTimeout("a");
		var b = NewTimeout("b");
		await owner.ScheduleTimeoutAsync(a, CancellationToken.None).ConfigureAwait(false);
		await alias.ScheduleTimeoutAsync(b, CancellationToken.None).ConfigureAwait(false);
		var due = await owner.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
		due.Select(row => row.TenantId).Order(StringComparer.Ordinal).ShouldBe(new[] { Owner, "tenantA " }.Order(StringComparer.Ordinal));
		var claims = await owner.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 2, CancellationToken.None).ConfigureAwait(false);
		claims.Select(row => row.Timeout.TimeoutId).Order().ShouldBe(new[] { a.TimeoutId, b.TimeoutId }.Order());
		(await alias.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 2, CancellationToken.None).ConfigureAwait(false)).ShouldBeEmpty();
		var old = claims.Single(row => row.Timeout.TimeoutId == a.TimeoutId);
		// Expire the actual persisted lease, not its token; avoid wall-clock sleeps and fake claim writers.
		await SqlServerExactTenantEvidence.ExpireClaimAsync(fixture.ConnectionString, old).ConfigureAwait(false);
		var current = (await alias.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 2, CancellationToken.None).ConfigureAwait(false)).Single();
		current.Timeout.TimeoutId.ShouldBe(a.TimeoutId);
		current.ClaimToken.ShouldNotBe(old.ClaimToken);
		(await owner.MarkDeliveredAsync(old, CancellationToken.None).ConfigureAwait(false)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
		(await SqlServerExactTenantEvidence.ReadClaimAsync(fixture.ConnectionString, a.TimeoutId).ConfigureAwait(false)).ShouldBe(current.ClaimToken);
		(await owner.MarkDeliveredAsync(current, CancellationToken.None).ConfigureAwait(false)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
		(await alias.MarkDeliveredAsync(claims.Single(row => row.Timeout.TimeoutId == b.TimeoutId), CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(SagaTimeoutRetirementOutcome.Retired);
		(await owner.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false)).ShouldBeEmpty();
	}

	private async Task InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue("Real SQL isolation tests must not skip unavailable infrastructure.");
		await fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await SqlServerExactTenantEvidence.RequireTimeoutCollationAsync(fixture.ConnectionString).ConfigureAwait(false);
		await fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private SqlServerSagaTimeoutStore Create(string tenant) => new(fixture.ConnectionString,
		NullLogger<SqlServerSagaTimeoutStore>.Instance, new ExactSqlTenantContext(tenant));

	private static SagaTimeout NewTimeout(string sagaId) => new(Guid.NewGuid().ToString(), sagaId,
		"exact-tenant-saga", "exact-tenant-timeout", new byte[] { 1, 7 },
		DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(-2));
}
