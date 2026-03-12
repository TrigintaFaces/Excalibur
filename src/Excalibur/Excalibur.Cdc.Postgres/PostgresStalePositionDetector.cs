// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Detects and classifies stale CDC position errors from Postgres exceptions.
/// </summary>
public static class PostgresStalePositionDetector
{
	/// <summary>Postgres SQLSTATE for WAL segment not accessible (could_not_access_file).</summary>
	public const string WalSegmentNotAccessible = "58P01";

	/// <summary>Postgres SQLSTATE for object not in prerequisite state (replication slot invalid).</summary>
	public const string ObjectNotInPrerequisiteState = "55000";

	/// <summary>Postgres SQLSTATE for undefined object (publication/slot not found).</summary>
	public const string UndefinedObject = "42704";

	/// <summary>Postgres SQLSTATE for connection failure.</summary>
	public const string ConnectionFailure = "08006";

	/// <summary>Postgres SQLSTATE for protocol violation (logical decoding issues).</summary>
	public const string ProtocolViolation = "08P01";

	/// <summary>Postgres SQLSTATE for feature not supported (e.g., wal_level not logical).</summary>
	public const string FeatureNotSupported = "0A000";

	/// <summary>
	/// Gets the set of Postgres SQLSTATE codes that indicate a stale CDC position.
	/// </summary>
	public static IReadOnlySet<string> StalePositionSqlStates { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		WalSegmentNotAccessible, ObjectNotInPrerequisiteState, UndefinedObject,
		ConnectionFailure, ProtocolViolation, FeatureNotSupported
	};

	/// <summary>
	/// Determines whether the specified exception indicates a stale CDC position.
	/// </summary>
	public static bool IsStalePositionException(Exception? exception)
	{
		if (exception == null)
		{
			return false;
		}

		return exception switch
		{
			PostgresException pgEx => IsStalePositionSqlState(pgEx.SqlState),
			NpgsqlException npgsqlEx => IsStalePositionByMessage(npgsqlEx.Message) ||
										IsStalePositionException(npgsqlEx.InnerException),
			AggregateException aggEx => aggEx.InnerExceptions.Any(IsStalePositionException),
			_ => IsStalePositionException(exception.InnerException)
		};
	}

	/// <summary>
	/// Extracts the SQLSTATE code that indicates a stale position from the exception.
	/// </summary>
	public static string? GetStalePositionSqlState(Exception? exception)
	{
		if (exception == null)
		{
			return null;
		}

		return exception switch
		{
			PostgresException pgEx when IsStalePositionSqlState(pgEx.SqlState) => pgEx.SqlState,
			NpgsqlException npgsqlEx => GetStalePositionSqlState(npgsqlEx.InnerException),
			AggregateException aggEx => aggEx.InnerExceptions
				.Select(GetStalePositionSqlState)
				.FirstOrDefault(s => s != null),
			_ => GetStalePositionSqlState(exception.InnerException)
		};
	}

	/// <summary>
	/// Creates a <see cref="CdcPositionResetEventArgs"/> from an exception and context.
	/// </summary>
	public static CdcPositionResetEventArgs CreateEventArgs(
		Exception exception, string processorId,
		PostgresCdcPosition? stalePosition = null, PostgresCdcPosition? newPosition = null,
		string? replicationSlot = null, string? publication = null, string? databaseName = null)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var sqlState = GetStalePositionSqlState(exception);
		var reasonCode = sqlState != null
			? PostgresStalePositionReasonCodes.FromSqlState(sqlState)
			: PostgresStalePositionReasonCodes.FromErrorMessage(exception.Message);

		var additionalContext = new Dictionary<string, object>();
		if (sqlState != null)
		{
			additionalContext["SqlState"] = sqlState;
		}

		if (replicationSlot != null)
		{
			additionalContext["ReplicationSlotName"] = replicationSlot;
		}

		if (publication != null)
		{
			additionalContext["PublicationName"] = publication;
		}

		return new CdcPositionResetEventArgs
		{
			ProcessorId = processorId,
			ProviderType = "Postgres",
			CaptureInstance = replicationSlot ?? string.Empty,
			DatabaseName = databaseName ?? string.Empty,
			StalePosition = stalePosition.HasValue ? BitConverter.GetBytes(stalePosition.Value.LsnValue) : null,
			NewPosition = newPosition.HasValue ? BitConverter.GetBytes(newPosition.Value.LsnValue) : null,
			ReasonCode = reasonCode,
			OriginalException = exception,
			DetectedAt = DateTimeOffset.UtcNow,
			AdditionalContext = additionalContext.Count > 0 ? additionalContext : null
		};
	}

	/// <summary>
	/// Determines the appropriate reason code from a Postgres exception.
	/// </summary>
	public static string GetReasonCode(PostgresException? exception)
	{
		if (exception == null)
		{
			return PostgresStalePositionReasonCodes.Unknown;
		}

		return PostgresStalePositionReasonCodes.FromSqlState(exception.SqlState);
	}

	/// <summary>
	/// Determines if a SQLSTATE code indicates a stale position scenario.
	/// </summary>
	public static bool IsStalePositionSqlState(string? sqlState) =>
		!string.IsNullOrEmpty(sqlState) && StalePositionSqlStates.Contains(sqlState);

	private static bool IsStalePositionByMessage(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return false;
		}

		return message.Contains("WAL SEGMENT", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("REPLICATION SLOT", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("REQUESTED WAL", StringComparison.OrdinalIgnoreCase) ||
			   (message.Contains("PUBLICATION", StringComparison.OrdinalIgnoreCase) &&
				message.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase)) ||
			   message.Contains("LOGICAL DECODING", StringComparison.OrdinalIgnoreCase);
	}
}
