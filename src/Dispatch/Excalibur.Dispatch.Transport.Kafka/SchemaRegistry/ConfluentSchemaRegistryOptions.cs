// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for the Confluent Schema Registry client.
/// </summary>
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
	/// Gets or sets whether to enable SSL verification.
	/// </summary>
	/// <value><see langword="true"/> to verify SSL certificates; otherwise, <see langword="false"/>.</value>
	public bool EnableSslCertificateVerification { get; set; } = true;

	/// <summary>
	/// Gets or sets the SSL CA certificate location.
	/// </summary>
	/// <value>The path to the CA certificate file, or <see langword="null"/> to use system certificates.</value>
	public string? SslCaLocation { get; set; }

	/// <summary>
	/// Gets or sets the SSL key location.
	/// </summary>
	/// <value>The path to the client key file, or <see langword="null"/> if not using mTLS.</value>
	public string? SslKeyLocation { get; set; }

	/// <summary>
	/// Gets or sets the SSL certificate location.
	/// </summary>
	/// <value>The path to the client certificate file, or <see langword="null"/> if not using mTLS.</value>
	public string? SslCertificateLocation { get; set; }

	/// <summary>
	/// Gets or sets the SSL key password.
	/// </summary>
	/// <value>The password for the client key, or <see langword="null"/> if unencrypted.</value>
	public string? SslKeyPassword { get; set; }

	/// <summary>
	/// Gets or sets whether to auto-register schemas on first use.
	/// </summary>
	/// <value><see langword="true"/> to auto-register; otherwise, <see langword="false"/>. Default is true.</value>
	public bool AutoRegisterSchemas { get; set; } = true;

	/// <summary>
	/// Gets or sets the default compatibility mode for new subjects.
	/// </summary>
	/// <value>The compatibility mode. Default is <see cref="CompatibilityMode.Backward"/>.</value>
	public CompatibilityMode DefaultCompatibility { get; set; } = CompatibilityMode.Backward;

	/// <summary>
	/// Gets or sets whether to validate schemas locally before registration.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to validate schema structure before sending to the registry;
	/// otherwise, <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	public bool ValidateBeforeRegister { get; set; } = true;

	/// <summary>
	/// Gets or sets the subject naming strategy.
	/// </summary>
	/// <value>The subject name strategy. Default is <see cref="SubjectNameStrategy.TopicName"/>.</value>
	public SubjectNameStrategy SubjectNameStrategy { get; set; } = SubjectNameStrategy.TopicName;

	/// <summary>
	/// Gets or sets the custom subject name strategy type, if using a custom implementation.
	/// </summary>
	/// <value>The custom strategy type, or <see langword="null"/> to use the enum-based strategy.</value>
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
	public Type? CustomSubjectNameStrategyType { get; set; }

	/// <summary>
	/// Gets or sets whether schema caching is enabled.
	/// </summary>
	/// <value><see langword="true"/> to cache schemas locally; otherwise, <see langword="false"/>. Default is true.</value>
	public bool CacheSchemas { get; set; } = true;

	/// <summary>
	/// Creates the configured subject name strategy instance.
	/// </summary>
	/// <returns>An <see cref="ISubjectNameStrategy"/> implementation.</returns>
	[RequiresUnreferencedCode("CreateSubjectNameStrategy uses Activator.CreateInstance for custom strategy types.")]
	[RequiresDynamicCode("CreateSubjectNameStrategy uses Activator.CreateInstance for custom strategy types.")]
	public ISubjectNameStrategy CreateSubjectNameStrategy()
	{
		if (CustomSubjectNameStrategyType is not null)
		{
			return (ISubjectNameStrategy)Activator.CreateInstance(CustomSubjectNameStrategyType)!;
		}

		return SubjectNameStrategy.ToStrategy();
	}
}
