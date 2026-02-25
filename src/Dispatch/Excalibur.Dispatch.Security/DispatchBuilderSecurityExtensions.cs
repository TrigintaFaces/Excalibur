// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring security via <see cref="IDispatchBuilder"/>.
/// </summary>
public static class DispatchBuilderSecurityExtensions
{
	/// <summary>
	/// Adds Dispatch security services (encryption, signing, rate limiting, validation) via the builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configuration">The configuration instance for security settings.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configuration"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddSecurity(configuration);
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Security service registration uses reflection for dependency injection and configuration binding")]
	[RequiresDynamicCode("Security service registration uses reflection to scan and register middleware and validators")]
	public static IDispatchBuilder AddSecurity(
		this IDispatchBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddDispatchSecurity(configuration);
		return builder;
	}
}
