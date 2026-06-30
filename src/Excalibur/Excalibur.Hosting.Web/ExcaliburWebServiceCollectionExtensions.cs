// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Web.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Excalibur web services in the application's dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddExcaliburWebServices(...)</c> was deleted in per §2.
/// The pre-unification aggregator bundled API versioning with Excalibur core wiring at
/// the composition root — consumers now configure API versioning explicitly, and wire
/// Excalibur via <c>services.AddExcalibur(x => x.ScanAssemblies(...))</c>.
/// </para>
/// </remarks>
public static class ExcaliburWebServiceCollectionExtensions
{
	/// <summary>
	/// Adds global exception handling services with customizable problem details configuration.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configureOptions"> Optional configuration action for customizing problem details options. </param>
	/// <returns> The configured service collection for method chaining. </returns>
	public static IServiceCollection AddGlobalExceptionHandler(
		this IServiceCollection services,
		Action<ProblemDetailsOptions>? configureOptions = null)
	{
		_ = services.Configure(configureOptions ?? (static _ => { }));
		_ = services.AddProblemDetails();
		_ = services.AddExceptionHandler<GlobalExceptionHandler>();
		return services;
	}
}
