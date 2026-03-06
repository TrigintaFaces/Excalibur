// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.CosmosDb.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Cosmos DB stores on <see cref="IA3Builder"/>.
/// </summary>
public static class A3BuilderCosmosDbExtensions
{
	/// <summary>
	/// Registers Cosmos DB grant and activity group stores with the specified options.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">Action to configure the Cosmos DB authorization options.</param>
	/// <returns>The builder for chaining.</returns>
	public static IA3Builder UseCosmosDb(this IA3Builder builder, Action<CosmosDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<CosmosDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.TryAddSingleton<IActivityGroupGrantStore, CosmosDbActivityGroupGrantStore>();

		return builder
			.UseGrantStore<CosmosDbGrantStore>();
	}
}
