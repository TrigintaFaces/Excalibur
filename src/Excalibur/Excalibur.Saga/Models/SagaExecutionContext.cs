// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Models;

/// <summary>
/// Provides context for saga step execution.
/// </summary>
/// <typeparam name="TData"> The type of saga data. </typeparam>
/// <param name="sagaId">The unique identifier for the saga instance.</param>
/// <param name="correlationId">The correlation identifier used to track related operations across the saga.</param>
/// <param name="data">The saga data containing the current state and business context.</param>
/// <param name="services">The service provider for dependency injection within saga steps.</param>
/// <param name="currentStepIndex">The zero-based index of the current step in the saga execution.</param>
/// <param name="isCompensating">Indicates whether the saga is currently executing compensation logic (rollback).</param>
/// <remarks>
/// Initializes a new instance of the <see cref="SagaExecutionContext{TData}" /> class.
/// </remarks>
public sealed class SagaExecutionContext<TData>(
	string sagaId,
	string correlationId,
	TData data,
	IServiceProvider services,
	int currentStepIndex,
	bool isCompensating = false) : ISagaContext<TData>
	where TData : class
{
	private readonly List<SagaActivity> _activities = [];

	/// <summary>
	/// Gets the saga identifier.
	/// </summary>
	/// <value>the saga identifier.</value>
	public string SagaId { get; } = sagaId;

	/// <summary>
	/// Gets the correlation ID.
	/// </summary>
	/// <value>the correlation ID.</value>
	public string CorrelationId { get; } = correlationId;

	/// <summary>
	/// Gets or sets the saga data.
	/// </summary>
	/// <value>the saga data.</value>
	public TData Data { get; set; } = data;

	/// <summary>
	/// Gets the service provider for dependency injection.
	/// </summary>
	/// <value>the service provider for dependency injection.</value>
	public IServiceProvider Services { get; } = services;

	/// <summary>
	/// Gets the shared context data between steps.
	/// </summary>
	/// <value>the shared context data between steps.</value>
	public IDictionary<string, object> SharedContext { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the current step index.
	/// </summary>
	/// <value>the current step index.</value>
	public int CurrentStepIndex { get; } = currentStepIndex;

	/// <summary>
	/// Gets a value indicating whether this is a compensation execution.
	/// </summary>
	/// <value><see langword="true"/> if whether this is a compensation execution.; otherwise, <see langword="false"/>.</value>
	public bool IsCompensating { get; } = isCompensating;

	/// <summary>
	/// Gets metadata associated with the saga (aliases SharedContext for ISagaContext compatibility).
	/// </summary>
	/// <value>A metadata dictionary scoped to the saga.</value>
	public IDictionary<string, object> Metadata => SharedContext;

	/// <summary>
	/// Gets the activity log.
	/// </summary>
	/// <value>The read-only activity log.</value>
	public IReadOnlyList<SagaActivity> Activities => _activities;

	/// <summary>
	/// Adds an activity log entry.
	/// </summary>
	/// <param name="message">The log message.</param>
	/// <param name="details">Additional details.</param>
	public void AddActivity(string message, object? details = null)
	{
		_activities.Add(new SagaActivity
		{
			Timestamp = DateTimeOffset.UtcNow,
			Message = message,
			Details = details
		});
	}

	/// <summary>
	/// Gets a service from the service provider.
	/// </summary>
	/// <typeparam name="TService"> The service type. </typeparam>
	/// <returns> The service instance. </returns>
	public TService GetRequiredService<TService>()
		where TService : notnull
		=> Services.GetRequiredService<TService>();
}

