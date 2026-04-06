// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.MongoDB.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB stores on <see cref="IA3Builder"/>.
/// </summary>
public static class A3BuilderMongoDbExtensions
{
	/// <summary>
	/// Registers MongoDB grant and activity group stores with the specified options.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">Action to configure the MongoDB authorization options.</param>
	/// <returns>The builder for chaining.</returns>
	public static IA3Builder UseMongoDB(this IA3Builder builder, Action<MongoDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<MongoDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbAuthorizationOptions>, MongoDbAuthorizationOptionsValidator>());

		builder.Services.TryAddSingleton<IActivityGroupGrantStore, MongoDbActivityGroupGrantStore>();

		return builder
			.UseGrantStore<MongoDbGrantStore>();
	}
}
