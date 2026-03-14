// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;
using Excalibur.Outbox.DynamoDb;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DynamoDB outbox stores on <see cref="IOutboxBuilder"/>.
/// </summary>
public static class OutboxBuilderDynamoDbExtensions
{
	/// <summary>
	/// Configures the outbox builder to use AWS DynamoDB for outbox storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Optional action to configure DynamoDB outbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =&gt;
	/// {
	///     outbox.UseDynamoDb(options =&gt;
	///     {
	///         options.Connection.Region = "us-east-1";
	///         options.TableName = "outbox";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseDynamoDb(
		this IOutboxBuilder builder,
		Action<DynamoDbOutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddDynamoDbOutboxStore(configure);
		}
		else
		{
			_ = builder.Services.AddDynamoDbOutboxStore(_ => { });
		}

		return builder;
	}
}
