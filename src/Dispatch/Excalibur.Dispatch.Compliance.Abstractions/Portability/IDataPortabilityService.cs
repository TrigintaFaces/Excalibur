// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides GDPR Article 20 data portability capabilities for exporting personal data.
/// </summary>
/// <remarks>
/// <para>
/// This service enables data subjects to receive their personal data in a structured,
/// commonly used, and machine-readable format for transfer to another controller.
/// </para>
/// </remarks>
public interface IDataPortabilityService
{
	/// <summary>
	/// Exports all personal data for a data subject in the specified format.
	/// </summary>
	/// <param name="subjectId">The data subject identifier.</param>
	/// <param name="format">The export format.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The export result containing the export identifier and metadata.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="subjectId"/> is null or whitespace.</exception>
	Task<DataExportResult> ExportAsync(
		string subjectId,
		ExportFormat format,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the status of a previously initiated export operation.
	/// </summary>
	/// <param name="exportId">The export identifier returned from <see cref="ExportAsync"/>.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The export result, or null if not found.</returns>
	Task<DataExportResult?> GetExportStatusAsync(
		string exportId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of a data portability export operation.
/// </summary>
public sealed record DataExportResult
{
	/// <summary>
	/// Gets the unique identifier for this export.
	/// </summary>
	public required string ExportId { get; init; }

	/// <summary>
	/// Gets the format of the export.
	/// </summary>
	public required ExportFormat Format { get; init; }

	/// <summary>
	/// Gets the total size of the exported data in bytes.
	/// </summary>
	public long DataSize { get; init; }

	/// <summary>
	/// Gets the timestamp when the export was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the export data expires.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets the current status of the export.
	/// </summary>
	public ExportStatus Status { get; init; } = ExportStatus.Completed;
}

/// <summary>
/// Status of a data export operation.
/// </summary>
public enum ExportStatus
{
	/// <summary>
	/// The export is in progress.
	/// </summary>
	InProgress = 0,

	/// <summary>
	/// The export completed successfully.
	/// </summary>
	Completed = 1,

	/// <summary>
	/// The export failed.
	/// </summary>
	Failed = 2,

	/// <summary>
	/// The export data has expired and is no longer available.
	/// </summary>
	Expired = 3
}

/// <summary>
/// Configuration options for data portability export.
/// </summary>
public sealed class DataPortabilityOptions
{
	/// <summary>
	/// Gets or sets the directory where exports are stored.
	/// Default: "exports".
	/// </summary>
	public string ExportDirectory { get; set; } = "exports";

	/// <summary>
	/// Gets or sets the maximum export size in bytes.
	/// Default: 100 MB.
	/// </summary>
	public long MaxExportSize { get; set; } = 100 * 1024 * 1024;

	/// <summary>
	/// Gets or sets the retention period for exported data before automatic cleanup.
	/// Default: 7 days.
	/// </summary>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}
