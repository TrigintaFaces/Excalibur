// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Excalibur.Data.DynamoDb.Projections;

/// <summary>
/// Configuration options for the DynamoDB projection store.
/// </summary>
public sealed class DynamoDbProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the DynamoDB table name for projections.
	/// </summary>
	[Required]
	public string TableName { get; set; } = "Projections";

	/// <summary>
	/// Gets or sets the partition key attribute name. Default: "PK".
	/// </summary>
	public string PartitionKeyName { get; set; } = "PK";

	/// <summary>
	/// Gets or sets whether to auto-create the table. Default: true.
	/// </summary>
	public bool AutoCreateTable { get; set; } = true;

	/// <summary>
	/// Gets or sets the JSON serializer options used for projection serialization and deserialization.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <see langword="null"/> (default), the store creates internal options with
	/// <see cref="JsonNamingPolicy.CamelCase"/>. For AOT/trimming scenarios, provide a
	/// <see cref="JsonSerializerOptions"/> instance with a source-generated
	/// <c>JsonSerializerContext</c> as the <see cref="JsonSerializerOptions.TypeInfoResolver"/>
	/// to eliminate reflection-based serialization.
	/// </para>
	/// </remarks>
	/// <value>Defaults to <see langword="null"/> (uses internal camelCase options).</value>
	public JsonSerializerOptions? JsonSerializerOptions { get; set; }
}
