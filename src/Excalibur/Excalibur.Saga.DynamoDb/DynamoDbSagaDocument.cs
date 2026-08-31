// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Amazon.DynamoDBv2.Model;

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.DynamoDb;

/// <summary>
/// DynamoDB document structure for saga state using single-table design.
/// </summary>
/// <remarks>
/// <para>
/// Uses single-table design with the following key structure:
/// </para>
/// <list type="bullet">
/// <item><description>PK: SAGA#t:{tenantId}:{sagaId} - Partition by tenant AND saga</description></item>
/// <item><description>SK: {sagaType} - Sort key for multi-type queries</description></item>
/// </list>
/// </remarks>
internal static class DynamoDbSagaDocument
{
	// Attribute names
	public const string PK = "PK";
	public const string SK = "SK";
	public const string SagaId = "sagaId";
	public const string SagaType = "sagaType";
	public const string StateJson = "stateJson";

	/// <summary>
	/// Attribute holding the owning tenant. Absent on items in the untenanted partition, which is why the
	/// unscoped write condition tests <c>attribute_not_exists</c> rather than comparing to a value.
	/// </summary>
	public const string TenantId = "tenantId";
	public const string IsCompleted = "isCompleted";
	public const string CompletedAt = "completedAt";
	public const string Version = "version";
	public const string CreatedUtc = "createdUtc";
	public const string UpdatedUtc = "updatedUtc";
	public const string Ttl = "ttl";

	// Partition key prefix
	public const string SagaPrefix = "SAGA#";

	/// <summary>
	/// The segment every partition key carries ahead of the owning tenant, after <see cref="SagaPrefix"/>.
	/// </summary>
	public const string TenantKeyPrefix = "t:";

	/// <summary>
	/// The complete leading prefix of every saga partition key this release writes:
	/// <see cref="SagaPrefix"/> followed by <see cref="TenantKeyPrefix"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant segment is NOT the leading one on this provider - the item-kind discriminator is, because
	/// the table is a single-table design and may hold items that are not sagas at all. A probe that tested
	/// <c>begins_with(PK, "t:")</c> would therefore match nothing and report every table clean; one that
	/// tested <c>NOT begins_with(PK, "t:")</c> would match everything and report every table dirty. Both are
	/// wrong in the same way, and the composed prefix is what makes the test say what it means.
	/// </para>
	/// <para>
	/// Declared once and consumed by both <see cref="CreatePK"/> and the store's legacy-item probe, so the
	/// shape the store writes and the shape it refuses to read cannot drift apart.
	/// </para>
	/// </remarks>
	public const string TenantedPartitionKeyPrefix = SagaPrefix + TenantKeyPrefix;

	/// <summary>
	/// Creates the partition key value for a given tenant and saga ID.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant belongs to the item's IDENTITY, which is a different property from the tenant term the
	/// write condition carries. The condition decides whether this scope may write an item; the identity
	/// decides which items can exist at all. Keyed on the saga identifier alone, two tenants running a saga
	/// at the same business key are ONE item, so the condition that correctly refuses a cross-tenant
	/// overwrite also refuses the second tenant its own saga -- an estate-wide uniqueness constraint on the
	/// saga identifier rather than an isolation control.
	/// </para>
	/// <para>
	/// The tenant term is total (never null, never empty): a host with no tenancy resolves the framework
	/// single-tenant default and a genuinely untenanted saga resolves the reserved untenanted sentinel, so
	/// every partition key carries a tenant segment and none can be produced without one.
	/// </para>
	/// </remarks>
	/// <param name="tenantId">The owning tenant term, as resolved from the store's scope.</param>
	/// <param name="sagaId">The saga identifier.</param>
	/// <returns>The partition key value.</returns>
	public static string CreatePK(string tenantId, Guid sagaId) =>
		$"{TenantedPartitionKeyPrefix}{tenantId}:{sagaId}";

	/// <summary>
	/// Creates the sort key value for a given saga type.
	/// </summary>
	/// <param name="sagaType">The saga type name.</param>
	/// <returns>The sort key value.</returns>
	public static string CreateSK(string sagaType) => sagaType;

	/// <summary>
	/// Converts a saga state to a DynamoDB item.
	/// </summary>
	/// <typeparam name="TSagaState">The type of saga state.</typeparam>
	/// <param name="sagaState">The saga state to convert.</param>
	/// <param name="stateJson">The serialized saga state as JSON.</param>
	/// <param name="newVersion">
	/// The persisted optimistic-concurrency version to write (the store's bumped <c>loadedVersion + 1</c>).
	/// </param>
	/// <param name="createdUtc">The creation timestamp.</param>
	/// <param name="updatedUtc">The update timestamp.</param>
	/// <param name="tenantId">The owning tenant term, which forms the leading segment of the partition key.</param>
	/// <param name="ttlSeconds">Optional TTL in seconds (0 = no TTL).</param>
	/// <returns>The DynamoDB item attributes.</returns>
	public static Dictionary<string, AttributeValue> FromSagaState<TSagaState>(
		TSagaState sagaState,
		string stateJson,
		long newVersion,
		DateTimeOffset createdUtc,
		DateTimeOffset updatedUtc,
		string tenantId,
		int ttlSeconds = 0)
		where TSagaState : SagaState
	{
		var sagaType = typeof(TSagaState).Name;
		var item = new Dictionary<string, AttributeValue>
		{
			[PK] = new() { S = CreatePK(tenantId, sagaState.SagaId) },
			[SK] = new() { S = CreateSK(sagaType) },
			[SagaId] = new() { S = sagaState.SagaId.ToString() },
			[SagaType] = new() { S = sagaType },
			[StateJson] = new() { S = stateJson },
			[IsCompleted] = new() { BOOL = sagaState.Completed },
			[Version] = new() { N = newVersion.ToString(CultureInfo.InvariantCulture) },
			[CreatedUtc] = new() { S = createdUtc.ToString("O", CultureInfo.InvariantCulture) },
			[UpdatedUtc] = new() { S = updatedUtc.ToString("O", CultureInfo.InvariantCulture) }
		};

		// Persist the completion timestamp only for a completed saga (round-trip UTC "O" format — fixed-width
		// and lexicographically ordered, so a string range comparison is a valid chronological range). A running
		// saga has no completedAt attribute, so retention purge (attribute_exists guard) never removes it.
		if (sagaState.CompletedAt is { } completedAt)
		{
			item[CompletedAt] = new() { S = completedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) };
		}

		if (ttlSeconds > 0)
		{
			var ttlValue = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeSeconds();
			item[Ttl] = new() { N = ttlValue.ToString(CultureInfo.InvariantCulture) };
		}

		return item;
	}
}
