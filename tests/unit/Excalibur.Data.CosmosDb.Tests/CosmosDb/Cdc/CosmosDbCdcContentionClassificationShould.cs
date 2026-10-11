// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;

using Excalibur.Cdc.CosmosDb;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb.Cdc;

/// <summary>
/// Locks which Cosmos statuses mean "a concurrent writer beat us to this checkpoint" and therefore become
/// <c>CdcPositionSaveOutcome.Contended</c>, rather than surfacing to the caller as a failure.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the obvious way to write the filter is silently wrong, and was. 449 is the status
/// Cosmos actually returns when several operations update the same item, and
/// <see cref="HttpStatusCode"/> HAS NO MEMBER FOR IT — its only member between 440 and 460 is 451. So a
/// filter spelled <c>ex.StatusCode is HttpStatusCode.Something</c> cannot name 449 even in principle, and
/// the store's own conflict-retry loop was written that way: it watched 409, which an upsert rarely
/// returns, and let 449 straight past.
/// </para>
/// <para>
/// The arm that matters is <see cref="Treat449AsContention"/>. If someone rewrites the predicate using the
/// enum for tidiness, that arm goes red and says why.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CosmosDbCdcContentionClassificationShould : UnitTestBase
{
	private static CosmosException WithStatus(int status) =>
		new("concurrent write", (HttpStatusCode)status, subStatusCode: 0, activityId: "test", requestCharge: 0);

	[Fact]
	public void Treat449AsContention()
	{
		CosmosDbCdcStateStore.IsConcurrentWriteConflict(WithStatus(449)).ShouldBeTrue(
			"449 is the status Cosmos returns when several operations update the same item, and it is the "
			+ "one that actually reaches us on an upsert. The SDK retries it with incremental back-off and "
			+ "surfaces it when that budget is spent, which is precisely a contended checkpoint.");
	}

	[Fact]
	public void NotBeExpressibleThroughTheHttpStatusCodeEnum()
	{
		// The reason the predicate compares a raw int. If a future BCL adds a 449 member this goes red,
		// which is the signal that the comparison can be simplified -- not a defect.
		Enum.IsDefined(typeof(HttpStatusCode), 449).ShouldBeFalse(
			"HttpStatusCode models the standard range and 449 sits outside it, so there is nothing to "
			+ "compare against by name. A filter written with the enum cannot match the status that occurs.");

		Enum.GetValues<HttpStatusCode>()
			.Select(v => (int)v)
			.Where(v => v is >= 440 and <= 460)
			.ShouldBe([451], "the only enum member anywhere near 449, and it is unrelated.");
	}

	[Fact]
	public void TreatConflictAsContention() =>
		CosmosDbCdcStateStore.IsConcurrentWriteConflict(WithStatus((int)HttpStatusCode.Conflict)).ShouldBeTrue(
			"the store's retry loop already treats 409 as the conflict condition, so the translation keeps "
			+ "it rather than changing behaviour the loop depends on.");

	[Fact]
	public void NotTreatThrottlingAsContention() =>
		CosmosDbCdcStateStore.IsConcurrentWriteConflict(WithStatus((int)HttpStatusCode.TooManyRequests)).ShouldBeFalse(
			"a spent throttle budget means under-provisioned throughput, not transient contention. "
			+ "Reporting it as Contended would tell the caller to carry on when what it needs is more RU/s.");

	[Fact]
	public void NotTreatPreconditionFailedAsContention() =>
		CosmosDbCdcStateStore.IsConcurrentWriteConflict(WithStatus((int)HttpStatusCode.PreconditionFailed)).ShouldBeFalse(
			"the upsert passes no ItemRequestOptions and therefore no IfMatchEtag, so there is no "
			+ "precondition to fail. An arm for 412 could never fire, and would advertise an "
			+ "optimistic-concurrency check this store does not perform.");

	[Theory]
	[InlineData((int)HttpStatusCode.Unauthorized)]
	[InlineData((int)HttpStatusCode.Forbidden)]
	[InlineData((int)HttpStatusCode.NotFound)]
	[InlineData((int)HttpStatusCode.RequestTimeout)]
	[InlineData((int)HttpStatusCode.InternalServerError)]
	[InlineData((int)HttpStatusCode.ServiceUnavailable)]
	public void LetEveryOtherFailureSurface(int status) =>
		CosmosDbCdcStateStore.IsConcurrentWriteConflict(WithStatus(status)).ShouldBeFalse(
			"an auth, routing or availability failure is not transient contention. Swallowing one as "
			+ "Contended would tell the caller the next cycle fixes it while data access is broken -- the "
			+ "mirror image of the defect this translation exists to fix.");
}
