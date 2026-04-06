// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Firestore.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Firestore stores on <see cref="IA3Builder"/>.
/// </summary>
public static class A3BuilderFirestoreExtensions
{
	/// <summary>
	/// Registers Firestore grant and activity group stores with the specified options.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">Action to configure the Firestore authorization options.</param>
	/// <returns>The builder for chaining.</returns>
	public static IA3Builder UseFirestore(this IA3Builder builder, Action<FirestoreAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<FirestoreAuthorizationOptions>()
			.Configure(configure)
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<FirestoreAuthorizationOptions>, FirestoreAuthorizationOptionsValidator>());

		builder.Services.TryAddSingleton<IActivityGroupGrantStore, FirestoreActivityGroupGrantStore>();

		return builder
			.UseGrantStore<FirestoreGrantStore>();
	}
}
