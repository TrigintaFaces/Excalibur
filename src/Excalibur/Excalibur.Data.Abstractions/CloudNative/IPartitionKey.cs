// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Represents a partition key for cloud-native document databases.
/// </summary>
/// <remarks>
/// <para>
/// Cloud-native document databases (Cosmos DB, DynamoDB, Firestore) require partition keys
/// for data distribution and query optimization. This interface provides a provider-agnostic
/// abstraction for partition key strategies.
/// </para>
/// <para>
/// <strong>Common Partition Strategies:</strong>
/// <list type="bullet">
/// <item><description>Tenant-based: <c>/tenantId</c> for multi-tenant isolation</description></item>
/// <item><description>Aggregate-based: <c>/aggregateId</c> for event sourcing</description></item>
/// <item><description>Hierarchical: <c>/tenantId/aggregateType</c> for complex scenarios</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IPartitionKey
{
	/// <summary>
	/// Gets the partition key value.
	/// </summary>
	/// <value>The partition key value as a string.</value>
	string Value { get; }

	/// <summary>
	/// Gets the partition key path (e.g., "/tenantId" for Cosmos DB).
	/// </summary>
	/// <value>The path expression for the partition key field.</value>
	string Path { get; }
}

/// <summary>
/// Represents a simple string-based partition key.
/// </summary>
/// <param name="Value">The partition key value.</param>
/// <param name="Path">The partition key path (default: "/id").</param>
public sealed record PartitionKey(string Value, string Path = "/id") : IPartitionKey;

/// <summary>
/// Represents a composite partition key with multiple components.
/// </summary>
/// <remarks>
/// Used for hierarchical partitioning strategies such as tenant + aggregate type.
/// </remarks>
public sealed class CompositePartitionKey : IPartitionKey
{
	private readonly string[] _components;
	private readonly string _separator;

	/// <summary>
	/// Initializes a new instance of the <see cref="CompositePartitionKey"/> class.
	/// </summary>
	/// <param name="path">The partition key path.</param>
	/// <param name="separator">The separator between components (default: "#").</param>
	/// <param name="components">The partition key components.</param>
	public CompositePartitionKey(string path, string separator, params string[] components)
	{
		Path = path;
		_separator = separator;
		_components = components;
	}

	/// <inheritdoc/>
	public string Value => string.Join(_separator, _components);

	/// <inheritdoc/>
	public string Path { get; }

	/// <summary>
	/// Gets the individual components of the composite key.
	/// </summary>
	public IReadOnlyList<string> Components => _components;
}
