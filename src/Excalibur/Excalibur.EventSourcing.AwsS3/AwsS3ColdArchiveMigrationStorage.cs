// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Amazon.S3;
using Amazon.S3.Model;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.AwsS3;

/// <summary>S3 object operations for externally fenced, retained cold archive migration.</summary>
internal sealed class AwsS3ColdArchiveMigrationStorage : IColdArchiveMigrationStorage
{
	private static readonly UTF8Encoding StrictUtf8 = new(false, true);
	private static readonly JsonTypeInfo<List<StoredEvent>> StrictArchiveTypeInfo = CreateArchiveTypeInfo();
	private readonly IAmazonS3 _client;
	private readonly string _bucket;
	private readonly string _prefix;
	private readonly Lazy<string> _namespaceId;

	/// <summary>Binds identity to the captured client's resolved bucket endpoint.</summary>
	/// <remarks>
	/// The client must retain a fixed, strongly consistent object namespace. Multi-region access points
	/// and failover routing do not satisfy that premise even when their endpoint URL is stable.
	/// Endpoint/address-style changes produce a different persisted binding and require operator review.
	/// </remarks>
	internal AwsS3ColdArchiveMigrationStorage(IAmazonS3 client, string bucket, string prefix)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrEmpty(bucket);
		ArgumentNullException.ThrowIfNull(prefix);
		_client = client;
		_bucket = bucket;
		_prefix = prefix;
		_namespaceId = new Lazy<string>(() => CreateNamespaceId(ResolveEndpoint(client, bucket, prefix), bucket, prefix));
	}

	private static Uri ResolveEndpoint(IAmazonS3 client, string bucket, string prefix)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrEmpty(bucket);
		ArgumentNullException.ThrowIfNull(prefix);
		// Access-point ARNs require separate routing guarantees; a stable MRAP alias is not one bucket.
		if (bucket.Contains(':', StringComparison.Ordinal) || bucket.EndsWith(".mrap", StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("Cold archive migration requires a direct bucket, not an access-point ARN or multi-region alias.", nameof(bucket));
		}

		var key = string.IsNullOrEmpty(prefix) ? "layout-v1.json" : prefix + "/layout-v1.json";
		try
		{
			var endpoint = client.DetermineServiceOperationEndpoint(new GetObjectRequest { BucketName = bucket, Key = key });
			if (endpoint is null || string.IsNullOrWhiteSpace(endpoint.URL))
			{
				throw new NotSupportedException("The supplied S3 client returned no endpoint identity.");
			}

			return new Uri(endpoint.URL, UriKind.Absolute);
		}
		catch (NotSupportedException ex)
		{
			throw new NotSupportedException("Typed archives and migration require an S3 client that resolves its fixed bucket endpoint. Configure a compatible client; Legacy guards do not require endpoint discovery.", ex);
		}
	}

	// The caller must supply the effective endpoint scope of this exact client, including its AWS
	// partition/region or custom service endpoint. Credentials are never part of persisted identity.
	internal AwsS3ColdArchiveMigrationStorage(IAmazonS3 client, Uri endpoint, string bucket, string prefix)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(endpoint);
		ArgumentException.ThrowIfNullOrEmpty(bucket);
		ArgumentNullException.ThrowIfNull(prefix);
		if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("https" or "http")
			|| !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query)
			|| !string.IsNullOrEmpty(endpoint.Fragment))
		{
			throw new ArgumentException("The endpoint must be an absolute HTTP endpoint without credentials, query, or fragment.", nameof(endpoint));
		}

		_client = client;
		_bucket = bucket;
		_prefix = prefix;
		_namespaceId = new Lazy<string>(() => CreateNamespaceId(endpoint, bucket, prefix));
	}

	private static string CreateNamespaceId(Uri endpoint, string bucket, string prefix)
	{
		if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("https" or "http")
			|| !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query)
			|| !string.IsNullOrEmpty(endpoint.Fragment))
		{
			throw new ArgumentException("The resolved endpoint must be absolute HTTP without credentials, query, or fragment.", nameof(endpoint));
		}

		return $"aws-s3:{Encode(endpoint.AbsoluteUri)}/{Encode(bucket)}/{Encode(prefix)}";
	}

	public string NamespaceId => _namespaceId.Value;
	public string GetLayoutKey() => Key("layout-v1.json");
	public string GetLegacyKey(KeyedTenantPartition tenant, string aggregateId) =>
		Key($"{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment(aggregateId)}/events.json.gz");
	public string GetTypedKey(KeyedTenantPartition tenant, string aggregateId, string aggregateType) =>
		Key(ColdStorageKey.StreamPath(tenant, aggregateType, aggregateId) + "/events.json.gz");
	public string GetReceiptKey(KeyedTenantPartition tenant, string aggregateId) =>
		Key($"migration-v1/{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment(aggregateId)}.json");

	public async Task<ColdArchiveMigrationObject?> ReadAsync(string key, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await _client.GetObjectAsync(new GetObjectRequest
			{
				BucketName = _bucket, Key = ValidateKey(key),
			}, cancellationToken).ConfigureAwait(false);
			if (response.HttpStatusCode != HttpStatusCode.OK || !string.IsNullOrEmpty(response.ContentRange)
				|| string.Equals(response.DeleteMarker, "true", StringComparison.OrdinalIgnoreCase)
				|| string.IsNullOrEmpty(response.ETag))
			{
				throw new InvalidDataException("Migration requires a complete object response and its revision.");
			}

			using var content = new MemoryStream();
			await response.ResponseStream.CopyToAsync(content, cancellationToken).ConfigureAwait(false);
			if (content.Length != response.ContentLength)
			{
				throw new InvalidDataException("The archive response length does not match the stored object length.");
			}

			return new ColdArchiveMigrationObject(content.ToArray(), response.ETag);
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound && ex.ErrorCode == "NoSuchKey")
		{
			// Under the required retention policy, a delete marker cannot replace an archive or receipt.
			// A missing bucket or denied read is never evidence that a stream is fresh.
			return null;
		}
	}

	public async Task<bool> TryCreateAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
	{
		try
		{
			using var stream = new MemoryStream(content.ToArray(), writable: false);
			var response = await _client.PutObjectAsync(new PutObjectRequest
			{
				BucketName = _bucket, Key = ValidateKey(key), InputStream = stream, IfNoneMatch = "*",
			}, cancellationToken).ConfigureAwait(false);
			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				throw new InvalidDataException("S3 did not acknowledge the conditional archive creation.");
			}

			return true;
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed && ex.ErrorCode == "PreconditionFailed")
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
		_ = EventSerializationDefaults.TryApplyTypeInfoResolver(options, AwsS3ColdStoreJsonContext.Default);
		return (JsonTypeInfo<List<StoredEvent>>)options.GetTypeInfo(typeof(List<StoredEvent>));
	}

	private static string Encode(string value) => Base64Url.EncodeToString(StrictUtf8.GetBytes(value));
	private string Key(string suffix) => ValidateKey(string.IsNullOrEmpty(_prefix) ? suffix : $"{_prefix}/{suffix}");
	private static string ValidateKey(string key)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);
		if (StrictUtf8.GetByteCount(key) > 1024)
		{
			throw new ArgumentException("The archive object key exceeds the S3 UTF-8 byte limit.", nameof(key));
		}

		return key;
	}
}
