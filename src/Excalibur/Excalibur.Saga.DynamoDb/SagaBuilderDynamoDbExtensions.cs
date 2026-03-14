// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DynamoDb;
using Excalibur.Saga.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderDynamoDbExtensions
{
	/// <summary>
	/// Configures the saga builder to use AWS DynamoDB for saga state storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure DynamoDB saga options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseDynamoDb(options =&gt;
	///     {
	///         options.Connection.Region = "us-east-1";
	///         options.TableName = "sagas";
	///     });
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder UseDynamoDb(
		this ISagaBuilder builder,
		Action<DynamoDbSagaOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddDynamoDbSagaStore(configure);
		}
		else
		{
			_ = builder.Services.AddDynamoDbSagaStore(_ => { });
		}

		return builder;
	}
}
