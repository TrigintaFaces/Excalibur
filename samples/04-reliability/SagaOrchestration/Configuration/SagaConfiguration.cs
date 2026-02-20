// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

using SagaOrchestration.Monitoring;
using SagaOrchestration.Sagas;
using SagaOrchestration.Steps;

namespace SagaOrchestration.Configuration;

/// <summary>
/// Configuration extensions for saga orchestration services.
/// </summary>
public static class SagaConfiguration
{
	/// <summary>
	/// Adds saga orchestration services to the DI container.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional configuration callback.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaOrchestration(
		this IServiceCollection services,
		Action<SagaOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var options = new SagaOptions();
		configureOptions?.Invoke(options);

		// Register stores (singleton for in-memory demo)
		_ = services.AddSingleton<ISagaStateStore, InMemorySagaStateStore>();
		_ = services.AddSingleton<ITimeoutStore, InMemoryTimeoutStore>();

		// Register saga steps in order of execution
		_ = services.AddScoped<ISagaStep, ReserveInventoryStep>();
		_ = services.AddScoped<ISagaStep, ProcessPaymentStep>();
		_ = services.AddScoped<ISagaStep, ShipOrderStep>();

		// Register saga orchestrator
		_ = services.AddScoped<OrderFulfillmentSaga>();

		// Register monitoring services
		_ = services.AddScoped<SagaDashboardService>();

		return services;
	}

	/// <summary>
	/// Adds saga orchestration with a failing payment step (for compensation demo).
	/// </summary>
	public static IServiceCollection AddSagaOrchestrationWithFailingPayment(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register stores (singleton for in-memory demo)
		_ = services.AddSingleton<ISagaStateStore, InMemorySagaStateStore>();
		_ = services.AddSingleton<ITimeoutStore, InMemoryTimeoutStore>();

		// Register saga steps with a failing payment step
		_ = services.AddScoped<ISagaStep, ReserveInventoryStep>();
		_ = services.AddScoped<ISagaStep>(sp =>
		{
			var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ProcessPaymentStep>>();
			return new ProcessPaymentStep(logger, shouldFail: true);
		});
		_ = services.AddScoped<ISagaStep, ShipOrderStep>();

		// Register saga orchestrator
		_ = services.AddScoped<OrderFulfillmentSaga>();

		// Register monitoring services
		_ = services.AddScoped<SagaDashboardService>();

		return services;
	}
}

/// <summary>
/// Options for saga orchestration configuration.
/// </summary>
public sealed class SagaOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts for compensation.
	/// </summary>
	public int MaxCompensationRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the default timeout for inventory reservation.
	/// </summary>
	public TimeSpan InventoryReservationTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the default timeout for payment confirmation.
	/// </summary>
	public TimeSpan PaymentConfirmationTimeout { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Gets or sets the threshold for detecting stuck sagas.
	/// </summary>
	public TimeSpan StuckSagaThreshold { get; set; } = TimeSpan.FromMinutes(30);
}
