// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Extension methods for configuring exception mapping on <see cref="IDispatchBuilder"/>.
/// </summary>
public static class ExceptionMappingDispatchBuilderExtensions
{
	/// <summary>
	/// Configures exception mapping for the Excalibur framework.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Configuration action for exception mappings. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This method registers exception-to-problem-details mappings that are used by the
	/// exception mapping middleware to convert exceptions to RFC 7807 Problem Details format.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.ConfigureExceptionMapping(mapping =>
	///     {
	///         mapping.UseApiExceptionMapping();
	///         mapping.Map&lt;DbException&gt;(ex => new MessageProblemDetails { ... });
	///         mapping.MapWhen&lt;HttpRequestException&gt;(
	///             ex => ex.StatusCode == HttpStatusCode.NotFound,
	///             ex => MessageProblemDetails.NotFound("External resource not found"));
	///         mapping.MapDefault(ex => MessageProblemDetails.InternalError("An unexpected error occurred."));
	///     });
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IDispatchBuilder ConfigureExceptionMapping(
		this IDispatchBuilder builder,
		Action<IExceptionMappingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.AddExceptionMapping(configure);

		return builder;
	}
}
