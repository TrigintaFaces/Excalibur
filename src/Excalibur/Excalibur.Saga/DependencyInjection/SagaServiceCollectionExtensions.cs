// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga;
using Excalibur.Saga.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur.Saga services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class SagaServiceCollectionExtensions
{
	/// <summary>
	/// Adds the core Excalibur.Saga services with default options.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburSaga(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// ADR-078: Register Dispatch primitives first (IDispatcher, IMessageBus, etc.)
		_ = services.AddDispatch();

		// AD-252-2: Use AddOptions pattern for proper configuration binding
		// AddOptions<T> ensures IOptions<T>, IOptionsSnapshot<T>, IOptionsMonitor<T> are registered
		// TryAddEnumerable prevents duplicate IConfigureOptions registrations
		_ = services.AddOptions<SagaOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<SagaOptions>, DefaultSagaOptionsSetup>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SagaOptions>, SagaOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds the core Excalibur.Saga services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configure">The action to configure saga options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburSaga(
		this IServiceCollection services,
		Action<SagaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure base services are registered (including Dispatch primitives)
		_ = services.AddExcaliburSaga();

		_ = services.Configure(configure);

		return services;
	}

	/// <summary>
	/// Adds the core Excalibur.Saga services and returns a builder for configuring
	/// sub-features such as orchestration, timeouts, and instrumentation.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configure">Action to configure saga sub-features via the builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the recommended entry point for configuring all saga services.
	/// It replaces multiple separate <c>AddDispatchAdvancedSagas</c>,
	/// <c>AddSagaTimeoutDelivery</c>, and <c>AddSagaInstrumentation</c> calls
	/// with a single, discoverable builder pattern.
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
	public static IServiceCollection AddExcaliburSaga(
		this IServiceCollection services,
		Action<ISagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register core saga services first
		_ = services.AddExcaliburSaga();

		// Apply builder configuration
		var builder = new SagaBuilder(services);
		configure(builder);

		return services;
	}

	/// <summary>
	/// Checks if Excalibur.Saga services have been registered.
	/// </summary>
	/// <param name="services">The service collection to check.</param>
	/// <returns>True if saga services are registered, false otherwise.</returns>
	public static bool HasExcaliburSaga(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// AD-252-2: Check for IConfigureOptions registration (proper options pattern)
		return services.Any(s =>
			s.ServiceType == typeof(IConfigureOptions<SagaOptions>) ||
			s.ImplementationType == typeof(DefaultSagaOptionsSetup));
	}

	/// <summary>
	/// Default options setup for SagaOptions.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Instantiated via DI through TryAddEnumerable ServiceDescriptor")]
	private sealed class DefaultSagaOptionsSetup : IConfigureOptions<SagaOptions>
	{
		public void Configure(SagaOptions options)
		{
			// Defaults are already set in SagaOptions constructor
			// This class exists to enable proper options pattern integration
		}
	}
}
