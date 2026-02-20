// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Hosting.Configuration.Validators;

namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Options for Excalibur configuration validation.
/// </summary>
public sealed class ExcaliburValidationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether validation is enabled.
	/// </summary>
	/// <value> <see langword="true" /> if validation is enabled; otherwise, <see langword="false" />. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to fail fast on validation errors.
	/// </summary>
	/// <value> <see langword="true" /> to fail fast; otherwise, <see langword="false" />. </value>
	public bool FailFast { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate database configurations.
	/// </summary>
	/// <value> <see langword="true" /> if database configurations should be validated; otherwise, <see langword="false" />. </value>
	public bool ValidateDatabases { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to test database connections.
	/// </summary>
	/// <value> <see langword="true" /> to test database connections; otherwise, <see langword="false" />. </value>
	public bool TestDatabaseConnections { get; set; }

	/// <summary>
	/// Gets the database connections to validate.
	/// </summary>
	/// <value> The database connections to validate. </value>
	public Dictionary<string, DatabaseProvider> DatabaseConnections { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to validate cloud provider configurations.
	/// </summary>
	/// <value> <see langword="true" /> if cloud provider configurations should be validated; otherwise, <see langword="false" />. </value>
	public bool ValidateCloudProviders { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether AWS is used.
	/// </summary>
	/// <value> <see langword="true" /> if AWS is used; otherwise, <see langword="false" />. </value>
	public bool UseAws { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether Azure is used.
	/// </summary>
	/// <value> <see langword="true" /> if Azure is used; otherwise, <see langword="false" />. </value>
	public bool UseAzure { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether Google Cloud is used.
	/// </summary>
	/// <value> <see langword="true" /> if Google Cloud is used; otherwise, <see langword="false" />. </value>
	public bool UseGoogleCloud { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to validate message broker configurations.
	/// </summary>
	/// <value> <see langword="true" /> if message broker configurations should be validated; otherwise, <see langword="false" />. </value>
	public bool ValidateMessageBrokers { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether RabbitMQ is used.
	/// </summary>
	/// <value> <see langword="true" /> if RabbitMQ is used; otherwise, <see langword="false" />. </value>
	public bool UseRabbitMq { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether Kafka is used.
	/// </summary>
	/// <value> <see langword="true" /> if Kafka is used; otherwise, <see langword="false" />. </value>
	public bool UseKafka { get; set; }
}
