// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Builders;
using Excalibur.Outbox;

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for outbox configuration.
/// </summary>
public static class OutboxExcaliburBuilderExtensions
{
	/// <summary>
	/// Configures the outbox subsystem for the Excalibur host.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">The outbox configuration action.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static IExcaliburBuilder AddOutbox(
		this IExcaliburBuilder builder,
		Action<IOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddExcaliburOutbox(configure);
		return builder;
	}
}
