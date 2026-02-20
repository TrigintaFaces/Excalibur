// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Extension methods for <see cref="IDispatchBuilder" /> to configure dispatch authorization.
/// </summary>
public static class DispatchBuilderExtensions
{
	/// <summary>
	/// Adds dispatch authorization services to the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder to configure.</param>
	/// <returns>The dispatch builder for chaining.</returns>
	public static IDispatchBuilder AddDispatchAuthorization(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchAuthorization();
		return builder;
	}
}
