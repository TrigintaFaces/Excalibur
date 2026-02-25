// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.SqlServer.Persistence;

internal sealed partial class SqlServerRequestValidator
{
	private static readonly string[] DisallowedPatterns = ["--", "/*", "*/", "XP_", "SP_", "EXEC(", "EXECUTE("];

	private readonly ILogger<SqlServerPersistenceProvider> _logger;

	internal SqlServerRequestValidator(ILogger<SqlServerPersistenceProvider> logger) =>
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

	internal bool ValidateRequest<TResult>(IDataRequest<IDbConnection, TResult> request)
	{
		if (request == null)
		{
			return false;
		}

		try
		{
			var command = request.Command;
			if (string.IsNullOrWhiteSpace(command.CommandText))
			{
				return false;
			}

			var sql = command.CommandText.ToUpperInvariant();
			foreach (var pattern in DisallowedPatterns)
			{
				if (sql.Contains(pattern, StringComparison.Ordinal) && !IsValidSqlPattern(sql, pattern))
				{
					LogUnsafeSqlPattern(_logger, pattern);
					return false;
				}
			}

			return true;
		}
		catch (Exception ex)
		{
			LogValidationError(_logger, request.GetType().Name, ex);
			return false;
		}
	}

	private static bool IsValidSqlPattern(string sql, string pattern) =>
		pattern switch
		{
			"--" => sql.Contains("-- ", StringComparison.Ordinal) ||
					sql.EndsWith("--", StringComparison.Ordinal),
			"/*" => sql.Contains("/*", StringComparison.Ordinal) &&
					sql.Contains("*/", StringComparison.Ordinal),
			"*/" => sql.Contains("/*", StringComparison.Ordinal) &&
					sql.Contains("*/", StringComparison.Ordinal),
			"xp_" => false,
			"sp_" => sql.Contains("sp_executesql", StringComparison.OrdinalIgnoreCase) ||
					 sql.Contains("sp_helpdb", StringComparison.OrdinalIgnoreCase),
			"exec(" => false,
			"execute(" => false,
			_ => true,
		};

	[LoggerMessage(DataSqlServerEventId.PersistenceUnsafeSqlPattern, LogLevel.Warning, "DataRequest contains potentially unsafe SQL pattern: {Pattern}")]
	private static partial void LogUnsafeSqlPattern(ILogger logger, string pattern);

	[LoggerMessage(DataSqlServerEventId.PersistenceValidationError, LogLevel.Error, "Error validating DataRequest {RequestType}")]
	private static partial void LogValidationError(ILogger logger, string requestType, Exception exception);
}
