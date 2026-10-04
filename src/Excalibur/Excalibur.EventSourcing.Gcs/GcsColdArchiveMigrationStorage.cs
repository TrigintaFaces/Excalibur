// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Google;
using Google.Cloud.Storage.V1;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

using StorageObject = Google.Apis.Storage.v1.Data.Object;

namespace Excalibur.EventSourcing.Gcs;

/// <summary>Generation-pinned GCS operations for externally fenced, retained archive migration.</summary>
internal sealed class GcsColdArchiveMigrationStorage : IColdArchiveMigrationStorage
{
	private static readonly UTF8Encoding StrictUtf8 = new(false, true);
	private static readonly JsonTypeInfo<List<StoredEvent>> ArchiveTypeInfo = CreateArchiveTypeInfo();
	private readonly StorageClient _client;
	private readonly string _bucket;
	private readonly string _prefix;

	// Own and configure this raw client separately; never change a borrowed client's HTTP pipeline.
	internal GcsColdArchiveMigrationStorage(StorageClient client, string bucket, string prefix)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrEmpty(bucket);
		ArgumentNullException.ThrowIfNull(prefix);
		var service = client.Service;
		if (service.GZipEnabled || !service.HttpClient.DefaultRequestHeaders.AcceptEncoding
			.Any(static encoding => encoding.Value == "gzip" && (encoding.Quality is null || encoding.Quality > 0)))
		{
			throw new ArgumentException("Migration requires a dedicated client without automatic decompression that accepts gzip bytes.", nameof(client));
		}

		var endpoint = new Uri(service.BaseUri);
		if (!string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
		{
			throw new ArgumentException("The storage endpoint must not contain credentials, query, or fragment.", nameof(client));
		}

		_client = client;
		_bucket = bucket;
		_prefix = prefix;
		NamespaceId = $"gcs:{Encode(endpoint.AbsoluteUri)}/{Encode(bucket)}/{Encode(prefix)}";
	}

	public string NamespaceId { get; }
	public string GetLayoutKey() => Key("layout-v1.json");
	public string GetLegacyKey(KeyedTenantPartition tenant, string aggregateId) =>
		Key($"{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment(aggregateId)}/events.json.gz");
	public string GetTypedKey(KeyedTenantPartition tenant, string aggregateId, string aggregateType) =>
		Key(ColdStorageKey.StreamPath(tenant, aggregateType, aggregateId) + "/events.json.gz");
	public string GetReceiptKey(KeyedTenantPartition tenant, string aggregateId) =>
		Key($"migration-v1/{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment(aggregateId)}.json");

	public async Task<ColdArchiveMigrationObject?> ReadAsync(string key, CancellationToken cancellationToken)
	{
		ValidateKey(key);
		StorageObject metadata;
		try
		{
			metadata = await _client.GetObjectAsync(_bucket, key, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			// The migration contract excludes bucket deletion/recreation. Under that premise a successful
			// bucket check distinguishes an absent object from an unavailable namespace.
			var bucket = await _client.GetBucketAsync(_bucket, cancellationToken: cancellationToken).ConfigureAwait(false);
			if (bucket.Name != _bucket)
			{
				throw new InvalidDataException("The storage response does not identify the requested bucket.");
			}

			return null;
		}

		ValidateIdentity(metadata, key);
		using var content = new MemoryStream();
		var observed = await _client.DownloadObjectAsync(_bucket, key, content,
			new DownloadObjectOptions
			{
				Generation = metadata.Generation, IfGenerationMatch = metadata.Generation,
				DownloadValidationMode = DownloadValidationMode.Always,
			}, cancellationToken).ConfigureAwait(false);
		ValidateIdentity(observed, key);
		if (observed.Generation != metadata.Generation)
		{
			throw new InvalidDataException("The downloaded archive generation differs from its metadata.");
		}

		var bytes = content.ToArray();
		GcsArchiveIntegrity.Validate(bytes, metadata.Size, metadata.Crc32c, cancellationToken);
		return new ColdArchiveMigrationObject(bytes, metadata.Generation!.Value.ToString(CultureInfo.InvariantCulture));
	}

	public async Task<bool> TryCreateAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
	{
		ValidateKey(key);
		var bytes = content.ToArray();
		using var stream = new MemoryStream(bytes, writable: false);
		StorageObject created;
		try
		{
			created = await _client.UploadObjectAsync(_bucket, key, "application/octet-stream", stream,
				new UploadObjectOptions { IfGenerationMatch = 0 }, cancellationToken).ConfigureAwait(false);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
		{
			return false;
		}

		ValidateIdentity(created, key);
		GcsArchiveIntegrity.Validate(bytes, created.Size, created.Crc32c, cancellationToken);
		return true;
	}

	public Task<IReadOnlyList<StoredEvent>> DecodeAsync(ColdArchiveMigrationObject archive, CancellationToken cancellationToken)
	{
		var json = ColdArchiveGzip.Decode(archive.CopyContent(), int.MaxValue, cancellationToken);
		IReadOnlyList<StoredEvent> events = JsonSerializer.Deserialize(json, ArchiveTypeInfo)
			?? throw new JsonException("The archive must contain an event array, not null.");
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(events);
	}

	private void ValidateIdentity(StorageObject metadata, string key)
	{
		if (metadata.Bucket != _bucket || metadata.Name != key || metadata.Generation is null or <= 0)
		{
			throw new InvalidDataException("The archive metadata must identify the requested object and a valid generation.");
		}
	}

	private static JsonTypeInfo<List<StoredEvent>> CreateArchiveTypeInfo()
	{
		var options = EventSerializationDefaults.CreateCanonicalOptions();
		options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
		options.AllowDuplicateProperties = false;
		_ = EventSerializationDefaults.TryApplyTypeInfoResolver(options, GcsColdStoreJsonContext.Default);
		return (JsonTypeInfo<List<StoredEvent>>)options.GetTypeInfo(typeof(List<StoredEvent>));
	}

	private static string Encode(string value) => Base64Url.EncodeToString(StrictUtf8.GetBytes(value));
	private string Key(string suffix) => ValidateKey(string.IsNullOrEmpty(_prefix) ? suffix : $"{_prefix}/{suffix}");
	private static string ValidateKey(string key)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);
		if (StrictUtf8.GetByteCount(key) > 1024)
		{
			throw new ArgumentException("The archive object name exceeds the GCS UTF-8 byte limit.", nameof(key));
		}

		return key;
	}
}
