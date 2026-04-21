// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Hosting.Builders;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for data-processing orchestration.
/// </summary>
public static class DataProcessingExcaliburBuilderExtensions
{
	/// <summary>
	/// Configures the data-processing subsystem within the Excalibur composition root.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">Fluent configuration for processors, record handlers, and connection factories.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(excalibur => excalibur
	///     .AddDispatch(...)
	///     .AddDataProcessing(dp => dp
	///         .ConnectionFactory(...)
	///         .AddProcessor&lt;OrderProcessor&gt;()
	///         .EnableBackgroundProcessing()));
	/// </code>
	/// </example>
	public static IExcaliburBuilder AddDataProcessing(
		this IExcaliburBuilder builder,
		Action<IDataProcessingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddDataProcessing(configure);
		return builder;
	}
}
