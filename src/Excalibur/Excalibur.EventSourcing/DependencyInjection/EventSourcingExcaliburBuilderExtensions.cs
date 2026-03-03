// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.Hosting.Builders;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for event sourcing.
/// </summary>
public static class EventSourcingExcaliburBuilderExtensions
{
	/// <summary>
	/// Configures event sourcing for the Excalibur host.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">The event sourcing configuration action.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IExcaliburBuilder AddEventSourcing(
		this IExcaliburBuilder builder,
		Action<IEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddExcaliburEventSourcing(configure);
		return builder;
	}
}
