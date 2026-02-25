// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Cdc;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Detects and classifies stale CDC position errors from SQL Server exceptions.
/// </summary>
/// <remarks>
/// <para>
/// SQL Server reports stale position scenarios through specific error numbers:
/// <list type="bullet">
/// <item><description>22037 - Invalid from_lsn specified for change data capture function</description></item>
/// <item><description>22029 - fn_cdc_get_all_changes was called with a from_lsn that is outside of valid range</description></item>
/// <item><description>22911 - CDC is not enabled for database</description></item>
/// <item><description>22985 - Capture instance not found</description></item>
/// </list>
/// </para>
/// </remarks>
public static class CdcStalePositionDetector
{
	/// <summary>
	/// SQL Server error number for invalid from_lsn in CDC function call.
	/// </summary>
	public const int InvalidFromLsnError = 22037;

	/// <summary>
	/// SQL Server error number for from_lsn outside valid range.
	/// </summary>
	public const int LsnOutOfRangeError = 22029;

	/// <summary>
	/// SQL Server error number for CDC not enabled on database.
	/// </summary>
	public const int CdcNotEnabledError = 22911;

	/// <summary>
	/// SQL Server error number for capture instance not found.
	/// </summary>
	public const int CaptureInstanceNotFoundError = 22985;

	/// <summary>
	/// Gets the set of SQL Server error numbers that indicate a stale CDC position.
	/// </summary>
	public static IReadOnlySet<int> StalePositionErrorNumbers { get; } = new HashSet<int>
	{
		InvalidFromLsnError,
		LsnOutOfRangeError,
		CdcNotEnabledError,
		CaptureInstanceNotFoundError
	};

	/// <summary>
	/// Maximum recursion depth for inner exception traversal to prevent StackOverflow from circular chains.
	/// </summary>
	private const int MaxExceptionDepth = 50;

	/// <summary>
	/// Determines whether the specified exception indicates a stale CDC position.
	/// </summary>
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// <see langword="true"/> if the exception indicates a stale position; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsStalePositionException(Exception? exception) =>
		IsStalePositionException(exception, 0);

	private static bool IsStalePositionException(Exception? exception, int depth)
	{
		if (exception == null || depth > MaxExceptionDepth)
		{
			return false;
		}

		return exception switch
		{
			SqlException sqlEx => ContainsStalePositionError(sqlEx),
			AggregateException aggEx => aggEx.InnerExceptions.Any(e => IsStalePositionException(e, depth + 1)),
			_ => IsStalePositionException(exception.InnerException, depth + 1)
		};
	}

	/// <summary>
	/// Extracts the SQL error number that indicates a stale position from the exception.
	/// </summary>
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// The SQL Server error number if found; otherwise, <see langword="null"/>.
	/// </returns>
	public static int? GetStalePositionErrorNumber(Exception? exception) =>
		GetStalePositionErrorNumber(exception, 0);

	private static int? GetStalePositionErrorNumber(Exception? exception, int depth)
	{
		if (exception == null || depth > MaxExceptionDepth)
		{
			return null;
		}

		return exception switch
		{
			SqlException sqlEx => GetFirstStalePositionError(sqlEx),
			AggregateException aggEx => aggEx.InnerExceptions
				.Select(e => GetStalePositionErrorNumber(e, depth + 1))
				.FirstOrDefault(n => n.HasValue),
			_ => GetStalePositionErrorNumber(exception.InnerException, depth + 1)
		};
	}

	/// <summary>
	/// Creates a <see cref="CdcPositionResetEventArgs"/> from an exception and context.
	/// </summary>
	/// <param name="exception">The exception that was caught.</param>
	/// <param name="processorId">The identifier of the CDC processor.</param>
	/// <param name="stalePosition">The LSN that was detected as stale.</param>
	/// <param name="newPosition">The new position to resume from, if known.</param>
	/// <param name="captureInstance">The affected capture instance, if known.</param>
	/// <param name="databaseName">The database name, if known.</param>
	/// <returns>A populated <see cref="CdcPositionResetEventArgs"/> instance.</returns>
	public static CdcPositionResetEventArgs CreateEventArgs(
		Exception exception,
		string processorId,
		byte[]? stalePosition = null,
		byte[]? newPosition = null,
		string? captureInstance = null,
		string? databaseName = null)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var errorNumber = GetStalePositionErrorNumber(exception);
		var reasonCode = errorNumber.HasValue
			? StalePositionReasonCodes.FromSqlError(errorNumber.Value)
			: StalePositionReasonCodes.Unknown;

		return new CdcPositionResetEventArgs
		{
			ProcessorId = processorId,
			ProviderType = "SqlServer",
			CaptureInstance = captureInstance ?? string.Empty,
			DatabaseName = databaseName ?? string.Empty,
			StalePosition = stalePosition,
			NewPosition = newPosition,
			ReasonCode = reasonCode,
			OriginalException = exception,
			DetectedAt = DateTimeOffset.UtcNow,
			AdditionalContext = errorNumber.HasValue
				? new Dictionary<string, object> { ["SqlErrorNumber"] = errorNumber.Value }
				: null
		};
	}

	/// <summary>
	/// Determines the appropriate reason code from a SQL exception.
	/// </summary>
	/// <param name="exception">The SQL exception to analyze.</param>
	/// <returns>The reason code string.</returns>
	public static string GetReasonCode(SqlException? exception)
	{
		if (exception == null)
		{
			return StalePositionReasonCodes.Unknown;
		}

		var errorNumber = GetFirstStalePositionError(exception);
		return errorNumber.HasValue
			? StalePositionReasonCodes.FromSqlError(errorNumber.Value)
			: StalePositionReasonCodes.Unknown;
	}

	private static bool ContainsStalePositionError(SqlException sqlException) =>
		sqlException.Errors.Cast<SqlError>()
			.Any(error => StalePositionErrorNumbers.Contains(error.Number));

	private static int? GetFirstStalePositionError(SqlException sqlException) =>
		sqlException.Errors.Cast<SqlError>()
			.Where(error => StalePositionErrorNumbers.Contains(error.Number))
			.Select(error => (int?)error.Number)
			.FirstOrDefault();
}
