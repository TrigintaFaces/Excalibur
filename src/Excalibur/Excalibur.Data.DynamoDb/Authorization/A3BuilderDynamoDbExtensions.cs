// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.DynamoDb.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB stores on <see cref="IA3Builder"/>.
/// </summary>
public static class A3BuilderDynamoDbExtensions
{
	/// <summary>
	/// Registers DynamoDB grant and activity group stores with the specified options.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">Action to configure the DynamoDB authorization options.</param>
	/// <returns>The builder for chaining.</returns>
	public static IA3Builder UseDynamoDb(this IA3Builder builder, Action<DynamoDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<DynamoDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DynamoDbAuthorizationOptions>, DynamoDbAuthorizationOptionsValidator>());

		builder.Services.TryAddSingleton<IActivityGroupGrantStore, DynamoDbActivityGroupGrantStore>();

		return builder
			.UseGrantStore<DynamoDbGrantStore>();
	}
}
