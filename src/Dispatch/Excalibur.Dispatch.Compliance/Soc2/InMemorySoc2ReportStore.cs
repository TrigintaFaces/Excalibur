// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// In-memory implementation of <see cref="ISoc2ReportStore"/> for development and testing.
/// </summary>
/// <remarks>
/// This implementation stores all reports in memory and is NOT suitable for production use.
/// Data is lost when the application restarts.
/// </remarks>
public sealed class InMemorySoc2ReportStore : ISoc2ReportStore
{
	private readonly ConcurrentDictionary<Guid, Soc2Report> _reports = new();

	/// <summary>
	/// Gets the total count of reports in the store.
	/// </summary>
	public int ReportCount => _reports.Count;

	/// <inheritdoc />
	public Task SaveReportAsync(
		Soc2Report report,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(report);

		_reports[report.ReportId] = report;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<Soc2Report?> GetReportAsync(
		Guid reportId,
		CancellationToken cancellationToken)
	{
		_ = _reports.TryGetValue(reportId, out var report);
		return Task.FromResult(report);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ReportSummary>> ListReportsAsync(
		ReportFilter filter,
		CancellationToken cancellationToken)
	{
		var query = _reports.Values.AsEnumerable();

		// Apply filters
		if (filter.ReportType.HasValue)
		{
			query = query.Where(r => r.ReportType == filter.ReportType.Value);
		}

		if (!string.IsNullOrEmpty(filter.TenantId))
		{
			query = query.Where(r => r.TenantId == filter.TenantId);
		}

		if (filter.GeneratedAfter.HasValue)
		{
			query = query.Where(r => r.GeneratedAt >= filter.GeneratedAfter.Value);
		}

		if (filter.GeneratedBefore.HasValue)
		{
			query = query.Where(r => r.GeneratedAt <= filter.GeneratedBefore.Value);
		}

		if (filter.PeriodStartAfter.HasValue)
		{
			query = query.Where(r => r.PeriodStart >= filter.PeriodStartAfter.Value);
		}

		if (filter.PeriodEndBefore.HasValue)
		{
			query = query.Where(r => r.PeriodEnd <= filter.PeriodEndBefore.Value);
		}

		if (filter.Opinion.HasValue)
		{
			query = query.Where(r => r.Opinion == filter.Opinion.Value);
		}

		// Apply sort order
		query = filter.SortOrder switch
		{
			ReportSortOrder.GeneratedAtDescending => query.OrderByDescending(r => r.GeneratedAt),
			ReportSortOrder.GeneratedAtAscending => query.OrderBy(r => r.GeneratedAt),
			ReportSortOrder.PeriodEndDescending => query.OrderByDescending(r => r.PeriodEnd),
			ReportSortOrder.PeriodEndAscending => query.OrderBy(r => r.PeriodEnd),
			_ => query.OrderByDescending(r => r.GeneratedAt)
		};

		// Apply pagination
		if (filter.Skip.HasValue)
		{
			query = query.Skip(filter.Skip.Value);
		}

		if (filter.MaxResults.HasValue)
		{
			query = query.Take(filter.MaxResults.Value);
		}

		// Project to summaries
		var summaries = query.Select(ToSummary).ToList();

		return Task.FromResult<IReadOnlyList<ReportSummary>>(summaries);
	}

	/// <inheritdoc />
	public Task<bool> DeleteReportAsync(
		Guid reportId,
		CancellationToken cancellationToken)
	{
		return Task.FromResult(_reports.TryRemove(reportId, out _));
	}

	/// <inheritdoc />
	public Task<int> GetReportCountAsync(
		ReportFilter filter,
		CancellationToken cancellationToken)
	{
		var query = _reports.Values.AsEnumerable();

		// Apply same filters as ListReportsAsync
		if (filter.ReportType.HasValue)
		{
			query = query.Where(r => r.ReportType == filter.ReportType.Value);
		}

		if (!string.IsNullOrEmpty(filter.TenantId))
		{
			query = query.Where(r => r.TenantId == filter.TenantId);
		}

		if (filter.GeneratedAfter.HasValue)
		{
			query = query.Where(r => r.GeneratedAt >= filter.GeneratedAfter.Value);
		}

		if (filter.GeneratedBefore.HasValue)
		{
			query = query.Where(r => r.GeneratedAt <= filter.GeneratedBefore.Value);
		}

		if (filter.PeriodStartAfter.HasValue)
		{
			query = query.Where(r => r.PeriodStart >= filter.PeriodStartAfter.Value);
		}

		if (filter.PeriodEndBefore.HasValue)
		{
			query = query.Where(r => r.PeriodEnd <= filter.PeriodEndBefore.Value);
		}

		if (filter.Opinion.HasValue)
		{
			query = query.Where(r => r.Opinion == filter.Opinion.Value);
		}

		return Task.FromResult(query.Count());
	}

	/// <summary>
	/// Clears all reports from the store.
	/// </summary>
	public void Clear()
	{
		_reports.Clear();
	}

	private static ReportSummary ToSummary(Soc2Report report) =>
		new()
		{
			ReportId = report.ReportId,
			ReportType = report.ReportType,
			Title = report.Title,
			PeriodStart = report.PeriodStart,
			PeriodEnd = report.PeriodEnd,
			GeneratedAt = report.GeneratedAt,
			Opinion = report.Opinion,
			ExceptionCount = report.Exceptions.Count,
			CategoriesIncluded = report.CategoriesIncluded,
			TenantId = report.TenantId
		};
}
