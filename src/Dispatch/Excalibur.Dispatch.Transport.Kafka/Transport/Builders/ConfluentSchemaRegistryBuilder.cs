// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Internal implementation of the Confluent Schema Registry builder.
/// </summary>
internal sealed class ConfluentSchemaRegistryBuilder : IConfluentSchemaRegistryBuilder
{
	private readonly ConfluentSchemaRegistryOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentSchemaRegistryBuilder"/> class.
	/// </summary>
	/// <param name="options">The options to configure.</param>
	public ConfluentSchemaRegistryBuilder(ConfluentSchemaRegistryOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder SchemaRegistryUrl(string url)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(url);
		_options.Url = url;
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder SchemaRegistryUrls(params string[] urls)
	{
		ArgumentNullException.ThrowIfNull(urls);

		if (urls.Length == 0)
		{
			throw new ArgumentException("At least one URL must be specified.", nameof(urls));
		}

		foreach (var url in urls)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				throw new ArgumentException("URLs cannot contain null or whitespace values.", nameof(urls));
			}
		}

		// Store as comma-separated for compatibility with underlying client
		_options.Url = string.Join(",", urls);
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder BasicAuth(string username, string password)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(username);
		ArgumentException.ThrowIfNullOrWhiteSpace(password);

		// Confluent format: "username:password"
		_options.BasicAuthUserInfo = $"{username}:{password}";
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder ConfigureSsl(Action<ISchemaRegistrySslBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var sslBuilder = new SchemaRegistrySslBuilder(_options);
		configure(sslBuilder);
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder SubjectNameStrategy(SubjectNameStrategy strategy)
	{
		_options.SubjectNameStrategy = strategy;
		_options.CustomSubjectNameStrategyType = null; // Clear any custom type
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder SubjectNameStrategy<TStrategy>()
		where TStrategy : class, ISubjectNameStrategy, new()
	{
		_options.CustomSubjectNameStrategyType = typeof(TStrategy);
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder CompatibilityMode(CompatibilityMode mode)
	{
		_options.DefaultCompatibility = mode;
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder AutoRegisterSchemas(bool enable = true)
	{
		_options.AutoRegisterSchemas = enable;
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder ValidateBeforeRegister(bool enable = true)
	{
		_options.ValidateBeforeRegister = enable;
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder CacheSchemas(bool enable = true)
	{
		_options.CacheSchemas = enable;
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder CacheCapacity(int capacity)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
		_options.MaxCachedSchemas = capacity;
		return this;
	}

	/// <inheritdoc/>
	public IConfluentSchemaRegistryBuilder RequestTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
		}

		_options.RequestTimeout = timeout;
		return this;
	}
}
