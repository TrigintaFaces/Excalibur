// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// 8vewf8 — end-to-end proof that an erasure metadata payload above Oracle's 2000-byte RAW limit round-trips
/// via <see cref="OracleBlobParameter"/> on real Oracle. The snapshot half of this concern was already
/// proven (<c>OracleSnapshotStoreKitConformanceShould</c>, 100,000-char payload, byte-for-byte). The erase
/// half's own <c>EraseEventsRequest.cs:67</c> binds a FIXED, small (~60-byte) JSON template — it never
/// itself produces an oversized value — so this test exercises the exact same table/column/parameter-helper
/// shape that request uses (<c>EVENTSTOREEVENTS.METADATA</c>, bound via <see cref="OracleBlobParameter"/>)
/// with a synthetic oversized payload, proving the MECHANISM the erase path depends on, not a scenario that
/// route can currently reach.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-vacuity was attempted and dropped, honestly.</b> A companion arm tried to bind the identical
/// oversized payload through Dapper's plain CLR-inferred <see cref="byte"/>[] parameter (the pre-fix shape
/// <see cref="OracleBlobParameter"/>'s own XML doc describes: infers <c>DbType.Binary</c>, which the doc
/// says ODP.NET maps to RAW, capped at 2000 bytes) expecting ORA-01460/ORA-12899. Measured against this
/// container (gvenzl/oracle-free 23c, <c>MAX_STRING_SIZE=STANDARD</c> — checked, not the extended-types
/// explanation), that bind did NOT throw, even with <c>DbType.Binary</c> forced explicitly. The doc
/// comment's premise did not reproduce on this ODP.NET/Oracle combination and the cause was not run down
/// further (out of scope for this test-only bead) — see <c>Excalibur.Dispatch-jz96yd</c> for the follow-up.
/// The unit-level type-emission check this bead's own history already cites
/// (<c>OracleBlobParameterShould</c>, 3/3, asserts the emitted parameter carries
/// <see cref="Oracle.ManagedDataAccess.Client.OracleDbType.Blob"/>) still stands as the non-vacuity proof
/// for the parameter-type side; this test's job is the round-trip, which is real regardless.
/// </para>
/// <para>
/// verify-against-real-infra-not-mock: real Oracle only, non-skipped (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleEventStoreErasureOversizedMetadataShould
{
	private const string AggregateType = "OversizedMetadataOrder";

	// Oracle's RAW parameter cap is 2000 bytes; comfortably over it so a near-boundary rounding quirk in the
	// driver cannot make this pass by accident.
	private const int OversizedPayloadBytes = 5000;

	private readonly OracleEventStoreContainerFixture _fixture;

	public OracleEventStoreErasureOversizedMetadataShould(OracleEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	[MessageName("Test.OracleEventStoreErasureOversizedMetadata.OrderPlaced")]
	private sealed record OrderPlaced(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	private OracleEventStore StoreFor() =>
		new(
			() => new OracleConnection(_fixture.ConnectionString),
			NullLogger<OracleEventStore>.Instance,
			schema: _fixture.Schema,
			table: _fixture.TableName,
			tenantContext: UntenantedTestTenantContext.Instance);

	private static byte[] OversizedPayload(byte fill) => Enumerable.Repeat(fill, OversizedPayloadBytes).ToArray();

	/// <summary>
	/// SAFETY — an oversized erasure-metadata payload, bound via <see cref="OracleBlobParameter"/> against
	/// the exact table/column <c>EraseEventsRequest</c> writes to, does NOT throw ORA-01460/ORA-12899.
	/// </summary>
	[Fact]
	public async Task RouteAnOversizedErasureMetadataPayloadThroughOracleBlobParameter_WithoutThrowing()
	{
		_fixture.DockerAvailable.ShouldBeTrue("the oversized-erasure-metadata lock is never skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var store = StoreFor();
		_ = await store.AppendAsync(
			aggId, AggregateType, new IDomainEvent[] { new OrderPlaced(aggId) }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		var oversized = OversizedPayload(0x5A);

		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// Same shape as EraseEventsRequest.cs: an UPDATE ... SET METADATA = :ErasureMetadata against
		// EVENTSTOREEVENTS, bound through OracleBlobParameter.
#pragma warning disable CA2100 // table name is fixture-owned, not user input
		var sql = $"UPDATE {_fixture.TableName} SET METADATA = :ErasureMetadata WHERE AGGREGATEID = :AggregateId";
#pragma warning restore CA2100
		var parameters = new DynamicParameters();
		parameters.Add(":ErasureMetadata", new OracleBlobParameter(oversized));
		parameters.Add(":AggregateId", aggId);

		// SAFETY: must not raise ORA-01460 ("unimplemented or unreasonable conversion requested") or
		// ORA-12899 ("value too large for column") — the size-dependent failure OracleBlobParameter exists
		// to prevent.
		var updated = await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
		updated.ShouldBe(1, "exactly the seeded row must be updated");

		// LIVENESS: the full oversized payload round-trips byte-for-byte, not merely "did not throw" (a
		// silently truncated write would also not throw).
		var readBack = await ReadMetadataAsync(aggId).ConfigureAwait(false);
		readBack.ShouldNotBeNull();
		readBack.ShouldBe(oversized, "the full 5000-byte payload must round-trip byte-for-byte, not be truncated at the 2000-byte RAW boundary.");

		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private async Task<byte[]?> ReadMetadataAsync(string aggregateId)
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

#pragma warning disable CA2100
		await using var command = new OracleCommand(
			$"SELECT METADATA FROM {_fixture.TableName} WHERE AGGREGATEID = :aggId", connection)
		{
			BindByName = true,
		};
#pragma warning restore CA2100
		_ = command.Parameters.Add(":aggId", aggregateId);

		var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
		return value as byte[];
	}
}
