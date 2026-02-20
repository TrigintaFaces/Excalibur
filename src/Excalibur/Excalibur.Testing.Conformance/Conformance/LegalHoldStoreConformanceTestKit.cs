// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

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
/// public class SqlServerLegalHoldStoreConformanceTests : LegalHoldStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override ILegalHoldStore CreateStore() =&gt;
///         new SqlServerLegalHoldStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class LegalHoldStoreConformanceTestKit
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();

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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();

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
		var store = CreateStore();
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
		var store = CreateStore();
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
	/// Verifies that GetActiveHoldsForDataSubjectAsync throws ArgumentException for null/whitespace.
	/// </summary>
	public virtual async Task GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException()
	{
		var store = CreateStore();

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
	/// Verifies that GetActiveHoldsForTenantAsync returns active tenant holds.
	/// </summary>
	public virtual async Task GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching()
	{
		var store = CreateStore();
		var tenantId = $"tenant-{Guid.NewGuid():N}";

		// Active hold for tenant
		var activeHold = CreateLegalHold(tenantId: tenantId, isActive: true);
		await store.SaveHoldAsync(activeHold, CancellationToken.None).ConfigureAwait(false);

		// Released hold for same tenant
		var releasedHold = CreateLegalHold(tenantId: tenantId, isActive: false);
		await store.SaveHoldAsync(releasedHold, CancellationToken.None).ConfigureAwait(false);

		var holds = await GetQueryStore(store).GetActiveHoldsForTenantAsync(tenantId, CancellationToken.None).ConfigureAwait(false);

		if (!holds.Any(h => h.HoldId == activeHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Active hold should be returned for tenant");
		}

		if (holds.Any(h => h.HoldId == releasedHold.HoldId))
		{
			throw new TestFixtureAssertionException(
				"Released hold should NOT be returned for tenant");
		}
	}

	/// <summary>
	/// Verifies that GetActiveHoldsForTenantAsync throws ArgumentException for null/whitespace tenantId.
	/// </summary>
	public virtual async Task GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException()
	{
		var store = CreateStore();

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
		var store = CreateStore();

		// Create holds with different timestamps
		var olderHold = CreateLegalHold(isActive: true);
		await store.SaveHoldAsync(olderHold, CancellationToken.None).ConfigureAwait(false);

		// Small delay to ensure different CreatedAt
		await Task.Delay(10).ConfigureAwait(false);

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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();
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
		var store = CreateStore();

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
		var store = CreateStore();

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
