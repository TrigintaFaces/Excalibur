// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for the Confluent Schema Registry client.
/// </summary>
/// <remarks>
/// <para>
/// SSL/TLS settings are in <see cref="SchemaRegistrySslOptions"/> via the <see cref="Ssl"/> property.
/// Schema management settings are in <see cref="SchemaRegistrySchemaOptions"/> via the <see cref="Schema"/> property.
/// </para>
/// </remarks>
public sealed class ConfluentSchemaRegistryOptions
{
	/// <summary>
	/// Gets or sets the Schema Registry URL(s).
	/// </summary>
	/// <value>The Schema Registry URL, e.g., "http://localhost:8081".</value>
	public string Url { get; set; } = "http://localhost:8081";

	/// <summary>
	/// Gets or sets the basic authentication username.
	/// </summary>
	/// <value>The username for basic authentication, or <see langword="null"/> if not using auth.</value>
	public string? BasicAuthUserInfo { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of schemas to cache locally.
	/// </summary>
	/// <value>The maximum cached schemas. Default is 1000.</value>
	public int MaxCachedSchemas { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the request timeout.
	/// </summary>
	/// <value>The timeout for schema registry requests. Default is 30 seconds.</value>
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets whether schema caching is enabled.
	/// </summary>
	/// <value><see langword="true"/> to cache schemas locally; otherwise, <see langword="false"/>. Default is true.</value>
	public bool CacheSchemas { get; set; } = true;

	/// <summary>
	/// Gets or sets the SSL/TLS configuration options.
	/// </summary>
	/// <value>The SSL options. Never <see langword="null"/>.</value>
	public SchemaRegistrySslOptions Ssl { get; set; } = new();

	/// <summary>
	/// Gets or sets the schema management configuration options.
	/// </summary>
	/// <value>The schema options. Never <see langword="null"/>.</value>
	public SchemaRegistrySchemaOptions Schema { get; set; } = new();

	/// <summary>
	/// Creates the configured subject name strategy instance.
	/// </summary>
	/// <returns>An <see cref="ISubjectNameStrategy"/> implementation.</returns>
	[RequiresUnreferencedCode("CreateSubjectNameStrategy uses reflection to construct custom strategy types.")]
	[RequiresDynamicCode("CreateSubjectNameStrategy uses reflection to construct custom strategy types.")]
	public ISubjectNameStrategy CreateSubjectNameStrategy()
	{
		if (Schema.CustomSubjectNameStrategyType is not null)
		{
			if (!typeof(ISubjectNameStrategy).IsAssignableFrom(Schema.CustomSubjectNameStrategyType))
			{
				throw new InvalidOperationException(
					$"Custom subject name strategy type '{Schema.CustomSubjectNameStrategyType.FullName}' must implement {nameof(ISubjectNameStrategy)}.");
			}

			var constructor = Schema.CustomSubjectNameStrategyType.GetConstructor(Type.EmptyTypes)
				?? throw new InvalidOperationException(
					$"Custom subject name strategy type '{Schema.CustomSubjectNameStrategyType.FullName}' must have a public parameterless constructor.");

			return (ISubjectNameStrategy)constructor.Invoke([]);
		}

		return Schema.SubjectNameStrategy.ToStrategy();
	}
}
