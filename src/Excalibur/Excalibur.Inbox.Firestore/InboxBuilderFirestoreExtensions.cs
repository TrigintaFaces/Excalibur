// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Firestore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Firestore provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderFirestoreExtensions
{
	/// <summary>
	/// Configures the inbox to use Google Cloud Firestore storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure the Firestore inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UseFirestore(
		this IInboxBuilder builder,
		Action<FirestoreInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddFirestoreInboxStore(configure);

		return builder;
	}
}
