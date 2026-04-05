// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Defines argument configuration methods for a RabbitMQ queue builder.
/// </summary>
public interface IRabbitMQQueueArgumentsBuilder
{
	/// <summary>
	/// Sets additional arguments for queue declaration.
	/// </summary>
	/// <param name="arguments">The arguments dictionary.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
	IRabbitMQQueueBuilder Arguments(IDictionary<string, object> arguments);

	/// <summary>
	/// Adds a single argument for queue declaration.
	/// </summary>
	/// <param name="key">The argument key.</param>
	/// <param name="value">The argument value.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
	IRabbitMQQueueBuilder WithArgument(string key, object value);
}
