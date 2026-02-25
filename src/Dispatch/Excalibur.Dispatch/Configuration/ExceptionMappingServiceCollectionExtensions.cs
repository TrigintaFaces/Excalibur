// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding exception mapping services to the DI container.
/// </summary>
public static class ExceptionMappingServiceCollectionExtensions
{
	/// <summary>
	/// Adds exception mapping services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional configuration action for exception mappings. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="IExceptionMapper"/> as a singleton service.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddExceptionMapping(mapping =>
	/// {
	///     mapping.UseApiExceptionMapping();
	///     mapping.Map&lt;DbException&gt;(ex => new MessageProblemDetails { ... });
	///     mapping.MapDefault(ex => MessageProblemDetails.InternalError("An unexpected error occurred."));
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddExceptionMapping(
		this IServiceCollection services,
		Action<IExceptionMappingBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var builder = new ExceptionMappingBuilder();
		configure?.Invoke(builder);

		var options = builder.Build();

		services.TryAddSingleton<IExceptionMapper>(_ => new ExceptionMapper(options));

		return services;
	}
}
