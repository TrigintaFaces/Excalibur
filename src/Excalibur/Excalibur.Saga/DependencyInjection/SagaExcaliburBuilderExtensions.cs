// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Builders;
using Excalibur.Saga;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for saga configuration.
/// </summary>
public static class SagaExcaliburBuilderExtensions
{
	/// <summary>
	/// Configures saga processing for the Excalibur host.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">
	/// Optional action to configure saga options. Pass <see langword="null"/> to use defaults.
	/// </param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IExcaliburBuilder AddSagas(
		this IExcaliburBuilder builder,
		Action<SagaOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddExcaliburSaga(configure);
		}
		else
		{
			_ = builder.Services.AddExcaliburSaga();
		}

		return builder;
	}

	/// <summary>
	/// Configures saga processing for the Excalibur host using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SagaOptions"/>.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IExcaliburBuilder AddSagas(
		this IExcaliburBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<SagaOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = builder.Services.AddExcaliburSaga();

		return builder;
	}
}
