// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.DependencyInjection;

/// <summary>
/// Builder interface for configuring Excalibur.Saga services.
/// </summary>
/// <remarks>
/// <para>
/// Provides a unified, fluent API for configuring all saga capabilities:
/// core saga services, advanced orchestration, timeout delivery, and telemetry.
/// </para>
/// <para>
/// This follows the Microsoft builder pattern (like <c>IHttpClientBuilder</c>
/// or <c>IHealthChecksBuilder</c>) where the builder exposes the underlying
/// <see cref="IServiceCollection"/> and provides typed extension methods.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcaliburSaga(saga => saga
///     .WithOrchestration(opts => opts.MaxRetryAttempts = 5)
///     .WithTimeouts(opts => opts.PollInterval = TimeSpan.FromSeconds(30))
///     .WithInstrumentation());
/// </code>
/// </example>
public interface ISagaBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The service collection.</value>
	IServiceCollection Services { get; }
}
