// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Portability;

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

		// THIS IMPLEMENTATION REFUSES. It does not export, and it will not say that it did.
		//
		// What stood here returned Status = ExportStatus.Completed unconditionally, having read nothing,
		// serialised nothing and written nothing -- and filled DataSize with
		// `inventory.Locations.Count * 1024L`, a fabrication, into a field documented as the byte count of
		// the exported data. The audience for that answer is a regulator, and the consumer had no way to
		// detect it from outside: a Completed with a plausible size is indistinguishable from a real export.
		//
		// The framework cannot implement Article 20 on a consumer's behalf. It does not know the shape of
		// their data, how to serialise it, or where the export should land -- DataExportResult carries no
		// payload, path or URI, so there is nowhere for one to go. An operation that cannot be performed
		// must report that it was not performed, and the loudest available report is a refusal.
		//
		// A consumer supplies their own: the registration uses TryAddScoped, so registering
		// IDataPortabilityService before calling AddDataPortability() takes precedence and this type is
		// never constructed.
		await Task.CompletedTask.ConfigureAwait(false);

		throw new NotSupportedException(
			$"No data-portability exporter is registered, so the GDPR Article 20 request for subject "
			+ $"'{subjectId}' ({format}) was NOT performed. This built-in implementation deliberately "
			+ "refuses rather than reporting a completed export it did not make -- the previous behaviour "
			+ "returned Completed with a fabricated data size, which would be presented to a regulator as "
			+ "evidence. Register your own IDataPortabilityService BEFORE calling AddDataPortability(): the "
			+ "registration is TryAdd, so yours wins. Only you can decide what to export, in what shape, "
			+ "and where to put it.");
	}

	/// <inheritdoc />
	/// <remarks>
	/// Always <see langword="null"/>. This implementation refuses to export, so no export is ever
	/// recorded and there is no status to report. It previously read a dictionary that only
	/// <c>ExportAsync</c> wrote to, and applied an expiry rule to its entries; with the export refusing,
	/// that dictionary could never have an entry and the expiry branch could never run. Unreachable code
	/// that looks like a working status lookup is its own small lie, so it is gone rather than left.
	/// </remarks>
	public Task<DataExportResult?> GetExportStatusAsync(
		string exportId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(exportId);

		return Task.FromResult<DataExportResult?>(null);
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
