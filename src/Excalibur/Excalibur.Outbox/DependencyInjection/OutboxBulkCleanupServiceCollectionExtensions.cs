// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering outbox bulk cleanup services.
/// </summary>
public static class OutboxBulkCleanupServiceCollectionExtensions
{
	/// <summary>
	/// Adds outbox bulk cleanup capabilities that delegate to <see cref="IOutboxStoreAdmin"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOutboxBulkCleanup(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IOutboxBulkCleanup, OutboxBulkCleanupAdapter>();

		return services;
	}
}
