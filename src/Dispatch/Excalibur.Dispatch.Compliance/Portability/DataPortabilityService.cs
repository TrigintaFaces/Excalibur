// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="IDataPortabilityService"/> providing GDPR Article 20
/// data portability export capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This in-memory implementation is suitable for development and testing.
/// Production deployments should use a persistent store-backed implementation.
/// </para>
/// </remarks>
public sealed partial class DataPortabilityService : IDataPortabilityService
{
	private readonly ConcurrentDictionary<string, DataExportResult> _exports = new(StringComparer.OrdinalIgnoreCase);
	private readonly IDataInventoryService? _dataInventoryService;
	private readonly IOptions<DataPortabilityOptions> _options;
	private readonly ILogger<DataPortabilityService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataPortabilityService"/> class.
	/// </summary>
	/// <param name="options">The data portability options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="dataInventoryService">Optional data inventory service for discovering data locations.</param>
	public DataPortabilityService(
		IOptions<DataPortabilityOptions> options,
		ILogger<DataPortabilityService> logger,
		IDataInventoryService? dataInventoryService = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dataInventoryService = dataInventoryService;
	}

	/// <inheritdoc />
	public async Task<DataExportResult> ExportAsync(
		string subjectId,
		ExportFormat format,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

		LogDataPortabilityExportStarted(subjectId, format);

		try
		{
			var exportId = Guid.NewGuid().ToString("N");
			var now = DateTimeOffset.UtcNow;

			// Discover data size via inventory if available
			long dataSize = 0;
			if (_dataInventoryService is not null)
			{
				var inventory = await _dataInventoryService.DiscoverAsync(
					subjectId, DataSubjectIdType.UserId, tenantId: null, cancellationToken)
					.ConfigureAwait(false);
				dataSize = inventory.Locations.Count * 1024L; // Estimate
			}

			var result = new DataExportResult
			{
				ExportId = exportId,
				Format = format,
				DataSize = dataSize,
				CreatedAt = now,
				ExpiresAt = now.Add(_options.Value.RetentionPeriod),
				Status = ExportStatus.Completed
			};

			_exports[exportId] = result;

			LogDataPortabilityExportCompleted(exportId, subjectId, dataSize);

			return result;
		}
		catch (Exception ex)
		{
			LogDataPortabilityExportFailed(subjectId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task<DataExportResult?> GetExportStatusAsync(
		string exportId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(exportId);

		_exports.TryGetValue(exportId, out var result);

		// Check if expired
		if (result is not null && result.ExpiresAt.HasValue && DateTimeOffset.UtcNow > result.ExpiresAt)
		{
			result = result with { Status = ExportStatus.Expired };
		}

		return Task.FromResult(result);
	}

	[LoggerMessage(
		ComplianceEventId.DataPortabilityExportStarted,
		LogLevel.Information,
		"Starting data portability export for subject {SubjectId} in format {Format}")]
	private partial void LogDataPortabilityExportStarted(string subjectId, ExportFormat format);

	[LoggerMessage(
		ComplianceEventId.DataPortabilityExportCompleted,
		LogLevel.Information,
		"Data portability export {ExportId} completed for subject {SubjectId}. Data size: {DataSize} bytes")]
	private partial void LogDataPortabilityExportCompleted(string exportId, string subjectId, long dataSize);

	[LoggerMessage(
		ComplianceEventId.DataPortabilityExportFailed,
		LogLevel.Error,
		"Data portability export failed for subject {SubjectId}")]
	private partial void LogDataPortabilityExportFailed(string subjectId, Exception exception);
}
