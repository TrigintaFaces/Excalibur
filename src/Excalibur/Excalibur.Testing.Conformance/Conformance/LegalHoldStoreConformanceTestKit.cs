// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ILegalHoldStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your legal hold store implementation conforms to the ILegalHoldStore contract.
/// </para>
/// <para>
/// The test kit verifies core legal hold store operations including save lifecycle,
/// retrieval, updates, data subject holds, tenant holds, list active, list all, and expired holds.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> ILegalHoldStore implements GDPR Article 17(3) "Legal Hold Exceptions"
/// which block erasure when data must be retained for legal reasons:
/// <list type="bullet">
/// <item><description><c>SaveHoldAsync</c> THROWS InvalidOperationException on duplicate HoldId</description></item>
/// <item><description><c>SaveHoldAsync</c> and <c>UpdateHoldAsync</c> THROW ArgumentNullException on null hold</description></item>
/// <item><description><c>GetActiveHoldsForDataSubjectAsync</c> THROWS ArgumentException on null/whitespace</description></item>
/// <item><description><c>GetActiveHoldsForTenantAsync</c> THROWS ArgumentException on null/whitespace tenantId</description></item>
/// <item><description>Active vs Expired vs Released state distinctions</description></item>
/// <item><description>GetExpiredHoldsAsync returns active holds with passed ExpiresAt (excludes released)</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The kit resolves the store from a container built by the store's own registration
/// // extension, so every arm runs against the object a consumer actually gets -- including
/// // the ambient ITenantContext the extension registers. Constructing the store by hand
/// // certifies an instance you assembled rather than the one your registration produces.
/// public class SqlServerLegalHoldStoreConformanceTests : LegalHoldStoreConformanceTestKit
/// {
///     private readonly ServiceProvider _provider;
/// 
///     public SqlServerLegalHoldStoreConformanceTests(SqlServerFixture fixture) =&gt;
///         _provider = new ServiceCollection()
///             .AddLogging()
///             .AddSqlServerLegalHoldStore(o =&gt;
///             {
///                 o.ConnectionString = fixture.ConnectionString;
///                 o.AutoCreateSchema = true;
///             })
///             .BuildServiceProvider();
/// 
///     protected override ILegalHoldStore CreateStore() =&gt;
///         _provider.GetRequiredService&lt;ILegalHoldStore&gt;();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class LegalHoldStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a fresh legal hold store instance for testing.
	/// </summary>
	/// <returns>An ILegalHoldStore implementation to test.</returns>
	protected abstract ILegalHoldStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Clears residual data before an arm runs. Defaults to <see cref="CleanupAsync"/>.
	/// </summary>
	/// <returns>A task that completes when the store holds no data from a previous arm.</returns>
	/// <remarks>
	/// <para>
	/// Defaults to <see cref="CleanupAsync"/>, which is correct for any suite whose teardown only deletes
	/// rows, keys or documents. A suite whose <see cref="CleanupAsync"/> <em>also</em> disposes a
	/// connection or client MUST override this with the data-only half — otherwise it disposes the store
	/// the arm is about to use, and every arm fails on a disposed handle rather than on the contract.
	/// </para>
	/// <para>
	/// Resetting <em>before</em> an arm is what makes the arm independent; resetting only afterwards makes
	/// every arm's starting state a function of whether its predecessor finished cleanly.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// Creates the store for a single arm and clears residual data before the arm runs.
	/// </summary>
	/// <returns>A store ready for one conformance arm.</returns>
	/// <remarks>
	/// Every arm in this kit obtains its store here rather than from <see cref="CreateStore"/> directly.
	/// That is the only thing that causes <see cref="CleanupAsync"/> to run: a cleanup a deriver overrides
	/// but the kit never calls is indistinguishable, from the deriver's side, from one that works.
	/// </remarks>
	protected async Task<ILegalHoldStore> CreateStoreForArmAsync()
	{
		var store = CreateStore();
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Creates a test legal hold with the given parameters.
	/// </summary>
	/// <param name="holdId">Optional hold identifier. If not provided, a new GUID is generated.</param>
	/// <param name="dataSubjectIdHash">Optional data subject ID hash for subject-specific holds.</param>
	/// <param name="tenantId">Optional tenant identifier for multi-tenant isolation.</param>
	/// <param name="isActive">Whether the hold is active. Default is true.</param>
	/// <param name="expiresAt">Optional expiration date.</param>
	/// <returns>A test legal hold.</returns>
	protected virtual LegalHold CreateLegalHold(
		Guid? holdId = null,
		string? dataSubjectIdHash = null,
		string? tenantId = null,
		bool isActive = true,
		DateTimeOffset? expiresAt = null) =>
		new()
		{
			HoldId = holdId ?? Guid.NewGuid(),
			DataSubjectIdHash = dataSubjectIdHash,
			IdType = dataSubjectIdHash is not null ? DataSubjectIdType.UserId : null,
			TenantId = tenantId,
			Basis = LegalHoldBasis.LegalClaims,
			CaseReference = $"CASE-{Guid.NewGuid():N}",
			Description = "Test legal hold for conformance testing",
			IsActive = isActive,
			ExpiresAt = expiresAt,
			CreatedBy = "test-admin",
			CreatedAt = DateTimeOffset.UtcNow
		};

	/// <summary>
	/// Generates a unique hold ID for test isolation.
	/// </summary>
	/// <returns>A unique hold identifier.</returns>
	protected virtual Guid GenerateHoldId() => Guid.NewGuid();

	/// <summary>
	/// Generates a unique data subject ID hash for test isolation.
	/// </summary>
	/// <returns>A unique data subject ID hash.</returns>
	protected virtual string GenerateDataSubjectIdHash() => $"hash-{Guid.NewGuid():N}";

	#region Save Lifecycle Tests

	/// <summary>
	/// Verifies that saving a new hold persists it successfully.
	/// </summary>
	public virtual async Task SaveHoldAsync_ShouldPersistHold()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var hold = CreateLegalHold();

		await store.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Hold with HoldId {hold.HoldId} was not found after SaveHoldAsync");
		}

		if (retrieved.HoldId != hold.HoldId)
		{
			throw new TestFixtureAssertionException(
				$"HoldId mismatch. Expected: {hold.HoldId}, Actual: {retrieved.HoldId}");
		}

		if (retrieved.Basis != hold.Basis)
		{
			throw new TestFixtureAssertionException(
				$"Basis mismatch. Expected: {hold.Basis}, Actual: {retrieved.Basis}");
		}

		if (retrieved.IsActive != hold.IsActive)
		{
			throw new TestFixtureAssertionException(
				$"IsActive mismatch. Expected: {hold.IsActive}, Actual: {retrieved.IsActive}");
		}
	}

	/// <summary>
	/// Verifies that saving a hold with duplicate ID throws InvalidOperationException.
	/// </summary>
	public virtual async Task SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var holdId = GenerateHoldId();
		var hold1 = CreateLegalHold(holdId: holdId);
		var hold2 = CreateLegalHold(holdId: holdId);

		await store.SaveHoldAsync(hold1, CancellationToken.None).ConfigureAwait(false);

		try
		{
			await store.SaveHoldAsync(hold2, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected InvalidOperationException for duplicate HoldId but no exception was thrown");
		}
		catch (InvalidOperationException)
		{
			// Expected - SaveHoldAsync throws on duplicate, NOT upsert
		}
	}

	/// <summary>
	/// Verifies that saving a null hold throws ArgumentNullException.
	/// </summary>
	public virtual async Task SaveHoldAsync_NullHold_ShouldThrowArgumentNullException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		try
		{
			await store.SaveHoldAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentNullException for null hold but no exception was thrown");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	#endregion

	#region Retrieval Tests

	/// <summary>
	/// Verifies that GetHoldAsync returns hold for existing ID.
	/// </summary>
	public virtual async Task GetHoldAsync_ExistingHold_ShouldReturnHold()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var hold = CreateLegalHold();

		await store.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Hold should be found by HoldId {hold.HoldId}");
		}

		if (retrieved.CaseReference != hold.CaseReference)
		{
			throw new TestFixtureAssertionException(
				$"CaseReference mismatch. Expected: {hold.CaseReference}, Actual: {retrieved.CaseReference}");
		}
	}

	/// <summary>
	/// Verifies that GetHoldAsync returns null for non-existent ID.
	/// </summary>
	public virtual async Task GetHoldAsync_NonExistent_ShouldReturnNull()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateHoldId();

		var hold = await store.GetHoldAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		if (hold is not null)
		{
			throw new TestFixtureAssertionException(
				"GetHoldAsync should return null for non-existent HoldId");
		}
	}

	#endregion

	#region Update Tests

	/// <summary>
	/// Verifies that UpdateHoldAsync updates hold and returns true.
	/// </summary>
	public virtual async Task UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var hold = CreateLegalHold();

		await store.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		// Update the hold - create a new record with modified values
		var updatedHold = hold with
		{
			Description = "Updated description",
			IsActive = false,
			ReleasedBy = "admin",
			ReleasedAt = DateTimeOffset.UtcNow,
			ReleaseReason = "Case closed"
		};

		var updated = await store.UpdateHoldAsync(updatedHold, CancellationToken.None).ConfigureAwait(false);

		if (!updated)
		{
			throw new TestFixtureAssertionException(
				"UpdateHoldAsync should return true for existing hold");
		}

		var retrieved = await store.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Hold should be found after update");
		}

		if (retrieved.Description != "Updated description")
		{
			throw new TestFixtureAssertionException(
				$"Description should be updated. Expected: 'Updated description', Actual: '{retrieved.Description}'");
		}

		if (retrieved.IsActive)
		{
			throw new TestFixtureAssertionException(
				"IsActive should be false after update");
		}
	}

	/// <summary>
	/// Verifies that UpdateHoldAsync returns false for non-existent hold.
	/// </summary>
	public virtual async Task UpdateHoldAsync_NonExistent_ShouldReturnFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var hold = CreateLegalHold();

		var updated = await store.UpdateHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		if (updated)
		{
			throw new TestFixtureAssertionException(
				"UpdateHoldAsync should return false for non-existent HoldId");
		}
	}

	/// <summary>
	/// Verifies that UpdateHoldAsync throws ArgumentNullException for null hold.
	/// </summary>
	public virtual async Task UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		try
		{
			_ = await store.UpdateHoldAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentNullException for null hold but no exception was thrown");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	#endregion

	/// <summary>
	/// Gets the <see cref="ILegalHoldQueryStore"/> sub-interface from the store via <c>GetService</c>.
	/// </summary>
	/// <param name="store">The legal hold store.</param>
	/// <returns>The query store.</returns>
	private static ILegalHoldQueryStore GetQueryStore(ILegalHoldStore store) =>
		(ILegalHoldQueryStore?)store.GetService(typeof(ILegalHoldQueryStore))
		?? throw new TestFixtureAssertionException(
			"ILegalHoldStore.GetService(typeof(ILegalHoldQueryStore)) returned null. " +
			"The store implementation must support ILegalHoldQueryStore.");

	#region Data Subject Holds Tests

	/// <summary>
	/// Verifies that GetActiveHoldsForDataSubjectAsync returns active holds for data subject.
	/// </summary>
	public virtual async Task GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var dataSubjectIdHash = GenerateDataSubjectIdHash();

		// Active hold for data subject
		var activeHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, isActive: true);
		await store.SaveHoldAsync(activeHold, CancellationToken.None).ConfigureAwait(false);

		// Released hold for same data subject
		var releasedHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, isActive: false);
		await store.SaveHoldAsync(releasedHold, CancellationToken.None).ConfigureAwait(false);

		var holds = await GetQueryStore(store).GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, null, CancellationToken.None).ConfigureAwait(false);

		if (!holds.Any(h => h.HoldId == activeHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Active hold should be returned for data subject");
		}

		if (holds.Any(h => h.HoldId == releasedHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Released hold should NOT be returned for data subject");
		}
	}

	/// <summary>
	/// Verifies that GetActiveHoldsForDataSubjectAsync filters by tenant correctly.
	/// </summary>
	public virtual async Task GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var dataSubjectIdHash = GenerateDataSubjectIdHash();

		// Hold for tenant-A
		var tenantAHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, tenantId: "tenant-A", isActive: true);
		await store.SaveHoldAsync(tenantAHold, CancellationToken.None).ConfigureAwait(false);

		// Hold for tenant-B
		var tenantBHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, tenantId: "tenant-B", isActive: true);
		await store.SaveHoldAsync(tenantBHold, CancellationToken.None).ConfigureAwait(false);

		var holds = await GetQueryStore(store).GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, "tenant-A", CancellationToken.None)
			.ConfigureAwait(false);

		if (!holds.Any(h => h.HoldId == tenantAHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Tenant-A hold should be returned when filtering by tenant-A");
		}

		// Note: tenant-B holds should NOT be returned when filtering for tenant-A
		if (holds.Any(h => h.HoldId == tenantBHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Tenant-B hold should NOT be returned when filtering by tenant-A");
		}
	}

	/// <summary>
	/// Verifies that a hold belonging to no tenant is reachable, and that naming a tenant does not widen
	/// the result to another tenant's holds.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A hold with no tenant is a preservation order that applies across the deployment rather than within
	/// one tenant, and a legal hold blocks erasure: a global order that no query returns is an order that
	/// silently stops blocking. The sibling tenant-filter arm plants tenant-A against tenant-B and so never
	/// creates one, which leaves the whole untenanted row shape unexercised — a store could drop global
	/// holds entirely and every arm in this kit would stay green.
	/// </para>
	/// <para>
	/// The two halves are what stop each other passing vacuously. LIVENESS: an unscoped read returns the
	/// global hold, so a store cannot satisfy the arm by returning nothing. SAFETY: a scoped read does not
	/// return another tenant's hold, so a store cannot satisfy it by returning everything.
	/// </para>
	/// <para>
	/// Whether a caller who names their own tenant ALSO sees the global hold is pinned separately, by
	/// <see cref="GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeVisibleToScopedCaller"/>. This arm
	/// stays on the unscoped read so the two directions fail independently: a store that lost global holds
	/// only on the scoped path would still be caught, and by exactly one arm.
	/// </para>
	/// </remarks>
	/// <returns> A task representing the asynchronous operation. </returns>
	public virtual async Task GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeReachableUnscoped()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var dataSubjectIdHash = GenerateDataSubjectIdHash();

		// A preservation order that names no tenant: it applies to the data subject wherever they appear.
		var globalHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, tenantId: null, isActive: true);
		await store.SaveHoldAsync(globalHold, CancellationToken.None).ConfigureAwait(false);

		var otherTenantHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, tenantId: "tenant-B", isActive: true);
		await store.SaveHoldAsync(otherTenantHold, CancellationToken.None).ConfigureAwait(false);

		// LIVENESS: the global order is readable. Without this half, a store that returns nothing at all
		// passes the safety half below having demonstrated nothing.
		var unscoped = await GetQueryStore(store)
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, null, CancellationToken.None)
			.ConfigureAwait(false);

		if (!unscoped.Any(h => h.HoldId == globalHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"An active legal hold with no tenant ({globalHold.HoldId}) was saved and an unscoped read for "
				+ "the same data subject did not return it. A legal hold blocks erasure, so a preservation "
				+ "order this store will not surface is one that has silently stopped blocking.");
		}

		// SAFETY: naming a tenant does not widen the answer to a different tenant's order.
		var scoped = await GetQueryStore(store)
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, "tenant-A", CancellationToken.None)
			.ConfigureAwait(false);

		if (scoped.Any(h => h.HoldId == otherTenantHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"A read scoped to tenant-A returned tenant-B's hold ({otherTenantHold.HoldId}). The caller's "
				+ "tenant argument can only narrow the result; a store that widens it discloses one tenant's "
				+ "legal matters to another.");
		}
	}

	/// <summary>
	/// Verifies that a caller who names their own tenant still sees a hold that belongs to no tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A hold with no tenant is a preservation order in force across the whole deployment, and a legal
	/// hold BLOCKS erasure. A store whose tenant argument removes those rows therefore reports "nothing is
	/// blocking" to a tenant-scoped erasure check that is in fact covered by a live order, and the
	/// deletion that follows cannot be undone.
	/// </para>
	/// <para>
	/// The two failure directions are not comparable, which is why the tenant argument widens rather than
	/// the global term narrowing: missing a hold destroys preserved data permanently, while seeing an
	/// extra one delays a deletion until someone releases the hold or re-scopes the query.
	/// </para>
	/// <para>
	/// The halves stop each other passing vacuously, and each reds under its own mutation. LIVENESS: a
	/// store that drops untenanted rows from the scoped read fails here and leaves SAFETY untouched.
	/// SAFETY: a store that "fixes" liveness by ignoring the tenant argument altogether fails here and
	/// leaves LIVENESS untouched. Neither mutation can satisfy both.
	/// </para>
	/// </remarks>
	/// <returns> A task representing the asynchronous operation. </returns>
	public virtual async Task GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeVisibleToScopedCaller()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var dataSubjectIdHash = GenerateDataSubjectIdHash();
		var tenantA = $"tenant-A-{Guid.NewGuid():N}";
		var tenantB = $"tenant-B-{Guid.NewGuid():N}";

		// A preservation order naming no tenant: it is in force for tenant A as much as for anyone.
		var globalHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, tenantId: null, isActive: true);
		await store.SaveHoldAsync(globalHold, CancellationToken.None).ConfigureAwait(false);

		// Another tenant's order for the same data subject. Without this row the arm is satisfied by a
		// store that returns everything, which is the opposite defect.
		var otherTenantHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, tenantId: tenantB, isActive: true);
		await store.SaveHoldAsync(otherTenantHold, CancellationToken.None).ConfigureAwait(false);

		var scoped = await GetQueryStore(store)
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, tenantA, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS -- the global order survives the caller's tenant argument.
		if (!scoped.Any(h => h.HoldId == globalHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"A read scoped to '{tenantA}' did not return the active legal hold that belongs to no tenant "
				+ $"({globalHold.HoldId}). A hold with no tenant is in force for every tenant, and a legal hold "
				+ "blocks erasure: a caller who names their own tenant and is told nothing is blocking will "
				+ "proceed with a deletion that cannot be undone. The caller's tenant argument must admit the "
				+ "holds that belong to no tenant, exactly as the store's own ambient term does.");
		}

		// SAFETY -- widening to admit global holds must not widen to another tenant's holds.
		if (scoped.Any(h => h.HoldId == otherTenantHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"A read scoped to '{tenantA}' returned '{tenantB}'s hold ({otherTenantHold.HoldId}). Admitting "
				+ "untenanted holds must not become admitting everything: a store that returns every tenant's "
				+ "holds discloses one tenant's legal matters to another.");
		}
	}

	/// <summary>
	/// Verifies that GetActiveHoldsForDataSubjectAsync throws ArgumentException for null/whitespace.
	/// </summary>
	public virtual async Task GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		try
		{
			_ = await GetQueryStore(store).GetActiveHoldsForDataSubjectAsync(null!, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentException for null dataSubjectIdHash but no exception was thrown");
		}
		catch (ArgumentException)
		{
			// Expected - ArgumentException or ArgumentNullException
		}
	}

	#endregion

	#region Tenant Holds Tests

	/// <summary>
	/// Verifies that GetActiveHoldsForTenantAsync is genuinely scoped to the requested tenant.
	/// </summary>
	/// <remarks>
	/// Seeds two tenants and asserts both halves of the contract: SAFETY -- a scoped read never
	/// returns another tenant's holds; and LIVENESS -- the scoped read still returns its own
	/// tenant's active hold, and still filters released holds by state. A single-tenant fixture
	/// cannot distinguish a correctly scoped store from one that ignores the tenant argument.
	/// </remarks>
	public virtual async Task GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var tenantA = $"tenant-A-{Guid.NewGuid():N}";
		var tenantB = $"tenant-B-{Guid.NewGuid():N}";

		// Active hold for tenant A -- the row a scoped read MUST return.
		var activeHold = CreateLegalHold(tenantId: tenantA, isActive: true);
		await store.SaveHoldAsync(activeHold, CancellationToken.None).ConfigureAwait(false);

		// Released hold for tenant A -- filtered out by state, not by tenant.
		var releasedHold = CreateLegalHold(tenantId: tenantA, isActive: false);
		await store.SaveHoldAsync(releasedHold, CancellationToken.None).ConfigureAwait(false);

		// ACTIVE hold belonging to a DIFFERENT tenant. Without this row the whole test is
		// satisfied by a store that ignores tenantId entirely: with only one tenant present,
		// "returns the tenant's holds" and "returns every hold" are indistinguishable.
		var otherTenantHold = CreateLegalHold(tenantId: tenantB, isActive: true);
		await store.SaveHoldAsync(otherTenantHold, CancellationToken.None).ConfigureAwait(false);

		var holds = await GetQueryStore(store).GetActiveHoldsForTenantAsync(tenantA, CancellationToken.None).ConfigureAwait(false);

		// SAFETY -- a read scoped to tenant A must never surface tenant B's hold. This is the arm
		// the kit was missing, and its absence is why a store that drops the tenantId predicate
		// could be certified as conforming. Identity is asserted on the hold's OWN HoldId, never
		// on the TenantId field of the returned row: a store that leaks the row but rewrites the
		// tenant label would evade a predicate written against TenantId.
		var leaked = holds.Where(h => h.HoldId == otherTenantHold.HoldId).ToList();

		if (leaked.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"CROSS-TENANT DISCLOSURE: GetActiveHoldsForTenantAsync scoped to '{tenantA}' returned "
				+ $"{leaked.Count} legal hold(s) belonging to '{tenantB}'. A scoped read must never return "
				+ "another tenant's legal holds. Hold IDs returned: "
				+ string.Join(", ", leaked.Select(h => h.HoldId)));
		}

		// LIVENESS -- paired with the safety arm above and NOT optional. Safety alone is fully
		// satisfied by a store that returns an empty set for every scoped read forever, which
		// discloses nothing because it answers nothing.
		if (!holds.Any(h => h.HoldId == activeHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"Active hold for tenant '{tenantA}' was NOT returned by its own scoped read. The tenant "
				+ "predicate is over-filtering: a store that returns nothing satisfies isolation trivially "
				+ "while being useless.");
		}

		// LIVENESS -- state filtering still works, and is a separate property from tenant scoping.
		if (holds.Any(h => h.HoldId == releasedHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Released hold should NOT be returned for tenant");
		}
	}

	/// <summary>
	/// Verifies that a tenant-scoped read of tenant-wide holds still sees a hold that belongs to no tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the sibling of the data-subject arm, on the path that carries the greater risk. A
	/// tenant-wide hold names no data subject, so the subject-scoped query can never return one - a null
	/// does not equal a value. If this read also drops the untenanted rows, then an estate-wide
	/// preservation order is reachable through NO query the erasure check makes, and the check reports
	/// nothing blocking with complete confidence.
	/// </para>
	/// <para>
	/// LIVENESS and SAFETY red independently, under the same pair of mutations as the data-subject arm:
	/// dropping untenanted rows fails liveness alone, ignoring the tenant argument fails safety alone.
	/// </para>
	/// </remarks>
	/// <returns> A task representing the asynchronous operation. </returns>
	public virtual async Task GetActiveHoldsForTenantAsync_GlobalHold_ShouldBeVisibleToScopedCaller()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var tenantA = $"tenant-A-{Guid.NewGuid():N}";
		var tenantB = $"tenant-B-{Guid.NewGuid():N}";

		// An estate-wide preservation order: no tenant, and no data subject either.
		var globalHold = CreateLegalHold(tenantId: null, isActive: true);
		await store.SaveHoldAsync(globalHold, CancellationToken.None).ConfigureAwait(false);

		var otherTenantHold = CreateLegalHold(tenantId: tenantB, isActive: true);
		await store.SaveHoldAsync(otherTenantHold, CancellationToken.None).ConfigureAwait(false);

		var scoped = await GetQueryStore(store)
			.GetActiveHoldsForTenantAsync(tenantA, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS -- the estate-wide order is in force for this tenant and must be returned to it.
		if (!scoped.Any(h => h.HoldId == globalHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"A read of tenant-wide holds scoped to '{tenantA}' did not return the active legal hold that "
				+ $"belongs to no tenant ({globalHold.HoldId}). A tenant-wide hold carries no data subject, so "
				+ "the subject-scoped query cannot return it either -- an estate-wide preservation order this "
				+ "read omits is reachable through no query the erasure check makes, and the erasure it was "
				+ "filed to prevent proceeds.");
		}

		// SAFETY -- still no cross-tenant disclosure.
		if (scoped.Any(h => h.HoldId == otherTenantHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"A read of tenant-wide holds scoped to '{tenantA}' returned '{tenantB}'s hold "
				+ $"({otherTenantHold.HoldId}). Admitting untenanted holds must not become admitting "
				+ "everything.");
		}
	}

	/// <summary>
	/// Verifies that GetActiveHoldsForTenantAsync throws ArgumentException for null/whitespace tenantId.
	/// </summary>
	public virtual async Task GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		try
		{
			_ = await GetQueryStore(store).GetActiveHoldsForTenantAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentException for null tenantId but no exception was thrown");
		}
		catch (ArgumentException)
		{
			// Expected - ArgumentException or ArgumentNullException
		}
	}

	#endregion

	#region List Active Tests

	/// <summary>
	/// Verifies that ListActiveHoldsAsync returns all active non-expired holds ordered by CreatedAt desc.
	/// </summary>
	public virtual async Task ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Create holds with different timestamps
		var olderHold = CreateLegalHold(isActive: true);
		await store.SaveHoldAsync(olderHold, CancellationToken.None).ConfigureAwait(false);

		// Small delay to ensure different CreatedAt
		await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);

		var newerHold = CreateLegalHold(isActive: true);
		await store.SaveHoldAsync(newerHold, CancellationToken.None).ConfigureAwait(false);

		// Released hold should not appear
		var releasedHold = CreateLegalHold(isActive: false);
		await store.SaveHoldAsync(releasedHold, CancellationToken.None).ConfigureAwait(false);

		var holds = await GetQueryStore(store).ListActiveHoldsAsync(null, CancellationToken.None).ConfigureAwait(false);

		if (!holds.Any(h => h.HoldId == olderHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Older active hold should be returned");
		}

		if (!holds.Any(h => h.HoldId == newerHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Newer active hold should be returned");
		}

		if (holds.Any(h => h.HoldId == releasedHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Released hold should NOT be returned in active list");
		}

		// Verify ordering (newest first)
		var newerIndex = -1;
		var olderIndex = -1;
		for (var i = 0; i < holds.Count; i++)
		{
			if (holds[i].HoldId == newerHold.HoldId)
			{
				newerIndex = i;
			}

			if (holds[i].HoldId == olderHold.HoldId)
			{
				olderIndex = i;
			}
		}

		if (newerIndex >= 0 && olderIndex >= 0 && newerIndex > olderIndex)
		{
			throw new TestFixtureAssertionException(
				"Holds should be ordered by CreatedAt descending (newest first)");
		}
	}

	/// <summary>
	/// Verifies that ListActiveHoldsAsync filters by tenant correctly.
	/// </summary>
	public virtual async Task ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var tenantA = $"tenant-A-{Guid.NewGuid():N}";
		var tenantB = $"tenant-B-{Guid.NewGuid():N}";

		var tenantAHold = CreateLegalHold(tenantId: tenantA, isActive: true);
		await store.SaveHoldAsync(tenantAHold, CancellationToken.None).ConfigureAwait(false);

		var tenantBHold = CreateLegalHold(tenantId: tenantB, isActive: true);
		await store.SaveHoldAsync(tenantBHold, CancellationToken.None).ConfigureAwait(false);

		var holds = await GetQueryStore(store).ListActiveHoldsAsync(tenantA, CancellationToken.None).ConfigureAwait(false);

		if (!holds.Any(h => h.HoldId == tenantAHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Tenant-A hold should be returned when filtering by tenant-A");
		}

		if (holds.Any(h => h.HoldId == tenantBHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Tenant-B hold should NOT be returned when filtering by tenant-A");
		}
	}

	#endregion

	#region List All Tests

	/// <summary>
	/// Verifies that ListAllHoldsAsync includes released holds.
	/// </summary>
	public virtual async Task ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var tenantId = $"tenant-{Guid.NewGuid():N}";

		var activeHold = CreateLegalHold(tenantId: tenantId, isActive: true);
		await store.SaveHoldAsync(activeHold, CancellationToken.None).ConfigureAwait(false);

		var releasedHold = CreateLegalHold(tenantId: tenantId, isActive: false);
		await store.SaveHoldAsync(releasedHold, CancellationToken.None).ConfigureAwait(false);

		var holds = await GetQueryStore(store).ListAllHoldsAsync(tenantId, null, null, CancellationToken.None).ConfigureAwait(false);

		if (!holds.Any(h => h.HoldId == activeHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Active hold should be returned in ListAllHoldsAsync");
		}

		if (!holds.Any(h => h.HoldId == releasedHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Released hold should be returned in ListAllHoldsAsync");
		}
	}

	/// <summary>
	/// Verifies that ListAllHoldsAsync filters by date range correctly.
	/// </summary>
	public virtual async Task ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var now = DateTimeOffset.UtcNow;

		// Old hold (created 10 days ago simulation - we'll filter it out)
		var oldHold = new LegalHold
		{
			HoldId = GenerateHoldId(),
			Basis = LegalHoldBasis.LegalClaims,
			CaseReference = $"CASE-{Guid.NewGuid():N}",
			Description = "Old hold",
			IsActive = true,
			CreatedBy = "test-admin",
			CreatedAt = now.AddDays(-10)
		};
		await store.SaveHoldAsync(oldHold, CancellationToken.None).ConfigureAwait(false);

		// Recent hold
		var recentHold = CreateLegalHold(isActive: true);
		await store.SaveHoldAsync(recentHold, CancellationToken.None).ConfigureAwait(false);

		// Query for recent holds only (last day)
		var holds = await GetQueryStore(store).ListAllHoldsAsync(null, now.AddDays(-1), now.AddDays(1), CancellationToken.None).ConfigureAwait(false);

		if (!holds.Any(h => h.HoldId == recentHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Recent hold should be returned within date range");
		}

		if (holds.Any(h => h.HoldId == oldHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Old hold should NOT be returned outside date range");
		}
	}

	#endregion

	#region Expired Holds Tests

	/// <summary>
	/// Verifies that GetExpiredHoldsAsync returns active holds with passed expiration.
	/// </summary>
	public virtual async Task GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Expired hold (active but ExpiresAt in past)
		var expiredHold = CreateLegalHold(isActive: true, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));
		await store.SaveHoldAsync(expiredHold, CancellationToken.None).ConfigureAwait(false);

		// Non-expired hold (ExpiresAt in future)
		var validHold = CreateLegalHold(isActive: true, expiresAt: DateTimeOffset.UtcNow.AddDays(30));
		await store.SaveHoldAsync(validHold, CancellationToken.None).ConfigureAwait(false);

		// Hold with no expiration
		var indefiniteHold = CreateLegalHold(isActive: true, expiresAt: null);
		await store.SaveHoldAsync(indefiniteHold, CancellationToken.None).ConfigureAwait(false);

		var expiredHolds = await GetQueryStore(store).GetExpiredHoldsAsync(CancellationToken.None).ConfigureAwait(false);

		if (!expiredHolds.Any(h => h.HoldId == expiredHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Expired hold should be returned by GetExpiredHoldsAsync");
		}

		if (expiredHolds.Any(h => h.HoldId == validHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Valid (non-expired) hold should NOT be returned by GetExpiredHoldsAsync");
		}

		if (expiredHolds.Any(h => h.HoldId == indefiniteHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Indefinite hold (no ExpiresAt) should NOT be returned by GetExpiredHoldsAsync");
		}
	}

	/// <summary>
	/// Verifies that GetExpiredHoldsAsync excludes released holds.
	/// </summary>
	public virtual async Task GetExpiredHoldsAsync_ShouldExcludeReleasedHolds()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Released hold with past expiration (should NOT be returned)
		var releasedExpiredHold = CreateLegalHold(isActive: false, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));
		await store.SaveHoldAsync(releasedExpiredHold, CancellationToken.None).ConfigureAwait(false);

		// Active expired hold (should be returned)
		var activeExpiredHold = CreateLegalHold(isActive: true, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));
		await store.SaveHoldAsync(activeExpiredHold, CancellationToken.None).ConfigureAwait(false);

		var expiredHolds = await GetQueryStore(store).GetExpiredHoldsAsync(CancellationToken.None).ConfigureAwait(false);

		if (expiredHolds.Any(h => h.HoldId == releasedExpiredHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Released hold should NOT be returned by GetExpiredHoldsAsync (only active expired holds)");
		}

		if (!expiredHolds.Any(h => h.HoldId == activeExpiredHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Active expired hold should be returned by GetExpiredHoldsAsync");
		}
	}

	#endregion

}
