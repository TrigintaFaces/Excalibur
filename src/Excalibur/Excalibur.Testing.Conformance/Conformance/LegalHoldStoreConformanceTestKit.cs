// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
	/// Verifies that a successful update increments the stored concurrency token.
	/// </summary>
	/// <remarks>
	/// The version is what makes a read-modify-write safe, so a store that accepts writes without moving
	/// it provides no protection at all — every subsequent stale write would still match. This arm is the
	/// liveness half: it also proves an uncontended update is still applied.
	/// </remarks>
	public virtual async Task UpdateHoldAsync_Succeeding_ShouldIncrementVersion()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var hold = CreateLegalHold();

		await store.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		var saved = await store.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false)
			?? throw new TestFixtureAssertionException("Hold should be found after save");

		var updated = await store.UpdateHoldAsync(
			saved with { Description = "First revision" },
			CancellationToken.None).ConfigureAwait(false);

		if (!updated)
		{
			throw new TestFixtureAssertionException(
				"UpdateHoldAsync should return true for an existing hold at the stored version");
		}

		var afterUpdate = await store.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false)
			?? throw new TestFixtureAssertionException("Hold should be found after update");

		if (afterUpdate.Version != saved.Version + 1)
		{
			throw new TestFixtureAssertionException(
				$"A successful update must increment Version. Expected: {saved.Version + 1}, "
				+ $"Actual: {afterUpdate.Version}");
		}

		if (afterUpdate.Description != "First revision")
		{
			throw new TestFixtureAssertionException(
				"The update must still apply the caller's values when the version matches");
		}
	}

	/// <summary>
	/// Verifies that an update carrying a stale concurrency token is refused, and that the stored record
	/// is left exactly as the concurrent writer left it.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails against a blind whole-record write. It reproduces the sequence that
	/// loses data: a reader takes a copy, a second writer changes the hold's expiry, and the reader then
	/// writes its stale copy back with the hold deactivated. Under a blind write the extension is gone and
	/// the hold is released; under compare-and-set the second write is refused and the extension stands.
	/// </remarks>
	public virtual async Task UpdateHoldAsync_StaleVersion_ShouldThrowAndLeaveHoldIntact()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var extendedExpiry = DateTimeOffset.UtcNow.AddYears(5);
		var hold = CreateLegalHold(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

		await store.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		// The reader's copy: taken before the concurrent write, and still carrying the version it read.
		var staleCopy = await store.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false)
			?? throw new TestFixtureAssertionException("Hold should be found after save");

		// The concurrent writer extends the expiry.
		var extended = await store.UpdateHoldAsync(
			staleCopy with { ExpiresAt = extendedExpiry },
			CancellationToken.None).ConfigureAwait(false);

		if (!extended)
		{
			throw new TestFixtureAssertionException(
				"The concurrent writer's update should have been applied");
		}

		// The reader now writes its stale copy back, deactivating the hold.
		var caught = false;
		try
		{
			_ = await store.UpdateHoldAsync(
				staleCopy with
				{
					IsActive = false,
					ReleasedBy = "conformance",
					ReleasedAt = DateTimeOffset.UtcNow,
					ReleaseReason = "expired"
				},
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (LegalHoldConcurrencyException)
		{
			caught = true;
		}

		if (!caught)
		{
			throw new TestFixtureAssertionException(
				"UpdateHoldAsync must raise LegalHoldConcurrencyException for a stale Version. "
				+ "A store that accepts the write silently releases a hold that was just extended.");
		}

		var stored = await store.GetHoldAsync(hold.HoldId, CancellationToken.None).ConfigureAwait(false)
			?? throw new TestFixtureAssertionException("Hold should still exist after a refused update");

		if (!stored.IsActive)
		{
			throw new TestFixtureAssertionException(
				"The refused update must not have been applied: the hold must still be ACTIVE");
		}

		if (stored.ExpiresAt is null || stored.ExpiresAt.Value <= DateTimeOffset.UtcNow)
		{
			throw new TestFixtureAssertionException(
				$"The concurrent writer's extended expiry must survive. Actual: {stored.ExpiresAt:O}");
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

		var holds = await GetQueryStore(store).GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, null, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

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

		var holds = await GetQueryStore(store).GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, "tenant-A", DateTimeOffset.UtcNow, CancellationToken.None)
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
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, null, DateTimeOffset.UtcNow, CancellationToken.None)
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
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, "tenant-A", DateTimeOffset.UtcNow, CancellationToken.None)
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
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, tenantA, DateTimeOffset.UtcNow, CancellationToken.None)
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
	/// Verifies that an EMPTY caller tenant argument means "no tenant supplied", identically across providers.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An empty string is not a tenant identifier: the untenanted partition is a sentinel VALUE, never
	/// <c>""</c> and never <see langword="null"/>. So <c>""</c> can only mean the caller named no tenant, and
	/// the answer must equal the answer for <see langword="null"/>. Providers disagreed on this: some read
	/// <c>""</c> as "unspecified" and left the ambient term alone, while others matched it as a literal tenant
	/// value and so returned only the holds belonging to no tenant — the same call, the same data, two
	/// different answers depending on which store was registered.
	/// </para>
	/// <para>
	/// The halves are independent. LIVENESS: the caller's own tenant hold survives an empty argument, which a
	/// store that treats <c>""</c> as a literal value fails while leaving SAFETY untouched. SAFETY: an empty
	/// argument does not become a licence to read another tenant's holds, which a store that "fixes" liveness
	/// by dropping the ambient term fails while leaving LIVENESS untouched.
	/// </para>
	/// </remarks>
	/// <returns> A task representing the asynchronous operation. </returns>
	public virtual async Task GetActiveHoldsForDataSubjectAsync_EmptyTenantId_ShouldBehaveAsUnspecified()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var dataSubjectIdHash = GenerateDataSubjectIdHash();
		var tenantA = $"tenant-A-{Guid.NewGuid():N}";
		var tenantB = $"tenant-B-{Guid.NewGuid():N}";

		var ownHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, tenantId: tenantA, isActive: true);
		await store.SaveHoldAsync(ownHold, CancellationToken.None).ConfigureAwait(false);

		// Another tenant's order for the same subject. Without it the arm is satisfied by a store that
		// returns everything, which is the opposite defect.
		var otherTenantHold = CreateLegalHold(dataSubjectIdHash: dataSubjectIdHash, tenantId: tenantB, isActive: true);
		await store.SaveHoldAsync(otherTenantHold, CancellationToken.None).ConfigureAwait(false);

		var withEmpty = await GetQueryStore(store)
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, string.Empty, DateTimeOffset.UtcNow, CancellationToken.None)
			.ConfigureAwait(false);

		var withNull = await GetQueryStore(store)
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, null, DateTimeOffset.UtcNow, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS -- an empty argument is not a tenant to match against, so the caller's own hold survives it.
		if (!withEmpty.Any(h => h.HoldId == ownHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"An active hold for '{tenantA}' ({ownHold.HoldId}) was saved, and a read passing an EMPTY "
				+ "tenant argument did not return it. An empty string is not a tenant identifier -- the "
				+ "untenanted partition is a sentinel VALUE, never the empty string -- so an empty argument "
				+ "means the caller named no tenant and must answer exactly as null does. A store that matches "
				+ "it as a literal value returns only the holds belonging to no tenant, and a legal hold blocks "
				+ "erasure: a caller told nothing is blocking proceeds with a deletion that cannot be undone.");
		}

		// The contract itself: empty and null are the SAME question, so they are the same answer. This is the
		// half that reds on the divergence -- one provider reading the empty string as a literal tenant value
		// while its siblings read it as "unsupplied" is exactly a set difference between these two reads.
		var emptyIds = withEmpty.Select(h => h.HoldId).OrderBy(id => id).ToList();
		var nullIds = withNull.Select(h => h.HoldId).OrderBy(id => id).ToList();
		if (!emptyIds.SequenceEqual(nullIds))
		{
			throw new TestFixtureAssertionException(
				"A read passing an EMPTY tenant argument returned a different set of holds than the same read "
				+ $"passing null ({emptyIds.Count} vs {nullIds.Count}). Both spell 'the caller named no tenant', "
				+ "so a store that distinguishes them gives one answer here and a different answer on a sibling "
				+ "provider for the same call.");
		}

		// SAFETY -- the arm must not be satisfiable by a store that simply returns everything. Naming a real
		// tenant still confines the read, so the two halves cannot be passed by the same mutation.
		var scoped = await GetQueryStore(store)
			.GetActiveHoldsForDataSubjectAsync(dataSubjectIdHash, tenantA, DateTimeOffset.UtcNow, CancellationToken.None)
			.ConfigureAwait(false);

		if (scoped.Any(h => h.HoldId == otherTenantHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				$"A read scoped to '{tenantA}' returned '{tenantB}'s hold ({otherTenantHold.HoldId}). Reading an "
				+ "unsupplied tenant argument permissively must not become reading every argument permissively: "
				+ "a store that returns every tenant's holds discloses one tenant's legal matters to another.");
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
			_ = await GetQueryStore(store).GetActiveHoldsForDataSubjectAsync(null!, null, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
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

		var holds = await GetQueryStore(store).GetActiveHoldsForTenantAsync(tenantA, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

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
			.GetActiveHoldsForTenantAsync(tenantA, DateTimeOffset.UtcNow, CancellationToken.None)
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
			_ = await GetQueryStore(store).GetActiveHoldsForTenantAsync(null!, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentException for null tenantId but no exception was thrown");
		}
		catch (ArgumentException)
		{
			// Expected - ArgumentException or ArgumentNullException
		}
	}

	#endregion

	/// <summary>
	/// Verifies that every in-force query EXCLUDES a hold whose stated expiry has passed, while still
	/// returning an indefinite hold and one whose expiry is in the future.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>In force is the CONJUNCTION of two independent facts, and one word was doing duty for both.</b>
	/// <c>IsActive</c> answers "was this hold RELEASED?" -- an act, with an actor, a time and a reason.
	/// <c>ExpiresAt</c> answers "has the stated period LAPSED?" -- the passage of time. Neither implies the
	/// other, and no member may use one as a proxy for the other: a hold is in force when it is neither
	/// released NOR lapsed.
	/// </para>
	/// <para>
	/// <b>THREE rows, because each one catches a different wrong implementation and no two of them
	/// together catch all three.</b> The lapsed row catches a provider that filters on the release flag
	/// alone, which is the defect this arm was added for. The INDEFINITE row is the liveness half: without
	/// it a provider that returns nothing at all passes the safety assertion, and an indefinite hold is
	/// the commonest kind there is. The FUTURE row catches an INVERTED comparison, which is the likeliest
	/// slip when adding the term and is otherwise indistinguishable from a correct fix -- a provider that
	/// returns only lapsed holds satisfies both of the other rows' expectations about itself.
	/// </para>
	/// <para>
	/// <b>This is a conformance arm rather than a per-provider test because the divergence it closes ran
	/// the dangerous direction:</b> the reference in-memory store derived expiry correctly while both SQL
	/// providers filtered on the release flag alone. Anything built and unit-tested against the reference
	/// store was therefore green in every test and inert on both shipped providers.
	/// </para>
	/// <para>
	/// It deliberately registers NO expiration sweep. A hold's stated expiry is a property of the hold,
	/// not of an optional background actor having run: the sweep RELEASES lapsed holds, and the three
	/// registration entry points for erasure, hold storage and expiration are separate public calls, so a
	/// deployment with holds and no sweep is a supported composition rather than a misconfiguration.
	/// </para>
	/// </remarks>
	/// <returns> A task representing the asynchronous operation. </returns>
	public virtual async Task ActiveHoldQueries_ShouldExcludeALapsedHoldAndStillReturnIndefiniteAndFutureOnes()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var tenant = $"tenant-expiry-{Guid.NewGuid():N}";
		var subject = $"subject-expiry-{Guid.NewGuid():N}";
		var now = DateTimeOffset.UtcNow;

		// CreatedAt is PINNED rather than taken from the clock, so the newest-first ordering below is
		// decidable without a delay between writes. Three holds created in quick succession can share a
		// timestamp, which would make an ordering assertion flaky rather than wrong.
		var createdBase = now.AddDays(-1);

		// NOT in force: never released, but its stated period has passed.
		var lapsed = CreateLegalHold(
			dataSubjectIdHash: subject, tenantId: tenant, isActive: true, expiresAt: now.AddHours(-1))
			with { CreatedAt = createdBase };

		// In force: expiry stated and not yet reached.
		var future = CreateLegalHold(
			dataSubjectIdHash: subject, tenantId: tenant, isActive: true, expiresAt: now.AddHours(1))
			with { CreatedAt = createdBase.AddMinutes(1) };

		// In force: an indefinite hold, which carries no expiry at all. NEWEST of the three.
		var indefinite = CreateLegalHold(
			dataSubjectIdHash: subject, tenantId: tenant, isActive: true, expiresAt: null)
			with { CreatedAt = createdBase.AddMinutes(2) };

		foreach (var hold in new[] { lapsed, future, indefinite })
		{
			await store.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);
		}

		var queries = GetQueryStore(store);

		AssertInForce(
			"GetActiveHoldsForDataSubjectAsync",
			await queries.GetActiveHoldsForDataSubjectAsync(subject, tenant, DateTimeOffset.UtcNow, CancellationToken.None)
				.ConfigureAwait(false),
			lapsed.HoldId, indefinite.HoldId, future.HoldId);

		AssertInForce(
			"GetActiveHoldsForTenantAsync",
			await queries.GetActiveHoldsForTenantAsync(tenant, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false),
			lapsed.HoldId, indefinite.HoldId, future.HoldId);

		AssertInForce(
			"ListActiveHoldsAsync",
			await queries.ListActiveHoldsAsync(tenant, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false),
			lapsed.HoldId, indefinite.HoldId, future.HoldId);
	}

	/// <summary>
	/// Verifies that the three in-force queries derive in force against the <c>asOf</c> they are GIVEN,
	/// and read no clock of their own.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The fixture is one hold whose expiry is in the FUTURE</b>, so it is in force at real wall-clock
	/// time and the two halves below differ only in the argument. Asked as of an instant BEFORE that
	/// expiry it must be returned; asked as of an instant AFTER it, it must not be. An implementation that
	/// ignores the parameter and reads its own clock returns the hold in BOTH, and the second assertion is
	/// what catches it -- there is no waiting and no sleeping, because the discriminator is the argument.
	/// </para>
	/// <para>
	/// <b>Why the contract carries the instant at all.</b> "The holds in force at asOf" is decidable;
	/// "the holds in force at a time the store chooses" is not, so two sound implementations could
	/// disagree and neither would be wrong. It also lets a caller that must read twice and union the
	/// results pin both reads to one instant, instead of unioning two sets that were never
	/// simultaneously true.
	/// </para>
	/// <para>
	/// Mutant, run rather than recorded: replace the parameter with the provider's own clock in any of
	/// the three members and the second half of that member's assertion goes RED.
	/// </para>
	/// </remarks>
	public virtual async Task ActiveHoldQueries_ShouldDeriveInForceAgainstTheGivenInstant()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var subject = $"subject-{Guid.NewGuid():N}";
		var tenant = $"tenant-{Guid.NewGuid():N}";

		var expiry = DateTimeOffset.UtcNow.AddHours(1);
		var hold = CreateLegalHold(
			dataSubjectIdHash: subject, tenantId: tenant, isActive: true, expiresAt: expiry);

		await store.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);

		var queries = GetQueryStore(store);
		var beforeExpiry = expiry.AddMinutes(-1);
		var afterExpiry = expiry.AddMinutes(1);

		await AssertDerivedAgainstAsOfAsync(
			"GetActiveHoldsForDataSubjectAsync",
			asOf => queries.GetActiveHoldsForDataSubjectAsync(subject, tenant, asOf, CancellationToken.None),
			hold.HoldId, beforeExpiry, afterExpiry).ConfigureAwait(false);

		await AssertDerivedAgainstAsOfAsync(
			"GetActiveHoldsForTenantAsync",
			asOf => queries.GetActiveHoldsForTenantAsync(tenant, asOf, CancellationToken.None),
			hold.HoldId, beforeExpiry, afterExpiry).ConfigureAwait(false);

		await AssertDerivedAgainstAsOfAsync(
			"ListActiveHoldsAsync",
			asOf => queries.ListActiveHoldsAsync(tenant, asOf, CancellationToken.None),
			hold.HoldId, beforeExpiry, afterExpiry).ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts that one in-force query answers the instant it was given rather than its own clock.
	/// </summary>
	/// <param name="member">The member under assertion, named in any failure.</param>
	/// <param name="read">The member, as a function of the instant.</param>
	/// <param name="holdId">The hold whose stated expiry falls between the two instants.</param>
	/// <param name="beforeExpiry">An instant at which the hold is still in force.</param>
	/// <param name="afterExpiry">An instant by which the hold has lapsed.</param>
	private static async Task AssertDerivedAgainstAsOfAsync(
		string member,
		Func<DateTimeOffset, Task<IReadOnlyList<LegalHold>>> read,
		Guid holdId,
		DateTimeOffset beforeExpiry,
		DateTimeOffset afterExpiry)
	{
		// LIVENESS first. Without it the safety half below is satisfied by a member that returns the
		// empty set to every caller, which is the cheapest way never to return a lapsed hold.
		var live = await read(beforeExpiry).ConfigureAwait(false);
		if (IndexOf(live, holdId) < 0)
		{
			throw new TestFixtureAssertionException(
				$"{member} must return a hold whose stated expiry is after the instant it was asked about. "
				+ "A hold in force at asOf is one that was released at no point and had not lapsed by then, "
				+ "and this one satisfies both.");
		}

		// SAFETY, and the half that catches a provider reading its own clock: at real wall-clock time this
		// hold has NOT lapsed, so only a member honouring the argument can exclude it.
		var lapsed = await read(afterExpiry).ConfigureAwait(false);
		if (IndexOf(lapsed, holdId) >= 0)
		{
			throw new TestFixtureAssertionException(
				$"{member} returned a hold that had lapsed by the instant it was asked about. The hold's "
				+ "expiry has not yet passed in real time, so returning it here means the member derived "
				+ "in force against its own clock and ignored the instant the caller supplied -- which "
				+ "makes its answer undecidable and makes two reads that must agree unable to.");
		}
	}

	/// <summary>
	/// Asserts that one in-force query excluded the lapsed hold and returned both of the live ones.
	/// </summary>
	/// <param name="member">The member under assertion, named in any failure.</param>
	/// <param name="holds">What that member returned.</param>
	/// <param name="lapsedId">The hold whose stated expiry has passed.</param>
	/// <param name="indefiniteId">The hold carrying no expiry.</param>
	/// <param name="futureId">The hold whose expiry is not yet reached.</param>
	private static void AssertInForce(
		string member,
		IReadOnlyList<LegalHold> holds,
		Guid lapsedId,
		Guid indefiniteId,
		Guid futureId)
	{
		if (holds.Any(h => h.HoldId == lapsedId))
		{
			throw new TestFixtureAssertionException(
				$"{member} returned a hold whose stated expiry has PASSED. A lapsed hold is not in force, so "
				+ "it must not block an erasure: reported in force forever, it leaves an Article 17 erasure "
				+ "permanently undischarged with nothing recording why. Filtering on the release flag alone "
				+ "is the defect -- in force is 'not released AND not lapsed'.");
		}

		if (!holds.Any(h => h.HoldId == indefiniteId))
		{
			throw new TestFixtureAssertionException(
				$"{member} dropped an INDEFINITE hold -- one carrying no expiry at all, which is the commonest "
				+ "kind. A hold with no stated end has not lapsed and is in force until it is released. "
				+ "Without this assertion a provider returning nothing would satisfy the lapsed-hold check.");
		}

		if (!holds.Any(h => h.HoldId == futureId))
		{
			throw new TestFixtureAssertionException(
				$"{member} dropped a hold whose expiry is in the FUTURE, which means the expiry comparison is "
				+ "inverted: it is returning the lapsed holds and withholding the live ones. That destroys "
				+ "data a court order said to keep, and it is indistinguishable from a correct fix unless "
				+ "this row is present.");
		}

		// ORDERING IS PART OF THE CONTRACT, and it diverged silently: both SQL providers ordered
		// newest-first while the reference store returned these two members in dictionary order. A caller
		// taking the first row got the newest hold on one provider and an arbitrary one on another, which
		// nothing detected because nothing takes the first row TODAY. The hold carrying the basis, case
		// reference and description is the one an operator reads first, so "arbitrary" is not a usable
		// contract and the convergence is toward the providers that already agreed.
		var indefiniteIndex = IndexOf(holds, indefiniteId);
		var futureIndex = IndexOf(holds, futureId);

		if (indefiniteIndex > futureIndex)
		{
			throw new TestFixtureAssertionException(
				$"{member} returned holds in the wrong order. In-force holds are ordered by CreatedAt "
				+ $"DESCENDING -- newest first -- and this store put the older hold at index {futureIndex} "
				+ $"ahead of the newer one at index {indefiniteIndex}.");
		}
	}

	/// <summary>
	/// The position of <paramref name="holdId"/> in <paramref name="holds"/>.
	/// </summary>
	/// <param name="holds">The returned holds.</param>
	/// <param name="holdId">The hold to locate.</param>
	/// <returns>Its index, or -1 when absent.</returns>
	private static int IndexOf(IReadOnlyList<LegalHold> holds, Guid holdId)
	{
		for (var i = 0; i < holds.Count; i++)
		{
			if (holds[i].HoldId == holdId)
			{
				return i;
			}
		}

		return -1;
	}

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

		var holds = await GetQueryStore(store).ListActiveHoldsAsync(null, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

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

		var holds = await GetQueryStore(store).ListActiveHoldsAsync(tenantA, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);

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
