// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Amazon.DynamoDBv2.Model;

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Data.DynamoDb.Saga;

/// <summary>
/// DynamoDB document structure for saga state using single-table design.
/// </summary>
/// <remarks>
/// <para>
/// Uses single-table design with the following key structure:
/// </para>
/// <list type="bullet">
/// <item><description>PK: SAGA#{sagaId} - Partition by saga</description></item>
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
	public const string IsCompleted = "isCompleted";
	public const string CreatedUtc = "createdUtc";
	public const string UpdatedUtc = "updatedUtc";
	public const string Ttl = "ttl";

	// Partition key prefix
	public const string SagaPrefix = "SAGA#";

	/// <summary>
	/// Creates the partition key value for a given saga ID.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <returns>The partition key value.</returns>
	public static string CreatePK(Guid sagaId) => $"{SagaPrefix}{sagaId}";

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
	/// <param name="createdUtc">The creation timestamp.</param>
	/// <param name="updatedUtc">The update timestamp.</param>
	/// <param name="ttlSeconds">Optional TTL in seconds (0 = no TTL).</param>
	/// <returns>The DynamoDB item attributes.</returns>
	public static Dictionary<string, AttributeValue> FromSagaState<TSagaState>(
		TSagaState sagaState,
		string stateJson,
		DateTimeOffset createdUtc,
		DateTimeOffset updatedUtc,
		int ttlSeconds = 0)
		where TSagaState : SagaState
	{
		var sagaType = typeof(TSagaState).Name;
		var item = new Dictionary<string, AttributeValue>
		{
			[PK] = new() { S = CreatePK(sagaState.SagaId) },
			[SK] = new() { S = CreateSK(sagaType) },
			[SagaId] = new() { S = sagaState.SagaId.ToString() },
			[SagaType] = new() { S = sagaType },
			[StateJson] = new() { S = stateJson },
			[IsCompleted] = new() { BOOL = sagaState.Completed },
			[CreatedUtc] = new() { S = createdUtc.ToString("O", CultureInfo.InvariantCulture) },
			[UpdatedUtc] = new() { S = updatedUtc.ToString("O", CultureInfo.InvariantCulture) }
		};

		if (ttlSeconds > 0)
		{
			var ttlValue = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeSeconds();
			item[Ttl] = new() { N = ttlValue.ToString(CultureInfo.InvariantCulture) };
		}

		return item;
	}
}
