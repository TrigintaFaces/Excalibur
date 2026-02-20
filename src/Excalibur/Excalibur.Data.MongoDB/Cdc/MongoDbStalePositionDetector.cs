// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Cdc;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Detects and classifies stale CDC position errors from MongoDB exceptions.
/// </summary>
/// <remarks>
/// <para>
/// MongoDB reports stale position scenarios through specific error codes:
/// <list type="bullet">
/// <item><description>136 - ChangeStreamHistoryLost (resume token not in oplog)</description></item>
/// <item><description>286 - ChangeStreamFatalError (invalid resume token)</description></item>
/// <item><description>26 - NamespaceNotFound (collection dropped)</description></item>
/// <item><description>73 - InvalidNamespace (collection renamed)</description></item>
/// <item><description>133 - StaleShardVersion (shard migration)</description></item>
/// </list>
/// </para>
/// </remarks>
public static class MongoDbStalePositionDetector
{
	/// <summary>
	/// MongoDB error code for ChangeStreamHistoryLost (resume token not in oplog).
	/// </summary>
	public const int ChangeStreamHistoryLost = 136;

	/// <summary>
	/// MongoDB error code for ChangeStreamFatalError (invalid resume token).
	/// </summary>
	public const int ChangeStreamFatalError = 286;

	/// <summary>
	/// MongoDB error code for NamespaceNotFound (collection dropped).
	/// </summary>
	public const int NamespaceNotFound = 26;

	/// <summary>
	/// MongoDB error code for InvalidNamespace (collection renamed).
	/// </summary>
	public const int InvalidNamespace = 73;

	/// <summary>
	/// MongoDB error code for StaleShardVersion (shard migration).
	/// </summary>
	public const int StaleShardVersion = 133;

	/// <summary>
	/// Gets the set of MongoDB error codes that indicate a stale CDC position.
	/// </summary>
	public static IReadOnlySet<int> StalePositionErrorCodes { get; } = new HashSet<int>
	{
		ChangeStreamHistoryLost,
		ChangeStreamFatalError,
		NamespaceNotFound,
		InvalidNamespace,
		StaleShardVersion
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
			MongoCommandException cmdEx => IsStalePositionErrorCode(cmdEx.Code),
			MongoException mongoEx => IsStalePositionByMessage(mongoEx.Message) ||
									  IsStalePositionException(mongoEx.InnerException),
			AggregateException aggEx => aggEx.InnerExceptions.Any(IsStalePositionException),
			_ => IsStalePositionException(exception.InnerException)
		};
	}

	/// <summary>
	/// Extracts the error code that indicates a stale position from the exception.
	/// </summary>
	/// <param name="exception">The exception to analyze.</param>
	/// <returns>
	/// The MongoDB error code if found; otherwise, <see langword="null"/>.
	/// </returns>
	public static int? GetStalePositionErrorCode(Exception? exception)
	{
		if (exception == null)
		{
			return null;
		}

		return exception switch
		{
			MongoCommandException cmdEx when IsStalePositionErrorCode(cmdEx.Code) => cmdEx.Code,
			MongoException mongoEx => GetStalePositionErrorCode(mongoEx.InnerException),
			AggregateException aggEx => aggEx.InnerExceptions
				.Select(GetStalePositionErrorCode)
				.FirstOrDefault(c => c.HasValue),
			_ => GetStalePositionErrorCode(exception.InnerException)
		};
	}

	/// <summary>
	/// Creates a <see cref="CdcPositionResetEventArgs"/> from an exception and context.
	/// </summary>
	/// <param name="exception">The exception that was caught.</param>
	/// <param name="processorId">The identifier of the CDC processor.</param>
	/// <param name="stalePosition">The resume token position that was detected as stale.</param>
	/// <param name="newPosition">The new position to resume from, if known.</param>
	/// <param name="databaseName">The affected database name, if known.</param>
	/// <param name="collectionName">The affected collection name, if known.</param>
	/// <returns>A populated <see cref="CdcPositionResetEventArgs"/> instance.</returns>
	public static CdcPositionResetEventArgs CreateEventArgs(
		Exception exception,
		string processorId,
		MongoDbCdcPosition? stalePosition = null,
		MongoDbCdcPosition? newPosition = null,
		string? databaseName = null,
		string? collectionName = null)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		var errorCode = GetStalePositionErrorCode(exception);
		var reasonCode = errorCode.HasValue
			? MongoDbStalePositionReasonCodes.FromErrorCode(errorCode.Value)
			: MongoDbStalePositionReasonCodes.FromErrorMessage(exception.Message);

		// Build additional context with MongoDB-specific data
		var additionalContext = new Dictionary<string, object>();
		if (errorCode.HasValue)
		{
			additionalContext["ErrorCode"] = errorCode.Value;
		}

		if (databaseName is not null)
		{
			additionalContext["DatabaseName"] = databaseName;
		}

		if (collectionName is not null)
		{
			additionalContext["CollectionName"] = collectionName;
		}

		// Convert MongoDbCdcPosition to byte[] (using UTF-8 encoding of the JSON token string)
		var stalePositionBytes = stalePosition?.TokenString is not null
			? Encoding.UTF8.GetBytes(stalePosition.Value.TokenString)
			: null;

		var newPositionBytes = newPosition?.TokenString is not null
			? Encoding.UTF8.GetBytes(newPosition.Value.TokenString)
			: null;

		// Determine capture instance from database/collection
		var captureInstance = !string.IsNullOrEmpty(databaseName) && !string.IsNullOrEmpty(collectionName)
			? $"{databaseName}.{collectionName}"
			: databaseName ?? collectionName ?? string.Empty;

		return new CdcPositionResetEventArgs
		{
			ProcessorId = processorId,
			ProviderType = "MongoDB",
			CaptureInstance = captureInstance,
			DatabaseName = databaseName ?? string.Empty,
			ReasonCode = reasonCode,
			ReasonMessage = exception.Message,
			StalePosition = stalePositionBytes,
			NewPosition = newPositionBytes,
			OriginalException = exception,
			DetectedAt = DateTimeOffset.UtcNow,
			AdditionalContext = additionalContext.Count > 0 ? additionalContext : null
		};
	}

	/// <summary>
	/// Determines the appropriate reason code from a MongoDB exception.
	/// </summary>
	/// <param name="exception">The MongoDB exception to analyze.</param>
	/// <returns>The reason code string.</returns>
	public static string GetReasonCode(MongoException? exception)
	{
		if (exception == null)
		{
			return MongoDbStalePositionReasonCodes.Unknown;
		}

		if (exception is MongoCommandException cmdEx)
		{
			return MongoDbStalePositionReasonCodes.FromErrorCode(cmdEx.Code);
		}

		return MongoDbStalePositionReasonCodes.FromErrorMessage(exception.Message);
	}

	/// <summary>
	/// Determines if an error code indicates a stale position scenario.
	/// </summary>
	/// <param name="errorCode">The MongoDB error code.</param>
	/// <returns><see langword="true"/> if the error code indicates stale position.</returns>
	public static bool IsStalePositionErrorCode(int errorCode) =>
		StalePositionErrorCodes.Contains(errorCode);

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

		return message.Contains("RESUME TOKEN", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("RESUMETOKEN", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("OPLOG", StringComparison.OrdinalIgnoreCase) ||
			   message.Contains("CHANGE STREAM HISTORY", StringComparison.OrdinalIgnoreCase) ||
			   (message.Contains("CHANGESTREAM", StringComparison.OrdinalIgnoreCase) &&
			   (message.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("HISTORY", StringComparison.OrdinalIgnoreCase) ||
				message.Contains("LOST", StringComparison.OrdinalIgnoreCase))) ||
			   (message.Contains("COLLECTION", StringComparison.OrdinalIgnoreCase) &&
				(message.Contains("DROPPED", StringComparison.OrdinalIgnoreCase) ||
				 message.Contains("NOT FOUND", StringComparison.OrdinalIgnoreCase)));
	}
}
