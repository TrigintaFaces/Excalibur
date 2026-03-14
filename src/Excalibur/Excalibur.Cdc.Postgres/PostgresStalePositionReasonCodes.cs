// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Provides standardized reason codes for Postgres CDC stale position scenarios.
/// </summary>
public static class PostgresStalePositionReasonCodes
{
	/// <summary>The WAL position is no longer available due to segment removal.</summary>
	public const string WalPositionStale = "WAL_POSITION_STALE";

	/// <summary>The replication slot is invalid or has been dropped.</summary>
	public const string ReplicationSlotInvalid = "REPLICATION_SLOT_INVALID";

	/// <summary>A logical decoding error occurred while processing changes.</summary>
	public const string LogicalDecodingError = "LOGICAL_DECODING_ERROR";

	/// <summary>The publication no longer exists or has been modified.</summary>
	public const string PublicationInvalid = "PUBLICATION_INVALID";

	/// <summary>The connection to the replication stream was lost unexpectedly.</summary>
	public const string ReplicationStreamDisconnected = "REPLICATION_STREAM_DISCONNECTED";

	/// <summary>The reason for the stale position could not be determined.</summary>
	public const string Unknown = "UNKNOWN";

	/// <summary>
	/// Determines the reason code from a Postgres SQLSTATE code.
	/// </summary>
	public static string FromSqlState(string? sqlState) =>
		sqlState switch
		{
			"58P01" => WalPositionStale,
			"55000" => ReplicationSlotInvalid,
			"42704" => PublicationInvalid,
			"08006" => ReplicationStreamDisconnected,
			"08P01" => LogicalDecodingError,
			"0A000" => LogicalDecodingError,
			_ => Unknown
		};

	/// <summary>
	/// Determines the reason code from a Postgres error message pattern.
	/// </summary>
	public static string FromErrorMessage(string? errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage))
		{
			return Unknown;
		}

		if (errorMessage.Contains("WAL SEGMENT", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("WAL FILE", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("REQUESTED WAL", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("WAL POSITION", StringComparison.OrdinalIgnoreCase))
		{
			return WalPositionStale;
		}

		if (errorMessage.Contains("REPLICATION SLOT", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DROPPED", StringComparison.OrdinalIgnoreCase)))
		{
			return ReplicationSlotInvalid;
		}

		if (errorMessage.Contains("PUBLICATION", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("DOES NOT EXIST", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("DROPPED", StringComparison.OrdinalIgnoreCase)))
		{
			return PublicationInvalid;
		}

		if (errorMessage.Contains("LOGICAL DECODING", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("PGOUTPUT", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("OUTPUT PLUGIN", StringComparison.OrdinalIgnoreCase))
		{
			return LogicalDecodingError;
		}

		if (errorMessage.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase) &&
			(errorMessage.Contains("LOST", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("CLOSED", StringComparison.OrdinalIgnoreCase) ||
			 errorMessage.Contains("TERMINATED", StringComparison.OrdinalIgnoreCase)))
		{
			return ReplicationStreamDisconnected;
		}

		return Unknown;
	}
}
