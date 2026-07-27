// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Saga.SqlServer.Requests;

namespace Excalibur.Data.Tests.Saga;

/// <summary>
/// Conformance for the saga purge range DELETE — the calibration exemplar whose three deliberate
/// intents each map to one sanctioned emitted-predicate shape. This is the liveness half of the
/// tenant-range-op detector: the purge must fail closed on omission AND still allow the declared
/// estate-wide sweep, so the detector neither leaks nor false-positives on a legitimate sweep.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Feature", "TenantIsolation")]
public sealed class PurgeRangeOpPredicateConformanceShould
{
	private const string QualifiedTableName = "[dbo].[Sagas]";
	private static readonly DateTimeOffset Threshold = DateTimeOffset.UnixEpoch;

	private static string EmittedSql(TenantScope scope, bool allTenants) =>
		new PurgeCompletedSagasRequest(Threshold, QualifiedTableName, default, scope, allTenants)
			.Command.CommandText;

	/// <summary>Reads the tenant value the request will actually bind, or null when none is bound.</summary>
	private static string? BoundTenant(TenantScope scope, bool allTenants)
	{
		var request = new PurgeCompletedSagasRequest(
			Threshold, QualifiedTableName, default, scope, allTenants);

		return request.Command.Parameters is Dapper.DynamicParameters parameters
			&& parameters.ParameterNames.Contains("TenantId", StringComparer.Ordinal)
				? parameters.Get<string>("TenantId")
				: null;
	}

	[Fact]
	public void FailClosed_ToTheUntenantedPartition_OnUnscopedOmission()
	{
		// SAFETY: omission (None) must delete ONLY the untenanted partition, never every tenant's rows.
		//
		// This arm asserted the OLD MECHANISM — `TenantId IS NULL`, plus a ShouldNotContain that now
		// FORBIDS the correct one. `IS NULL` was right only while the column was nullable; the column
		// carries the reserved sentinel and is never null, so omission is expressed as an equality against
		// that sentinel. Scoped and None therefore emit IDENTICAL SQL and the discriminator moved from the
		// text to the BOUND VALUE — which is why asserting on CommandText alone can no longer tell a
		// tenant-restricted purge from an untenanted one.
		var sql = EmittedSql(TenantScope.None, allTenants: false);

		sql.ShouldContain(
			"AND TenantId = @TenantId",
			Case.Insensitive,
			"an unscoped purge must still carry a tenant predicate. A DELETE with no discriminator would "
			+ "purge every tenant's completed sagas — the estate-wide sweep, reached by omission rather "
			+ "than by the caller declaring it.");

		BoundTenant(TenantScope.None, allTenants: false).ShouldBe(
			KeyedTenantPartition.Untenanted.TenantId,
			"omission must bind the reserved untenanted sentinel. If this binds a real tenant the purge "
			+ "deletes their rows; if it binds null the predicate matches nothing and the purge silently "
			+ "retains rows it was asked to remove.");
	}

	[Fact]
	public void RestrictToTheTenant_WhenScoped()
	{
		var sql = EmittedSql(TenantScope.Scoped("tenant-1"), allTenants: false);
		sql.ShouldContain("TenantId = @TenantId");
	}

	[Fact]
	public void EmitNoTenantPredicate_OnlyForTheDeclaredEstateWideSweep()
	{
		// LIVENESS (must-not-fire): the explicit, opted-in sweep is allowed to span all tenants —
		// it emits NO tenant predicate, and that is correct precisely because it was declared.
		var sweep = EmittedSql(TenantScope.None, allTenants: true);
		sweep.ShouldNotContain("TenantId IS NULL");
		sweep.ShouldNotContain("TenantId = @TenantId");
		// Non-vacuity: it is the SAME statement modulo the tenant fragment — still a real purge.
		sweep.ShouldContain("DELETE FROM");
		sweep.ShouldContain("CompletedAt");
	}
}
