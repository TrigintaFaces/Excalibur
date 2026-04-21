// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Cdc;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods bridging data-change-handler assembly scanning onto
/// <see cref="ICdcBuilder"/> so consumers configure CDC handlers inside a
/// single <c>AddCdcProcessor(cdc =&gt; ...)</c> composition root (ADR-321).
/// </summary>
public static class DataChangeHandlerCdcBuilderExtensions
{
	/// <summary>
	/// Scans the specified assembly for classes implementing <c>IDataChangeHandler</c>
	/// and registers them in the underlying service collection.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="assembly">The assembly to scan.</param>
	/// <param name="lifetime">Service lifetime for registered handlers. Default is <see cref="ServiceLifetime.Singleton"/>.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover types implementing IDataChangeHandler.")]
	public static ICdcBuilder AddDataChangeHandlersFromAssembly(
		this ICdcBuilder builder,
		Assembly assembly,
		ServiceLifetime lifetime = ServiceLifetime.Singleton)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDataChangeHandlersFromAssembly(assembly, lifetime);
		return builder;
	}
}
