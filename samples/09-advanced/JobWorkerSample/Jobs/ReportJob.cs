// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;

namespace JobWorkerSample.Jobs;

/// <summary>
///     A sample job that generates reports with different frequencies (daily, weekly, monthly).
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="ReportJob" /> class.
/// </remarks>
/// <param name="logger"> The logger instance. </param>
public class ReportJob(ILogger<ReportJob> logger) : IBackgroundJob
{
	private readonly ILogger<ReportJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("📊 Starting report generation job at {Timestamp}", DateTimeOffset.UtcNow);

		try
		{
			var reportType = DetermineReportType();
			await GenerateReportAsync(reportType, cancellationToken).ConfigureAwait(false);
			await SaveReportAsync(reportType, cancellationToken).ConfigureAwait(false);
			await NotifyStakeholdersAsync(reportType, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("📊 Report generation job completed successfully for {ReportType}", reportType);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "📊 Report generation job failed");
			throw;
		}
	}

	/// <summary>
	///     Determines the type of report to generate based on the current date.
	/// </summary>
	/// <returns> The report type. </returns>
	private string DetermineReportType()
	{
		var now = DateTimeOffset.UtcNow;

		// Monthly report on the 1st of each month
		if (now.Day == 1)
		{
			return "Monthly";
		}

		// Weekly report on Mondays
		if (now.DayOfWeek == DayOfWeek.Monday)
		{
			return "Weekly";
		}

		// Default to daily report
		return "Daily";
	}

	/// <summary>
	///     Generates a report of the specified type.
	/// </summary>
	/// <param name="reportType"> The type of report to generate. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task GenerateReportAsync(string reportType, CancellationToken cancellationToken)
	{
		_logger.LogDebug("📊 Generating {ReportType} report", reportType);

		var generationTime = reportType switch
		{
			"Monthly" => TimeSpan.FromSeconds(5), // Monthly reports take longer
			"Weekly" => TimeSpan.FromSeconds(3), // Weekly reports take medium time
			"Daily" => TimeSpan.FromSeconds(1), // Daily reports are quick
			_ => TimeSpan.FromSeconds(1)
		};

		// Simulate report generation
		await Task.Delay(generationTime, cancellationToken).ConfigureAwait(false);

		var reportData = GenerateReportData(reportType);

		_logger.LogInformation("📊 {ReportType} report generated: {@ReportData}", reportType, reportData);
	}

	/// <summary>
	///     Generates sample report data based on the report type.
	/// </summary>
	/// <param name="reportType"> The type of report. </param>
	/// <returns> Sample report data. </returns>
	private object GenerateReportData(string reportType)
	{
		var baseData = new
		{
			ReportType = reportType,
			GeneratedAt = DateTimeOffset.UtcNow,
			TotalTransactions = Random.Shared.Next(1000, 10000),
			TotalRevenue = Random.Shared.Next(50000, 500000),
			ActiveUsers = Random.Shared.Next(100, 1000),
			ErrorRate = Math.Round(Random.Shared.NextDouble() * 5, 2) // 0-5% error rate
		};

		return reportType switch
		{
			"Monthly" => new
			{
				baseData.ReportType,
				baseData.GeneratedAt,
				baseData.TotalTransactions,
				baseData.TotalRevenue,
				baseData.ActiveUsers,
				baseData.ErrorRate,
				MonthlyGrowth = Math.Round(Random.Shared.NextDouble() * 20 - 5, 2), // -5% to +15%
				NewCustomers = Random.Shared.Next(50, 200),
				ChurnRate = Math.Round(Random.Shared.NextDouble() * 10, 2) // 0-10% churn
			},
			"Weekly" => new
			{
				baseData.ReportType,
				baseData.GeneratedAt,
				baseData.TotalTransactions,
				baseData.TotalRevenue,
				baseData.ActiveUsers,
				baseData.ErrorRate,
				WeeklyGrowth = Math.Round(Random.Shared.NextDouble() * 10 - 2, 2), // -2% to +8%
				PeakDay = ((DayOfWeek)Random.Shared.Next(0, 7)).ToString()
			},
			_ => baseData
		};
	}

	/// <summary>
	///     Simulates saving the report to storage.
	/// </summary>
	/// <param name="reportType"> The type of report. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task SaveReportAsync(string reportType, CancellationToken cancellationToken)
	{
		_logger.LogDebug("📊 Saving {ReportType} report to storage", reportType);

		// Simulate saving to storage
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

		var fileName = $"{reportType.ToLowerInvariant()}_report_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.json";

		_logger.LogDebug("📊 Report saved as {FileName}", fileName);
	}

	/// <summary>
	///     Simulates notifying stakeholders about the report.
	/// </summary>
	/// <param name="reportType"> The type of report. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task NotifyStakeholdersAsync(string reportType, CancellationToken cancellationToken)
	{
		_logger.LogDebug("📊 Notifying stakeholders about {ReportType} report", reportType);

		var stakeholders = reportType switch
		{
			"Monthly" => new[] { "CEO", "CFO", "VP Sales", "VP Marketing", "Board Members" },
			"Weekly" => new[] { "VP Sales", "VP Marketing", "Operations Manager" },
			"Daily" => new[] { "Operations Manager", "Customer Success" },
			_ => new[] { "Operations Manager" }
		};

		// Simulate notification delivery
		foreach (var stakeholder in stakeholders)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
			_logger.LogDebug("📊 Notification sent to {Stakeholder}", stakeholder);
		}

		_logger.LogDebug("📊 All stakeholders notified about {ReportType} report", reportType);
	}
}
