// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;
using Excalibur.Dispatch;
using Excalibur.AuditLogging;
using Excalibur.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryAuditStore"/> validating IAuditStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryAuditStore uses an instance-level ConcurrentDictionary with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>StoreAsync THROWS InvalidOperationException on duplicate EventId</description></item>
/// <item><description>Hash chain integrity via PreviousEventHash and EventHash</description></item>
/// <item><description>Multi-tenant isolation, with null TenantId routed to the reserved untenanted partition</description></item>
/// <item><description>Genesis hash for first event in tenant chain</description></item>
/// <item><description>QueryAsync supports 11 filter criteria + pagination + ordering</description></item>
/// <item><description>VerifyChainIntegrityAsync detects tampering (COMPLIANCE-CRITICAL)</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "STORE")]
public sealed class InMemoryAuditStoreConformanceTests : AuditStoreConformanceTestKit
{
	/// <inheritdoc />
	/// <remarks>
	/// Deliberately ambient-less, as the kit intends: these arms assert the partition a caller with no
	/// tenant resolves to. The store now requires a context, so that host is named rather than implied —
	/// the partition addressed is the same one a missing context resolved to, so no arm changes what it
	/// exercises.
	/// </remarks>
	protected override IAuditStore CreateStore() =>
		new InMemoryAuditStore(AuditIntegrityTestStrategy.Create(), ConformanceTenantHosts.UntenantedAuditHost());

	/// <inheritdoc />
	/// <remarks>
	/// Reaches the store's own indices by reflection rather than widening production visibility. Both are
	/// mutated: the by-id map and the partition list the verifier actually walks. Throws when the record is
	/// not reachable, so a removal that removed nothing cannot let the deletion arm pass vacuously.
	/// </remarks>
	protected override Task DeleteRecordOutOfBandAsync(
		IAuditStore store,
		string eventId,
		CancellationToken cancellationToken)
	{
		var (byId, byTenant) = ReadIndices(store);
		_ = byId.TryRemove(eventId, out _);

		var removed = 0;
		foreach (var partition in byTenant.Values)
		{
			lock (partition)
			{
				removed += partition.RemoveAll(e => string.Equals(e.EventId, eventId, StringComparison.Ordinal));
			}
		}

		return removed == 1
			? Task.CompletedTask
			: throw new InvalidOperationException(
				$"Expected to delete exactly one stored event '{eventId}', deleted {removed}.");
	}

	/// <inheritdoc />
	/// <remarks>
	/// AuditEvent is a record, so the mutated copy must replace the entry in the partition list the verifier
	/// reads; replacing only the by-id entry would leave the pristine original in front of verification.
	/// Both hash columns are carried across unchanged.
	/// </remarks>
	protected override Task RewriteRecordActionOutOfBandAsync(
		IAuditStore store,
		string eventId,
		string newAction,
		CancellationToken cancellationToken)
	{
		var (byId, byTenant) = ReadIndices(store);

		if (!byId.TryGetValue(eventId, out var stored))
		{
			throw new InvalidOperationException($"Stored event '{eventId}' not found; refusing to pass vacuously.");
		}

		var rewritten = stored with { Action = newAction };
		byId[eventId] = rewritten;

		var replaced = 0;
		foreach (var partition in byTenant.Values)
		{
			lock (partition)
			{
				for (var i = 0; i < partition.Count; i++)
				{
					if (string.Equals(partition[i].EventId, eventId, StringComparison.Ordinal))
					{
						partition[i] = rewritten;
						replaced++;
					}
				}
			}
		}

		return replaced == 1
			? Task.CompletedTask
			: throw new InvalidOperationException(
				$"Expected stored event '{eventId}' in exactly one partition, found {replaced}.");
	}

	private static (ConcurrentDictionary<string, AuditEvent> ById, ConcurrentDictionary<string, List<AuditEvent>> ByTenant) ReadIndices(
		IAuditStore store)
	{
		var byId = Field<ConcurrentDictionary<string, AuditEvent>>(store, "_eventsById");
		var byTenant = Field<ConcurrentDictionary<string, List<AuditEvent>>>(store, "_eventsByTenant");
		return (byId, byTenant);
	}

	private static T Field<T>(IAuditStore store, string name)
		where T : class
	{
		var field = store.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException(
				$"{store.GetType().Name}.{name} not found; the store's storage shape changed — update these hooks.");

		return (T?)field.GetValue(store)
			?? throw new InvalidOperationException($"{name} was null.");
	}

	/// <summary>A record removed from the middle of the trail is reported.</summary>
	[Fact]
	public Task VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations_Test() =>
		VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations();

	/// <summary>A rewritten record with intact hash columns is reported.</summary>
	[Fact]
	public Task VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations_Test() =>
		VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations();

	/// <summary>An intact trail interleaving two tenants verifies clean.</summary>
	[Fact]
	public Task VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified_Test() =>
		VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified();

	/// <inheritdoc />
	/// <remarks>
	/// The interleaving arm needs a store that resolves the ambient tenant the arm establishes; the default
	/// fixture deliberately supplies none, and would resolve every read to the untenanted partition.
	/// </remarks>
	protected override IAuditStore CreateTenantAwareStore() =>
		new InMemoryAuditStore(AuditIntegrityTestStrategy.Create(), new AmbientHolderTenantContext());

	private sealed class AmbientHolderTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	#region Store Tests

	[Fact]
	public Task StoreAsync_ShouldPersistEvent_Test() =>
		StoreAsync_ShouldPersistEvent();

	[Fact]
	public Task StoreAsync_WithNullEvent_ShouldThrow_Test() =>
		StoreAsync_WithNullEvent_ShouldThrow();

	[Fact]
	public Task StoreAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
		StoreAsync_DuplicateId_ShouldThrowInvalidOperationException();

	#endregion Store Tests

	#region Retrieval Tests

	[Fact]
	public Task GetByIdAsync_ExistingEvent_ShouldReturnEvent_Test() =>
		GetByIdAsync_ExistingEvent_ShouldReturnEvent();

	[Fact]
	public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() =>
		GetByIdAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetByIdAsync_NullOrEmpty_ShouldThrow_Test() =>
		GetByIdAsync_NullOrEmpty_ShouldThrow();

	[Fact]
	public Task GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt_Test() =>
		GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt();

	#endregion Retrieval Tests

	#region Query Tests

	[Fact]
	public Task QueryAsync_ByDateRange_ShouldReturnMatching_Test() =>
		QueryAsync_ByDateRange_ShouldReturnMatching();

	[Fact]
	public Task QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents_Test() =>
		QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents();

	[Fact]
	public Task QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents_Test() =>
		QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents();

	[Fact]
	public Task QueryAsync_ByEventType_ShouldFilter_Test() =>
		QueryAsync_ByEventType_ShouldFilter();

	[Fact]
	public Task QueryAsync_ByActorId_ShouldFilter_Test() =>
		QueryAsync_ByActorId_ShouldFilter();

	[Fact]
	public Task QueryAsync_Pagination_ShouldRespectSkipAndMaxResults_Test() =>
		QueryAsync_Pagination_ShouldRespectSkipAndMaxResults();

	#endregion Query Tests

	#region Count Tests

	[Fact]
	public Task CountAsync_WithFilters_ShouldReturnCount_Test() =>
		CountAsync_WithFilters_ShouldReturnCount();

	[Fact]
	public Task CountAsync_EmptyResult_ShouldReturnZero_Test() =>
		CountAsync_EmptyResult_ShouldReturnZero();

	#endregion Count Tests

	#region Integrity Tests

	[Fact]
	public Task VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified_Test() =>
		VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified();

	[Fact]
	public Task VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope_Test() =>
		VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope();

	#endregion Integrity Tests

	#region LastEvent Tests

	[Fact]
	public Task GetLastEventAsync_WithTenant_ShouldReturnLastForTenant_Test() =>
		GetLastEventAsync_WithTenant_ShouldReturnLastForTenant();

	[Fact]
	public Task GetLastEventAsync_DefaultTenant_ShouldReturnLast_Test() =>
		GetLastEventAsync_DefaultTenant_ShouldReturnLast();

	#endregion LastEvent Tests

	#region Hash Chain Tests

	[Fact]
	public Task StoreAsync_ShouldSetPreviousEventHash_Test() =>
		StoreAsync_ShouldSetPreviousEventHash();

	[Fact]
	public Task StoreAsync_ShouldComputeEventHash_Test() =>
		StoreAsync_ShouldComputeEventHash();

	#endregion Hash Chain Tests

	#region ApplicationName Tests

	[Fact]
	public Task StoreAsync_WithApplicationName_ShouldPersistApplicationName_Test() =>
		StoreAsync_WithApplicationName_ShouldPersistApplicationName();

	[Fact]
	public Task StoreAsync_WithNullApplicationName_ShouldPersistNull_Test() =>
		StoreAsync_WithNullApplicationName_ShouldPersistNull();

	[Fact]
	public Task QueryAsync_ByApplicationName_ShouldFilter_Test() =>
		QueryAsync_ByApplicationName_ShouldFilter();

	[Fact]
	public Task CountAsync_ByApplicationName_ShouldCount_Test() =>
		CountAsync_ByApplicationName_ShouldCount();

	[Fact]
	public Task StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash_Test() =>
		StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash();

	#endregion ApplicationName Tests

	/// <summary>Every arm this kit declares is surfaced above; an omission fails by name.</summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
		ConformanceSuite_ShouldWireEveryArm();
}
