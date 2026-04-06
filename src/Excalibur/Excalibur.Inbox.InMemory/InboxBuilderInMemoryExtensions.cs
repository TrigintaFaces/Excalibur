// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.InMemory;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderInMemoryExtensions
{
	/// <summary>
	/// Configures the inbox to use in-memory storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Optional action to configure the in-memory inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// The in-memory store is not suitable for production use.
	/// Use it only for unit tests, integration tests, or local development.
	/// </remarks>
	public static IInboxBuilder UseInMemory(
		this IInboxBuilder builder,
		Action<InMemoryInboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
		_ = builder.Services.AddInMemoryInboxStore(configure ?? (_ => { }));
#pragma warning restore IL2026

		return builder;
	}
}
