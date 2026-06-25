// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Data;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to get the latest snapshot for an aggregate.
/// </summary>
public sealed class GetLatestSnapshotRequest : DataRequestBase<IDbConnection, ISnapshot?>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetLatestSnapshotRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	public GetLatestSnapshotRequest(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreSnapshots")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, Metadata
			FROM {qualifiedTable}
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var result = await connection.QuerySingleOrDefaultAsync<SnapshotData>(Command).ConfigureAwait(false);
			if (result == null)
			{
				return null;
			}

			return new Snapshot
			{
				SnapshotId = result.SnapshotId ?? Guid.NewGuid().ToString(),
				AggregateId = result.AggregateId,
				AggregateType = result.AggregateType,
				Version = result.Version,
				Data = result.Data,
				CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
				Metadata = DeserializeMetadata(result.Metadata),
			};
		};
	}

	/// <summary>
	/// Deserializes the stored binary metadata payload back into a dictionary, inferring CLR primitive
	/// types so that consumers reading typed values (e.g. the integer schema version) observe the
	/// original type rather than a <see cref="JsonElement"/>. Returns <see langword="null"/> when no
	/// metadata was persisted.
	/// </summary>
	private static IDictionary<string, object>? DeserializeMetadata(byte[]? metadata)
	{
		if (metadata is null || metadata.Length == 0)
		{
			return null;
		}

		Dictionary<string, JsonElement>? raw;
#pragma warning disable IL2026, IL3050 // Metadata deserialization inherently uses reflection (matches SqlServerEventStore precedent)
		raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadata);
#pragma warning restore IL2026, IL3050
		if (raw is null)
		{
			return null;
		}

		var result = new Dictionary<string, object>(raw.Count);
		foreach (var (key, element) in raw)
		{
			result[key] = ConvertJsonElement(element)!;
		}

		return result;
	}

	/// <summary>
	/// Converts a <see cref="JsonElement"/> to its inferred CLR primitive. Integral numbers prefer
	/// <see cref="int"/> (then <see cref="long"/>) so that an <c>is int</c> consumer check succeeds;
	/// non-integral numbers fall back to <see cref="double"/>. Non-primitive values are returned as a
	/// cloned <see cref="JsonElement"/>.
	/// </summary>
	private static object? ConvertJsonElement(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.String:
				return element.GetString();
			case JsonValueKind.Number:
				if (element.TryGetInt32(out var intValue))
				{
					return intValue;
				}

				if (element.TryGetInt64(out var longValue))
				{
					return longValue;
				}

				return element.GetDouble();
			case JsonValueKind.True:
				return true;
			case JsonValueKind.False:
				return false;
			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				return null;
			default:
				return element.Clone();
		}
	}

	private sealed record SnapshotData(
		string? SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		byte[] Data,
		DateTime CreatedAt,
		byte[]? Metadata);
}
