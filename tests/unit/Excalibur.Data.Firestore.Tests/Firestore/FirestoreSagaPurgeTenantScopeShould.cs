// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Firestore;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Behavioral lock on tenant scoping in <see cref="FirestoreSagaStore"/>'s <c>PurgeCompletedBeforeAsync</c>
/// (bead vtklu8): every tenant scope is admitted, none is refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>History.</b> This purge used to gate on "the scope does not name the host's one partition",
/// refusing a real, named
/// tenant with a tenant-scope <see cref="NotSupportedException"/> on the stated grounds that the store had no
/// tenant discriminator to filter on. That premise no longer holds: the tenant is a first-class document
/// field, so the purge now applies it as a real <c>WhereEqualTo("tenantId", ...)</c> predicate
/// (<see cref="FirestoreSagaPurgeShould.Purge_AppliesATenantEqualityPredicate_WhenScopedToATenant"/> pins
/// the predicate structurally) instead of refusing. There is no longer a tenancy reason to refuse ANY
/// caller, including one scoped to a real, named tenant.
/// </para>
/// <para>
/// <b>Why this file exists rather than asserting against source text.</b> Firestore's query path runs
/// through sealed SDK types that cannot be faked, so the deletion round-trip is emulator-deferred (as in
/// <c>FirestoreSagaPurgeShould</c>). But the tenancy DECISION — refuse or proceed — runs before
/// <c>EnsureInitializedAsync</c> and before any SDK call, and the parameterless-database constructor
/// connects to nothing. So that decision is reachable from a unit test even though the query behind it is
/// not: the arms below assert the exception <em>type</em> (or its absence) rather than success. A call
/// that proceeds still fails downstream for want of a live Firestore project, and that is fine — being
/// admitted is the property under test.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> restore a gate that admits only the untenanted sentinel and the single-tenant
/// default identity and refuses everything else — the real-tenant arm goes RED (it throws again instead
/// of proceeding). The scope property that gate was built on has since been deleted for want of any
/// consumer, so the mutant now has to be written out by hand; that it is no longer reachable through a
/// shipped member is the stronger form of this lock.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
public sealed class FirestoreSagaPurgeTenantScopeShould
{
	private static readonly DateTimeOffset Threshold = DateTimeOffset.UtcNow.AddDays(-30);

	[Fact]
	public async Task AdmitAPurgeScopedToARealTenant()
	{
		// RED against the pre-fix code, which refused this exact scope with TenantScopeNotSupportedException.
		var store = CreateStore(new FixedTenantContext("tenant-a"));

		await ShouldNotBeRefusedOnTenancyGrounds(store).ConfigureAwait(false);
	}

	[Fact]
	public async Task AdmitAPurgeFromASingleTenantHost()
	{
		var store = CreateStore(TestTenantContext.SingleTenant);

		await ShouldNotBeRefusedOnTenancyGrounds(store).ConfigureAwait(false);
	}

	[Fact]
	public async Task AdmitAPurgeFromTheUntenantedPartition()
	{
		var store = CreateStore(TestTenantContext.Untenanted);

		await ShouldNotBeRefusedOnTenancyGrounds(store).ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts the call was never refused on tenancy grounds, without requiring the purge behind it to
	/// succeed.
	/// </summary>
	/// <remarks>
	/// A call that proceeds reaches the Firestore SDK and fails there for want of a live project — so "did
	/// not throw at all" is not the property to assert and would make this arm unrunnable off an emulator.
	/// The property is narrower and exact: whatever happens next, it is not a tenancy refusal.
	/// </remarks>
	/// <param name="store">The store under test.</param>
	private static async Task ShouldNotBeRefusedOnTenancyGrounds(FirestoreSagaStore store)
	{
		try
		{
			_ = await store.PurgeCompletedBeforeAsync(Threshold, CancellationToken.None).ConfigureAwait(false);
		}
		catch (NotSupportedException ex)
		{
			throw new ShouldAssertException(
				"the scoped purge refused a caller on tenancy grounds. The tenant is a first-class " +
				"document field now, so this purge must filter on it rather than refuse. Refusal message " +
				"was: " + ex.Message);
		}
		catch (Exception)
		{
			// Admitted, then failed downstream on the absent Firestore project. That is the pass condition:
			// the tenancy decision is the subject, not the SDK round-trip.
		}
	}

	private static FirestoreSagaStore CreateStore(ITenantContext tenantContext) => new(
		Options.Create(new FirestoreSagaOptions
		{
			ProjectId = "excalibur-test",
			CollectionName = "sagas",
			// Points the SDK at a port nothing is listening on, so an admitted call fails fast locally
			// instead of reaching for ambient cloud credentials.
			EmulatorHost = "127.0.0.1:1",
		}),
		A.Fake<ILogger<FirestoreSagaStore>>(),
		new DispatchJsonSerializer(),
		tenantContext);

	/// <summary>A context pinned to a real, named tenant — the case the store must no longer refuse.</summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
