// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

using Npgsql;

namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Detects and classifies stale CDC position errors from Postgres exceptions.
/// </summary>
/// <remarks>
/// <para>
/// Postgres reports stale position scenarios through specific SQLSTATE codes:
/// <list type="bullet">
/// <item><description>58P01 - could_not_access_file (WAL segment removed)</description></item>
/// <item><description>55000 - object_not_in_prerequisite_state (replication slot invalid)</description></item>
/// <item><description>42704 - undefined_object (publication/slot not found)</description></item>
/// <item><description>08006 - connection_failure (replication stream lost)</description></item>
/// <item><description>08P01 - protocol_violation (logical decoding error)</description></item>
/// <item><description>0A000 - feature_not_supported (wal_level not logical)</description></item>
/// </list>
/// </para>
/// </remarks>
public static class PostgresStalePositionDetector
{
	/// <summary>
	/// Postgres SQLSTATE for WAL segment not accessible (could_not_access_file).
	/// </summary>
	public const string WalSegmentNotAccessible = "58P01";

	/// <summary>
	/// Postgres SQLSTATE for object not in prerequisite state (replication slot invalid).
	/// </summary>
	public const string ObjectNotInPrerequisiteState = "55000";

	/// <summary>
	/// Postgres SQLSTATE for undefined object (publication/slot not found).
	/// </summary>
	public const string UndefinedObject = "42704";

	/// <summary>
	/// Postgres SQLSTATE for connection failure.
	/// </summary>
	public const string ConnectionFailure = "08006";

	/// <summary>
	/// Postgres SQLSTATE for protocol violation (logical decoding issues).
	/// </summary>
	public const string ProtocolViolation = "08P01";

	/// <summary>
	/// Postgres SQLSTATE for feature not supported (e.g., wal_level not logical).
	/// </summary>
	public const string FeatureNotSupported = "0A000";

	/// <summary>
	/// Gets the set of Postgres SQLSTATE codes that indicate a stale CDC position.
	/// </summary>
	public static IReadOnlySet<string> StalePositionSqlStates { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		WalSegmentNotAccessible,
		ObjectNotInPrerequisiteState,
		UndefinedObject,
		ConnectionFailure,
		ProtocolViolation,
		FeatureNotSupported
	};

	/// <summary>
	/// Determines whether the specified exception indicates a stale CDC position.
	/// </summary>
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// <see langword="true"/> if the exception indicates a stale position; otherwise, <see langword="false"/>.
	/// </returns>
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
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// The Postgres SQLSTATE code if found; otherwise, <see langword="null"/>.
	/// </returns>
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
	/// <param name="exception">The exception that was caught.</param>
	/// <param name="processorId">The identifier of the CDC processor.</param>
	/// <param name="stalePosition">The WAL position that was detected as stale.</param>
	/// <param name="newPosition">The new position to resume from, if known.</param>
	/// <param name="replicationSlot">The affected replication slot name, if known.</param>
	/// <param name="publication">The affected publication name, if known.</param>
	/// <param name="databaseName">The database name, if known.</param>
	/// <returns>A populated <see cref="CdcPositionResetEventArgs"/> instance.</returns>
	public static CdcPositionResetEventArgs CreateEventArgs(
		Exception exception,
		string processorId,
		PostgresCdcPosition? stalePosition = null,
		PostgresCdcPosition? newPosition = null,
		string? replicationSlot = null,
		string? publication = null,
		string? databaseName = null)
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
	/// <param name="exception">The Postgres exception to analyze.</param>
	/// <returns>The reason code string.</returns>
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
	/// <param name="sqlState">The Postgres SQLSTATE code.</param>
	/// <returns><see langword="true"/> if the SQLSTATE indicates stale position.</returns>
	public static bool IsStalePositionSqlState(string? sqlState) =>
		!string.IsNullOrEmpty(sqlState) && StalePositionSqlStates.Contains(sqlState);

	/// <summary>
	/// Determines if an error message indicates a stale position scenario.
	/// </summary>
	/// <param name="message">The error message to analyze.</param>
	/// <returns><see langword="true"/> if the message indicates stale position.</returns>
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
