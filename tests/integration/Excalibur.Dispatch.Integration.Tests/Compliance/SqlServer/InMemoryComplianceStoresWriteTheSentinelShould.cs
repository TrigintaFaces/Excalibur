// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Binds the in-memory compliance stores to the term the relational stores write: for the same input, both
/// providers must store the same spelling of "no tenant".
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the assertion is a COMPARISON and not a constant.</b> Asserting that each store writes the
/// sentinel, separately, is two independent facts that can drift apart one at a time -- which is what
/// happened: the relational stores folded an absent tenant through the keyed partition and the in-memory
/// stores raw-assigned it, so the same call stored the sentinel in one and NULL in the other. A per-store
/// assertion cannot see that; only comparing the two can. Every arm below reads the term back from BOTH
/// providers, asserts they agree, and then asserts what they agree ON.
/// </para>
/// <para>
/// <b>Why the value is read back rather than taken from the return.</b> Neither save returns the stored
/// term, and a store that computed the right value and bound the wrong one would satisfy any assertion made
/// against something it handed back. These arms re-read through the store's own public API, which is the
/// only surface a consumer has.
/// </para>
/// <para>
/// <b>Real SQL Server, never skip-gated.</b> The relational half of each comparison is the column's own
/// behaviour -- NOT NULL, a DEFAULT, and an ordinal collation. Substituting a fake for that side would
/// leave the comparison asserting that two C# dictionaries agree with each other.
/// </para>
/// <para>
/// <b>What these arms are RED against.</b> The parity arms fail against the raw assignment they replaced:
/// with the in-memory store binding the caller's own NULL, the in-memory term is NULL while the relational
/// term is the sentinel, and the comparison fails on the first input. The final arm additionally fails if
/// the caller's tenant argument is widened back to admit a second spelling of absent.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class InMemoryComplianceStoresWriteTheSentinelShould : IntegrationTestBase
{
	private const string Sentinel = "__untenanted__";
	private const string RealTenant = "t-a";

	private readonly SqlServerFixture _fixture;

	public InMemoryComplianceStoresWriteTheSentinelShould(SqlServerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY + PARITY. Saving a hold whose tenant is absent, in any of its spellings, stores the SAME term
	/// in the in-memory store as in the relational one.
	/// </summary>
	[Theory]
	[InlineData(null, Sentinel)]
	[InlineData("", Sentinel)]
	[InlineData("  ", Sentinel)]
	[InlineData(Sentinel, Sentinel)]
	[InlineData(RealTenant, RealTenant)]
	public async Task StoreTheSameHoldTenantTermOnBothProviders(string? supplied, string expected)
	{
		RequireServer();

		var inMemoryHold = NewHold(supplied);
		var sqlHold = NewHold(supplied);

		var inMemory = UnscopedInMemoryHolds();
		var sql = UnscopedSqlHolds();

		await inMemory.SaveHoldAsync(inMemoryHold, TestCancellationToken);
		await sql.SaveHoldAsync(sqlHold, TestCancellationToken);

		var inMemoryTerm = (await inMemory.GetHoldAsync(inMemoryHold.HoldId, TestCancellationToken))
			.ShouldNotBeNull().TenantId;
		var sqlTerm = (await sql.GetHoldAsync(sqlHold.HoldId, TestCancellationToken))
			.ShouldNotBeNull().TenantId;

		inMemoryTerm.ShouldBe(
			sqlTerm,
			"both stores were handed the same tenant (" + Describe(supplied) + ") and must persist the same "
			+ "term. When they disagree, a hold written under one provider stops being found under the "
			+ "other, and a legal hold that is not found does not refuse an erasure -- it allows one.");

		sqlTerm.ShouldBe(expected, "an absent tenant is stored as the reserved sentinel, never as NULL.");
	}

	/// <summary>
	/// SAFETY + PARITY. The same holds for the erasure-request store, whose write path had the same split.
	/// </summary>
	[Theory]
	[InlineData(null, Sentinel)]
	[InlineData("", Sentinel)]
	[InlineData("  ", Sentinel)]
	[InlineData(Sentinel, Sentinel)]
	[InlineData(RealTenant, RealTenant)]
	public async Task StoreTheSameRequestTenantTermOnBothProviders(string? supplied, string expected)
	{
		RequireServer();

		var inMemoryRequest = NewRequest(supplied);
		var sqlRequest = NewRequest(supplied);
		var scheduled = DateTimeOffset.UtcNow.AddDays(30);

		var inMemory = UnscopedInMemoryRequests();
		var sql = UnscopedSqlRequests();

		await inMemory.SaveRequestAsync(inMemoryRequest, scheduled, TestCancellationToken);
		await sql.SaveRequestAsync(sqlRequest, scheduled, TestCancellationToken);

		var inMemoryTerm = (await inMemory.GetStatusAsync(inMemoryRequest.RequestId, TestCancellationToken))
			.ShouldNotBeNull().TenantId;
		var sqlTerm = (await sql.GetStatusAsync(sqlRequest.RequestId, TestCancellationToken))
			.ShouldNotBeNull().TenantId;

		inMemoryTerm.ShouldBe(
			sqlTerm,
			"both stores were handed the same tenant (" + Describe(supplied) + ") and must persist the same term.");

		sqlTerm.ShouldBe(expected);
	}

	/// <summary>
	/// SAFETY + PARITY on the UPDATE path, which normalises separately from the insert and so can drift on
	/// its own.
	/// </summary>
	[Theory]
	[InlineData(null, Sentinel)]
	[InlineData(RealTenant, RealTenant)]
	public async Task StoreTheSameHoldTenantTermOnBothProviders_WhenUpdating(string? supplied, string expected)
	{
		RequireServer();

		var inMemoryHold = NewHold(supplied);
		var sqlHold = NewHold(supplied);

		var inMemory = UnscopedInMemoryHolds();
		var sql = UnscopedSqlHolds();

		await inMemory.SaveHoldAsync(inMemoryHold, TestCancellationToken);
		await sql.SaveHoldAsync(sqlHold, TestCancellationToken);

		(await inMemory.UpdateHoldAsync(inMemoryHold with { Description = "revised" }, TestCancellationToken))
			.ShouldBeTrue();
		(await sql.UpdateHoldAsync(sqlHold with { Description = "revised" }, TestCancellationToken))
			.ShouldBeTrue();

		var inMemoryTerm = (await inMemory.GetHoldAsync(inMemoryHold.HoldId, TestCancellationToken))
			.ShouldNotBeNull().TenantId;
		var sqlTerm = (await sql.GetHoldAsync(sqlHold.HoldId, TestCancellationToken))
			.ShouldNotBeNull().TenantId;

		inMemoryTerm.ShouldBe(sqlTerm, "the update path must normalise the tenant the way the insert does.");
		sqlTerm.ShouldBe(expected);
	}

	/// <summary>
	/// LIVENESS. An untenanted hold is STILL returned to an untenanted reader, on both providers.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails when a store goes inert. Every safety arm above, and the narrowing arm
	/// below, are satisfied by a store that returns nothing to anybody; only this one is not. A legal hold
	/// that stops being returned does not refuse an erasure, it permits one, so "returns nothing" would be
	/// the most expensive way for this file to be green.
	/// </remarks>
	[Fact]
	public async Task StillReturnAnUntenantedHoldToAnUntenantedReader()
	{
		RequireServer();

		var subject = NewSubject();
		var inMemory = UnscopedInMemoryHolds();
		var sql = UnscopedSqlHolds();

		await inMemory.SaveHoldAsync(NewHold(tenantId: null, subject), TestCancellationToken);
		await sql.SaveHoldAsync(NewHold(tenantId: null, subject), TestCancellationToken);

		var fromMemory = await inMemory.GetActiveHoldsForDataSubjectAsync(subject, null, TestCancellationToken);
		var fromSql = await sql.GetActiveHoldsForDataSubjectAsync(subject, null, TestCancellationToken);

		fromMemory.ShouldHaveSingleItem().TenantId.ShouldBe(
			Sentinel,
			"a hold belonging to no tenant blocks erasure for everyone, so an untenanted reader must still "
			+ "see it after the tenant term stopped being spelled two ways.");
		fromSql.ShouldHaveSingleItem().TenantId.ShouldBe(Sentinel);
	}

	/// <summary>
	/// SAFETY + PARITY. A caller naming a real tenant narrows STRICTLY: an untenanted hold is not returned,
	/// on either provider.
	/// </summary>
	/// <remarks>
	/// RED against the state this change replaced, taken as a whole. Before it, the in-memory store wrote
	/// the caller's NULL through unfolded AND admitted a null tenant as a second match for the caller's
	/// argument, so this read handed back the untenanted hold while the relational store did not. Removing
	/// that second spelling is not independently observable once the fold lands in the same change -- with
	/// no write path able to produce a NULL, the disjunct matches nothing. It is removed so that it cannot
	/// quietly become live again if one ever does.
	/// </remarks>
	[Fact]
	public async Task NotReturnAnUntenantedHoldToAReaderNamingARealTenant()
	{
		RequireServer();

		var subject = NewSubject();
		var inMemory = UnscopedInMemoryHolds();
		var sql = UnscopedSqlHolds();

		await inMemory.SaveHoldAsync(NewHold(tenantId: null, subject), TestCancellationToken);
		await sql.SaveHoldAsync(NewHold(tenantId: null, subject), TestCancellationToken);

		var fromMemory = await inMemory.GetActiveHoldsForDataSubjectAsync(subject, RealTenant, TestCancellationToken);
		var fromSql = await sql.GetActiveHoldsForDataSubjectAsync(subject, RealTenant, TestCancellationToken);

		fromMemory.Count.ShouldBe(
			fromSql.Count,
			"a caller naming a tenant must narrow identically on both providers; the in-memory store "
			+ "admitting a second spelling of absent is the divergence this change removes.");

		// The caller's argument narrows to their own tenant PLUS holds belonging to no tenant, and both
		// providers agree on that. Withholding a global hold from a tenant-scoped reader does not fail
		// safe: that hold blocks this tenant's erasures, so a caller who cannot see it proceeds with an
		// erasure the hold exists to prevent. Parity is the property under test here, not emptiness.
		fromMemory.ShouldHaveSingleItem();
	}

	// ---- arrangement -------------------------------------------------------------------------------

	private void RequireServer() => _fixture.DockerAvailable.ShouldBeTrue(
		_fixture.InitializationError
		?? "SQL Server must be reachable: half of every comparison in this file is the server's own answer.");

	private static string Describe(string? tenantId) => tenantId switch
	{
		null => "null",
		"" => "empty",
		_ when string.IsNullOrWhiteSpace(tenantId) => "whitespace",
		_ => "'" + tenantId + "'"
	};

	private static string NewSubject() => "subject-" + Guid.NewGuid().ToString("N");

	private static LegalHold NewHold(string? tenantId, string? dataSubjectIdHash = null) => new()
	{
		HoldId = Guid.NewGuid(),
		DataSubjectIdHash = dataSubjectIdHash ?? NewSubject(),
		IdType = DataSubjectIdType.UserId,
		TenantId = tenantId,
		Basis = LegalHoldBasis.LegalObligation,
		CaseReference = "sentinel-parity",
		Description = "Parity arm.",
		IsActive = true,
		CreatedBy = "sentinel-parity",
		CreatedAt = DateTimeOffset.UtcNow,
	};

	private static ErasureRequest NewRequest(string? tenantId) => new()
	{
		RequestId = Guid.NewGuid(),
		DataSubjectId = NewSubject(),
		IdType = DataSubjectIdType.UserId,
		TenantId = tenantId,
		LegalBasis = ErasureLegalBasis.DataSubjectRequest,
		RequestedBy = "sentinel-parity",
		RequestedAt = DateTimeOffset.UtcNow,
	};

	// The non-multi-tenant shape on both sides: no ambient tenant is required, so the term that reaches
	// storage is the caller's own -- the only shape in which the two providers could disagree.
	private static InMemoryLegalHoldStore UnscopedInMemoryHolds() => new(
		tenantContext: UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	private static InMemoryErasureStore UnscopedInMemoryRequests() => new(
		new PassThroughDataSubjectHasher(),
		tenantContext: UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	// Fully qualified: an unqualified `Options.Create` binds to the Excalibur.Dispatch.Options NAMESPACE in
	// this file's scope, not to Microsoft's static class.
	private SqlServerLegalHoldStore UnscopedSqlHolds() => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerLegalHoldStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			TableName = "LegalHoldsSentinelParity",
			AutoCreateSchema = true,
		}),
		EnabledTestLogger.Create<SqlServerLegalHoldStore>(),
		tenantContext: UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	private SqlServerErasureStore UnscopedSqlRequests() => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerErasureStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RequestsTableName = "ErasureRequestsSentinelParity",
			CertificatesTableName = "ErasureCertificatesSentinelParity",
			AutoCreateSchema = true,
		}),
		new PassThroughDataSubjectHasher(),
		EnabledTestLogger.Create<SqlServerErasureStore>(),
		tenantContext: UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	/// <summary>
	/// Implements <see cref="IDataSubjectHasher"/> directly and inherits no first-party base, so the term
	/// under comparison is the one the store wrote rather than one a shared helper rewrote.
	/// </summary>
	private sealed class PassThroughDataSubjectHasher : IDataSubjectHasher
	{
		public string HashDataSubjectId(string dataSubjectId) => dataSubjectId;
	}
}
