// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Configuration options for the MongoDB CDC processor using Change Streams.
/// </summary>
/// <remarks>
/// <para>
/// Connection properties are in <see cref="Connection"/> and change stream properties are in <see cref="ChangeStream"/>.
/// This follows the <c>MongoClientSettings</c> pattern of separating connection from change stream configuration.
/// </para>
/// </remarks>
public sealed class MongoDbCdcOptions
{
	/// <summary>
	/// Gets or sets the database name to watch for changes.
	/// </summary>
	/// <remarks>
	/// If null, watches changes across all databases.
	/// </remarks>
	public string? DatabaseName { get; set; }

	/// <summary>
	/// Gets or sets the collection names to watch for changes.
	/// </summary>
	/// <remarks>
	/// If empty, watches all collections in the database (or all collections in all databases
	/// if <see cref="DatabaseName"/> is also null).
	/// </remarks>
	public string[] CollectionNames { get; set; } = [];

	/// <summary>
	/// Gets or sets the unique identifier for this CDC processor instance.
	/// </summary>
	/// <remarks>
	/// Used for state tracking and distinguishing multiple processors.
	/// </remarks>
	[Required]
	public string ProcessorId { get; set; } = "default";

	/// <summary>
	/// Gets or sets the number of changes to process in a single batch.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the interval between reconnection attempts after a failure.
	/// </summary>
	public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the connection options.
	/// </summary>
	/// <value> The MongoDB connection options. </value>
	public MongoDbConnectionOptions Connection { get; set; } = new();

	/// <summary>
	/// Gets or sets the change stream options.
	/// </summary>
	/// <value> The MongoDB change stream options. </value>
	public MongoDbChangeStreamOptions ChangeStream { get; set; } = new();

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing or invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Connection.ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required.");
		}

		if (string.IsNullOrWhiteSpace(ProcessorId))
		{
			throw new InvalidOperationException("ProcessorId is required.");
		}

		if (BatchSize <= 0)
		{
			throw new InvalidOperationException("BatchSize must be greater than 0.");
		}

		if (ChangeStream.MaxAwaitTime <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("MaxAwaitTime must be positive.");
		}
	}
}
