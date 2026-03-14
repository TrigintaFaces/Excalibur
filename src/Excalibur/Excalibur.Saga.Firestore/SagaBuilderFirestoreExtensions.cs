// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Firestore;
using Excalibur.Saga.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Firestore saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderFirestoreExtensions
{
	/// <summary>
	/// Configures the saga builder to use Google Cloud Firestore for saga state storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure Firestore saga options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseFirestore(options =&gt;
	///     {
	///         options.ProjectId = "my-project";
	///         options.CollectionName = "sagas";
	///     });
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder UseFirestore(
		this ISagaBuilder builder,
		Action<FirestoreSagaOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddFirestoreSagaStore(configure);
		}
		else
		{
			_ = builder.Services.AddFirestoreSagaStore(_ => { });
		}

		return builder;
	}
}
