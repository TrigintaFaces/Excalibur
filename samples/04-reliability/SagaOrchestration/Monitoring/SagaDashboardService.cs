// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using SagaOrchestration.Sagas;

namespace SagaOrchestration.Monitoring;

/// <summary>
/// Service for monitoring saga status and generating dashboard aggregates.
/// </summary>
/// <remarks>
/// <para>
/// This service provides operational visibility into saga execution:
/// <list type="bullet">
///   <item>Query saga status by ID</item>
///   <item>Detect stuck sagas (no progress for N minutes)</item>
///   <item>List active/failed/compensating sagas</item>
///   <item>Generate dashboard aggregates (counts, success rate)</item>
/// </list>
/// </para>
/// <para>
/// In production, this would integrate with your observability stack
/// (Prometheus metrics, Grafana dashboards, alerts, etc.).
/// </para>
/// </remarks>
public sealed partial class SagaDashboardService
{
	private readonly ISagaStateStore _stateStore;
	private readonly ILogger<SagaDashboardService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaDashboardService"/> class.
	/// </summary>
	public SagaDashboardService(ISagaStateStore stateStore, ILogger<SagaDashboardService> logger)
	{
		_stateStore = stateStore;
		_logger = logger;
	}

	/// <summary>
	/// Gets the status of a specific saga.
	/// </summary>
	public async Task<SagaStatusReport?> GetSagaStatusAsync(string sagaId, CancellationToken cancellationToken)
	{
		var data = await _stateStore.GetAsync(sagaId, cancellationToken).ConfigureAwait(false);
		if (data == null)
		{
			return null;
		}

		return new SagaStatusReport
		{
			SagaId = data.SagaId,
			OrderId = data.OrderId,
			Status = data.Status,
			CompletedSteps = [.. data.CompletedSteps],
			CreatedAt = data.CreatedAt,
			LastUpdatedAt = data.LastUpdatedAt,
			FailureReason = data.FailureReason,
		};
	}

	/// <summary>
	/// Gets all sagas that appear stuck (no progress for the specified threshold).
	/// </summary>
	/// <param name="stuckThreshold">How long without updates before a saga is considered stuck.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of stuck saga reports.</returns>
	public async Task<List<SagaStatusReport>> GetStuckSagasAsync(TimeSpan stuckThreshold, CancellationToken cancellationToken)
	{
		var all = await _stateStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var now = DateTimeOffset.UtcNow;

		var stuck = all
			.Where(s => s.Status == SagaStatus.Running && (now - s.LastUpdatedAt) > stuckThreshold)
			.Select(s => new SagaStatusReport
			{
				SagaId = s.SagaId,
				OrderId = s.OrderId,
				Status = s.Status,
				CompletedSteps = [.. s.CompletedSteps],
				CreatedAt = s.CreatedAt,
				LastUpdatedAt = s.LastUpdatedAt,
				IsStuck = true,
				StuckDuration = now - s.LastUpdatedAt,
			})
			.ToList();

		if (stuck.Count > 0)
		{
			LogStuckSagasDetected(_logger, stuck.Count, stuckThreshold);
		}

		return stuck;
	}

	/// <summary>
	/// Gets all active (running) sagas.
	/// </summary>
	public async Task<List<SagaStatusReport>> GetActiveSagasAsync(CancellationToken cancellationToken)
	{
		var active = await _stateStore.GetByStatusAsync(SagaStatus.Running, cancellationToken)
			.ConfigureAwait(false);

		return [.. active.Select(ToReport)];
	}

	/// <summary>
	/// Gets all failed sagas.
	/// </summary>
	public async Task<List<SagaStatusReport>> GetFailedSagasAsync(CancellationToken cancellationToken)
	{
		var failed = await _stateStore.GetByStatusAsync(SagaStatus.Failed, cancellationToken)
			.ConfigureAwait(false);

		return [.. failed.Select(ToReport)];
	}

	/// <summary>
	/// Gets all sagas currently compensating.
	/// </summary>
	public async Task<List<SagaStatusReport>> GetCompensatingSagasAsync(CancellationToken cancellationToken)
	{
		var compensating = await _stateStore.GetByStatusAsync(SagaStatus.Compensating, cancellationToken)
			.ConfigureAwait(false);

		return [.. compensating.Select(ToReport)];
	}

	/// <summary>
	/// Gets dashboard aggregates for operational visibility.
	/// </summary>
	public async Task<DashboardAggregates> GetDashboardAsync(CancellationToken cancellationToken)
	{
		var all = await _stateStore.GetAllAsync(cancellationToken).ConfigureAwait(false);

		var running = all.Count(s => s.Status == SagaStatus.Running);
		var completed = all.Count(s => s.Status == SagaStatus.Completed);
		var failed = all.Count(s => s.Status == SagaStatus.Failed);
		var compensating = all.Count(s =>
			s.Status == SagaStatus.Compensating ||
			s.Status == SagaStatus.Compensated ||
			s.Status == SagaStatus.PartiallyCompensated);

		var finishedCount = completed + failed;
		var successRate = finishedCount > 0 ? (decimal)completed / finishedCount : 0m;

		return new DashboardAggregates
		{
			TotalSagas = all.Count,
			RunningCount = running,
			CompletedCount = completed,
			FailedCount = failed,
			CompensatingCount = compensating,
			SuccessRate = Math.Round(successRate, 2),
			AsOfUtc = DateTimeOffset.UtcNow,
		};
	}

	/// <summary>
	/// Prints a formatted dashboard to the console.
	/// </summary>
	public async Task PrintDashboardAsync(CancellationToken cancellationToken)
	{
		var dashboard = await GetDashboardAsync(cancellationToken).ConfigureAwait(false);

		Console.WriteLine();
		Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
		Console.WriteLine("║                  SAGA DASHBOARD                           ║");
		Console.WriteLine("╠═══════════════════════════════════════════════════════════╣");
		Console.WriteLine($"║  Total Sagas:      {dashboard.TotalSagas,5}                               ║");
		Console.WriteLine($"║  Running:          {dashboard.RunningCount,5}                               ║");
		Console.WriteLine($"║  Completed:        {dashboard.CompletedCount,5}                               ║");
		Console.WriteLine($"║  Failed:           {dashboard.FailedCount,5}                               ║");
		Console.WriteLine($"║  Compensating:     {dashboard.CompensatingCount,5}                               ║");
		Console.WriteLine($"║  Success Rate:     {dashboard.SuccessRate,5:P0}                               ║");
		Console.WriteLine("╠═══════════════════════════════════════════════════════════╣");
		Console.WriteLine($"║  As of: {dashboard.AsOfUtc:yyyy-MM-dd HH:mm:ss} UTC                      ║");
		Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
		Console.WriteLine();
	}

	private static SagaStatusReport ToReport(OrderSagaData data) => new()
	{
		SagaId = data.SagaId,
		OrderId = data.OrderId,
		Status = data.Status,
		CompletedSteps = [.. data.CompletedSteps],
		CreatedAt = data.CreatedAt,
		LastUpdatedAt = data.LastUpdatedAt,
		FailureReason = data.FailureReason,
	};

	[LoggerMessage(Level = LogLevel.Warning, Message = "Detected {Count} stuck sagas (no progress in {Threshold})")]
	private static partial void LogStuckSagasDetected(ILogger logger, int count, TimeSpan threshold);
}

/// <summary>
/// Status report for a single saga.
/// </summary>
public sealed class SagaStatusReport
{
	/// <summary>
	/// Gets or sets the saga ID.
	/// </summary>
	public string SagaId { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the order ID.
	/// </summary>
	public string OrderId { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the current status.
	/// </summary>
	public SagaStatus Status { get; init; }

	/// <summary>
	/// Gets or sets the list of completed steps.
	/// </summary>
	public List<string> CompletedSteps { get; init; } = new();

	/// <summary>
	/// Gets or sets when the saga was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets or sets when the saga was last updated.
	/// </summary>
	public DateTimeOffset LastUpdatedAt { get; init; }

	/// <summary>
	/// Gets or sets the failure reason if failed.
	/// </summary>
	public string? FailureReason { get; init; }

	/// <summary>
	/// Gets or sets whether this saga is stuck.
	/// </summary>
	public bool IsStuck { get; init; }

	/// <summary>
	/// Gets or sets how long the saga has been stuck.
	/// </summary>
	public TimeSpan? StuckDuration { get; init; }
}

/// <summary>
/// Dashboard aggregates for operational visibility.
/// </summary>
public sealed class DashboardAggregates
{
	/// <summary>
	/// Gets or sets the total number of sagas.
	/// </summary>
	public int TotalSagas { get; init; }

	/// <summary>
	/// Gets or sets the number of running sagas.
	/// </summary>
	public int RunningCount { get; init; }

	/// <summary>
	/// Gets or sets the number of completed sagas.
	/// </summary>
	public int CompletedCount { get; init; }

	/// <summary>
	/// Gets or sets the number of failed sagas.
	/// </summary>
	public int FailedCount { get; init; }

	/// <summary>
	/// Gets or sets the number of compensating/compensated sagas.
	/// </summary>
	public int CompensatingCount { get; init; }

	/// <summary>
	/// Gets or sets the success rate (completed / finished).
	/// </summary>
	public decimal SuccessRate { get; init; }

	/// <summary>
	/// Gets or sets when this data was collected.
	/// </summary>
	public DateTimeOffset AsOfUtc { get; init; }
}
