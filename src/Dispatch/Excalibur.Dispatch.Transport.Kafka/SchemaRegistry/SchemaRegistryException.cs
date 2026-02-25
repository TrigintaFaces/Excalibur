// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Exception thrown when a schema registry operation fails.
/// </summary>
[Serializable]
public sealed class SchemaRegistryException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaRegistryException"/> class.
	/// </summary>
	public SchemaRegistryException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaRegistryException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public SchemaRegistryException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaRegistryException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public SchemaRegistryException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the schema subject involved in the failed operation.
	/// </summary>
	/// <value>The schema subject, or <see langword="null"/> if not applicable.</value>
	public string? Subject { get; init; }

	/// <summary>
	/// Gets or sets the schema ID involved in the failed operation.
	/// </summary>
	/// <value>The schema ID, or <see langword="null"/> if not applicable.</value>
	public int? SchemaId { get; init; }

	/// <summary>
	/// Gets or sets the HTTP status code returned by the schema registry.
	/// </summary>
	/// <value>The HTTP status code, or <see langword="null"/> if not applicable.</value>
	public int? StatusCode { get; init; }

	/// <summary>
	/// Gets or sets the error code returned by the schema registry.
	/// </summary>
	/// <value>The error code, or <see langword="null"/> if not applicable.</value>
	public int? ErrorCode { get; init; }
}
