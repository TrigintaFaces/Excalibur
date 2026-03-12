// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Connection options for MongoDB CDC processor.
/// </summary>
/// <remarks>
/// Follows the <c>MongoClientSettings</c> pattern of separating connection properties from change stream configuration.
/// </remarks>
public sealed class MongoDbConnectionOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	/// <remarks>
	/// For Change Streams, the MongoDB deployment must be a replica set or sharded cluster.
	/// </remarks>
	[Required]
	public string ConnectionString { get; set; } = "mongodb://localhost:27017";

	/// <summary>
	/// Gets or sets the server selection timeout.
	/// </summary>
	public TimeSpan ServerSelectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the connection timeout.
	/// </summary>
	public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets whether to use SSL/TLS.
	/// </summary>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets or sets the maximum connection pool size.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;
}
