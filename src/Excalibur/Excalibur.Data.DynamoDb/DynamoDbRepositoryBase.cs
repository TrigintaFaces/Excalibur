// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Provides a base implementation for interacting with DynamoDB for a specific document type.
/// </summary>
/// <typeparam name="TDocument">The type of the document to manage in DynamoDB.</typeparam>
/// <remarks>
/// <para>
/// This class includes operations for adding, updating, retrieving, deleting, and scanning
/// documents, as well as initializing tables in DynamoDB.
/// </para>
/// <para>
/// Documents are stored as flat DynamoDB attributes. The <see cref="Document"/> model
/// handles JSON ↔ AttributeValue conversion, and metadata attributes are stripped
/// before deserialization so they don't interfere with the consumer document type.
/// </para>
/// <para>
/// Use this base class to build custom query repositories that share DynamoDB tables
/// with <c>IProjectionStore&lt;T&gt;</c> or other stores.
/// </para>
/// </remarks>
public abstract class DynamoDbRepositoryBase<TDocument> : IDynamoDbRepositoryBase<TDocument>, IDynamoDbRepositoryBaseQuery<TDocument>
	where TDocument : class
{
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbRepositoryBase{TDocument}"/> class.
	/// </summary>
	/// <param name="client">The DynamoDB client instance.</param>
	/// <param name="tableName">The name of the table to operate on.</param>
	/// <param name="partitionKeyName">The name of the partition key attribute.</param>
	protected DynamoDbRepositoryBase(IAmazonDynamoDB client, string tableName, string partitionKeyName)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKeyName);

		Client = client;
		TableName = tableName;
		PartitionKeyName = partitionKeyName;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <summary>
	/// Gets the underlying DynamoDB client.
	/// </summary>
	protected IAmazonDynamoDB Client { get; }

	/// <summary>
	/// Gets the name of the table this repository operates on.
	/// </summary>
	protected string TableName { get; }

	/// <summary>
	/// Gets the name of the partition key attribute.
	/// </summary>
	protected string PartitionKeyName { get; }

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON serialization is used with known types at runtime")]
	public virtual async Task<TDocument?> GetByIdAsync(string documentId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var response = await Client.GetItemAsync(new GetItemRequest
		{
			TableName = TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[PartitionKeyName] = new() { S = documentId },
			},
		}, cancellationToken).ConfigureAwait(false);

		if (response.HttpStatusCode != HttpStatusCode.OK || !response.IsItemSet)
		{
			return null;
		}

		var doc = Document.FromAttributeMap(response.Item);
		doc.Remove(PartitionKeyName);

		// NOTE: Unlike DynamoDbProjectionStore.DeserializeItem, this base class does
		// NOT strip the '_projection' metadata key. The base class is a general-purpose
		// repository that doesn't know about projection metadata. STJ's default behavior
		// silently ignores unknown properties, so any '_projection' map left in the
		// document is harmless during deserialization.
		return JsonSerializer.Deserialize<TDocument>(doc.ToJson(), _jsonOptions);
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON serialization is used with known types at runtime")]
	public virtual async Task<bool> AddOrUpdateAsync(string documentId, TDocument document, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(document);

		var json = JsonSerializer.Serialize(document, _jsonOptions);
		var doc = Document.FromJson(json);
		doc[PartitionKeyName] = documentId;

		var item = doc.ToAttributeMap();

		var response = await Client.PutItemAsync(new PutItemRequest
		{
			TableName = TableName,
			Item = item,
		}, cancellationToken).ConfigureAwait(false);

		return response.HttpStatusCode == HttpStatusCode.OK;
	}

	/// <inheritdoc />
	public virtual async Task<bool> RemoveAsync(string documentId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var response = await Client.DeleteItemAsync(new DeleteItemRequest
		{
			TableName = TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[PartitionKeyName] = new() { S = documentId },
			},
		}, cancellationToken).ConfigureAwait(false);

		return response.HttpStatusCode == HttpStatusCode.OK;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON serialization is used with known types at runtime")]
	public virtual async Task<IReadOnlyList<TDocument>> ScanAsync(
		ScanRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		request.TableName = TableName;

		var response = await Client.ScanAsync(request, cancellationToken).ConfigureAwait(false);

		var results = new List<TDocument>(response.Items.Count);
		foreach (var item in response.Items)
		{
			var doc = Document.FromAttributeMap(item);
			doc.Remove(PartitionKeyName);

			var deserialized = JsonSerializer.Deserialize<TDocument>(doc.ToJson(), _jsonOptions);
			if (deserialized is not null)
			{
				results.Add(deserialized);
			}
		}

		return results;
	}

	/// <summary>
	/// Initializes the DynamoDB table (creates if needed, etc.).
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public abstract Task InitializeTableAsync(CancellationToken cancellationToken);
}
