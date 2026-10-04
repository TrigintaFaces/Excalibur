// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.AzureBlob;

/// <summary>Azure object operations for the fenced cold archive migration protocol.</summary>
internal sealed class AzureBlobColdArchiveMigrationStorage : IColdArchiveMigrationStorage
{
	private static readonly JsonTypeInfo<List<StoredEvent>> StrictArchiveTypeInfo = CreateArchiveTypeInfo();
	private readonly BlobContainerClient _container;

	internal AzureBlobColdArchiveMigrationStorage(BlobContainerClient container)
	{
		ArgumentNullException.ThrowIfNull(container);
		if (!string.IsNullOrEmpty(container.Uri.UserInfo))
		{
			throw new ArgumentException("The storage endpoint must not contain user information.", nameof(container));
		}

		_container = container;
		NamespaceId = "azure-blob:" + container.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
	}

	public string NamespaceId { get; }
	public string GetLayoutKey() => "layout-v1.json";
	public string GetLegacyKey(KeyedTenantPartition tenant, string aggregateId) =>
		ValidateKey($"{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment(aggregateId)}.json.gz");
	public string GetTypedKey(KeyedTenantPartition tenant, string aggregateId, string aggregateType) =>
		ValidateKey(ColdStorageKey.StreamPath(tenant, aggregateType, aggregateId) + ".json.gz");
	public string GetReceiptKey(KeyedTenantPartition tenant, string aggregateId) =>
		ValidateKey($"migration-v1/{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment(aggregateId)}.json");

	public async Task<ColdArchiveMigrationObject?> ReadAsync(string key, CancellationToken cancellationToken)
	{
		try
		{
			var response = await _container.GetBlobClient(ValidateKey(key)).DownloadContentAsync(cancellationToken).ConfigureAwait(false);
			return new ColdArchiveMigrationObject(response.Value.Content.ToMemory(), response.Value.Details.ETag.ToString());
		}
		catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == BlobErrorCode.BlobNotFound.ToString())
		{
			return null;
		}
	}

	public async Task<bool> TryCreateAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
	{
		try
		{
			await _container.GetBlobClient(ValidateKey(key)).UploadAsync(BinaryData.FromBytes(content),
				new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } },
				cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (RequestFailedException ex) when (
			(ex.Status == 412 && ex.ErrorCode == BlobErrorCode.ConditionNotMet.ToString())
			|| (ex.Status == 409 && ex.ErrorCode == BlobErrorCode.BlobAlreadyExists.ToString()))
		{
			return false;
		}
	}

	public Task<IReadOnlyList<StoredEvent>> DecodeAsync(ColdArchiveMigrationObject archive, CancellationToken cancellationToken)
	{
		var json = ColdArchiveGzip.Decode(archive.CopyContent(), int.MaxValue, cancellationToken);
		IReadOnlyList<StoredEvent> events = JsonSerializer.Deserialize(json, StrictArchiveTypeInfo)
			?? throw new JsonException("The archive must contain an event array, not null.");
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(events);
	}

	private static JsonTypeInfo<List<StoredEvent>> CreateArchiveTypeInfo()
	{
		var options = EventSerializationDefaults.CreateCanonicalOptions();
		options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
		options.AllowDuplicateProperties = false;
		_ = EventSerializationDefaults.TryApplyTypeInfoResolver(options, AzureBlobColdStoreJsonContext.Default);
		return (JsonTypeInfo<List<StoredEvent>>)options.GetTypeInfo(typeof(List<StoredEvent>));
	}

	private static string ValidateKey(string key)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);
		if (key.Length > 1024)
		{
			throw new ArgumentException("The archive object key exceeds the Azure Blob name limit.", nameof(key));
		}

		return key;
	}
}
