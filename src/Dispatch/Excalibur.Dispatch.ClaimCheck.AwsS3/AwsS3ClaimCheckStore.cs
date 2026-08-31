// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Amazon.S3;
using Amazon.S3.Model;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ClaimCheck.AwsS3;

/// <summary>
/// AWS S3 implementation of the <see cref="IClaimCheckProvider"/> for storing
/// large message payloads in Amazon S3.
/// </summary>
public sealed partial class AwsS3ClaimCheckStore : IClaimCheckProvider, IDisposable
{
	private readonly IAmazonS3 _s3Client;
	private readonly AwsS3ClaimCheckOptions _options;
	private readonly ClaimCheckOptions _claimCheckOptions;
	private readonly ILogger<AwsS3ClaimCheckStore> _logger;
	private readonly bool _ownsClient;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsS3ClaimCheckStore"/> class.
	/// </summary>
	/// <param name="options">The S3-specific options.</param>
	/// <param name="claimCheckOptions">The core claim check options.</param>
	/// <param name="logger">The logger instance.</param>
	public AwsS3ClaimCheckStore(
		IOptions<AwsS3ClaimCheckOptions> options,
		IOptions<ClaimCheckOptions> claimCheckOptions,
		ILogger<AwsS3ClaimCheckStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_claimCheckOptions = claimCheckOptions?.Value ?? throw new ArgumentNullException(nameof(claimCheckOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_s3Client = CreateClient(_options);
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsS3ClaimCheckStore"/> class
	/// with an existing S3 client.
	/// </summary>
	/// <param name="s3Client">The S3 client.</param>
	/// <param name="options">The S3-specific options.</param>
	/// <param name="claimCheckOptions">The core claim check options.</param>
	/// <param name="logger">The logger instance.</param>
	public AwsS3ClaimCheckStore(
		IAmazonS3 s3Client,
		IOptions<AwsS3ClaimCheckOptions> options,
		IOptions<ClaimCheckOptions> claimCheckOptions,
		ILogger<AwsS3ClaimCheckStore> logger)
	{
		_s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_claimCheckOptions = claimCheckOptions?.Value ?? throw new ArgumentNullException(nameof(claimCheckOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_ownsClient = false;
	}

	/// <inheritdoc />
	public async Task<ClaimCheckReference> StoreAsync(
		byte[] payload,
		CancellationToken cancellationToken,
		ClaimCheckMetadata? metadata = null)
	{
		ArgumentNullException.ThrowIfNull(payload);

		var id = $"{_claimCheckOptions.IdPrefix}{Guid.NewGuid():N}";
		var key = GetObjectKey(id);

		metadata ??= new ClaimCheckMetadata();

		var s3Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["claim-check-id"] = id,
			["original-size"] = payload.Length.ToString(CultureInfo.InvariantCulture),
			["stored-at"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
		};

		if (!string.IsNullOrEmpty(metadata.ContentType))
		{
			s3Metadata["content-type"] = metadata.ContentType;
		}

		await using var stream = new MemoryStream(payload);
		var putRequest = new PutObjectRequest
		{
			BucketName = _options.BucketName,
			Key = key,
			InputStream = stream,
			ContentType = metadata.ContentType ?? "application/octet-stream"
		};

		foreach (var kvp in s3Metadata)
		{
			putRequest.Metadata.Add(kvp.Key, kvp.Value);
		}

		await _s3Client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);

		LogStoredPayload(id, payload.Length);

		var storedAt = DateTimeOffset.UtcNow;

		return new ClaimCheckReference
		{
			Id = id,
			BlobName = key,
			Location = $"s3://{_options.BucketName}/{key}",
			Size = payload.Length,
			StoredAt = storedAt,
			ExpiresAt = _claimCheckOptions.ResolveExpiresAt(storedAt),
			Metadata = metadata
		};
	}

	/// <inheritdoc />
	public async Task<byte[]> RetrieveAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		// Resolve from the recorded name rather than recomputing one. GetObjectKey derives a
		// date-partitioned key from the CURRENT UTC date, so a payload stored at 23:59:59 was looked for
		// under the next day's prefix a second later and reported not-found while still sitting in the
		// bucket. Claim check exists for payloads that outlive their message, so crossing midnight is the
		// ordinary case. Recomputation remains only for references written before the name was recorded.
		var key = string.IsNullOrEmpty(reference.BlobName) ? GetObjectKey(reference.Id) : reference.BlobName;

		// An expired payload is a form of missing payload, so it surfaces as the same exception a deleted
		// or never-stored one does. S3 has no per-object time-to-live -- its lifecycle rules are
		// bucket-wide and delete on a daily schedule rather than at a per-payload instant -- so expiry is
		// enforced here, before the object is fetched, rather than delegated to the store.
		if (reference.IsExpired(DateTimeOffset.UtcNow))
		{
			throw new KeyNotFoundException($"Claim check '{reference.Id}' has expired.");
		}

		try
		{
			var response = await _s3Client.GetObjectAsync(
				_options.BucketName, key, cancellationToken).ConfigureAwait(false);

			await using var responseStream = response.ResponseStream;
			using var ms = new MemoryStream();
			await responseStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);

			var data = ms.ToArray();
			LogRetrievedPayload(reference.Id, data.Length);
			return data;
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			throw new KeyNotFoundException($"Claim check '{reference.Id}' not found in S3.", ex);
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(
		ClaimCheckReference reference,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reference);

		// Resolve from the recorded name rather than recomputing one. GetObjectKey derives a
		// date-partitioned key from the CURRENT UTC date, so a payload stored at 23:59:59 was looked for
		// under the next day's prefix a second later and reported not-found while still sitting in the
		// bucket. Claim check exists for payloads that outlive their message, so crossing midnight is the
		// ordinary case. Recomputation remains only for references written before the name was recorded.
		var key = string.IsNullOrEmpty(reference.BlobName) ? GetObjectKey(reference.Id) : reference.BlobName;

		try
		{
			// S3's DeleteObject is deliberately idempotent -- it returns 204 for an object that was never
			// there -- so the delete itself cannot answer whether one existed. The contract defines the
			// return value as exactly that observation, so it has to be obtained before deleting. The
			// catch below cannot serve: a missing key raises nothing to catch.
			var existed = await ObjectExistsAsync(key, cancellationToken).ConfigureAwait(false);

			await _s3Client.DeleteObjectAsync(
				_options.BucketName, key, cancellationToken).ConfigureAwait(false);

			LogDeletedClaimCheck(reference.Id);

			return existed;
		}
		catch (AmazonS3Exception)
		{
			return false;
		}
	}

	/// <inheritdoc />
	public bool ShouldUseClaimCheck(byte[] payload)
	{
		ArgumentNullException.ThrowIfNull(payload);
		return payload.Length >= _claimCheckOptions.PayloadThreshold;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_ownsClient)
		{
			_s3Client.Dispose();
		}
	}

	/// <summary>
	/// Reports whether an object is present, so a delete can say what it observed.
	/// </summary>
	/// <param name="key">The object key.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> when the object exists; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// A metadata request rather than a read: it transfers no payload. This is the cost of honouring a
	/// contract that defines the delete's result as an observation, on a service whose delete is
	/// deliberately silent about what it removed.
	/// </remarks>
	private async Task<bool> ObjectExistsAsync(string key, CancellationToken cancellationToken)
	{
		try
		{
			_ = await _s3Client.GetObjectMetadataAsync(
				_options.BucketName, key, cancellationToken).ConfigureAwait(false);

			return true;
		}
		catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	private string GetObjectKey(string claimCheckId)
	{
		var date = DateTimeOffset.UtcNow;
		return $"{_options.Prefix}{date:yyyy/MM/dd}/{claimCheckId}";
	}

	/// <summary>
	/// Builds an <see cref="IAmazonS3"/> client from the configured options. Exposed to the provider's DI
	/// registration so the container owns a single <see cref="IAmazonS3"/> that the store depends on through
	/// the abstraction (rather than each store constructing its own), while a consumer that pre-registers
	/// their own <see cref="IAmazonS3"/> (e.g. via the AWS SDK's <c>AddAWSService&lt;IAmazonS3&gt;()</c>)
	/// takes precedence.
	/// </summary>
	/// <param name="options">The S3 options to build the client from.</param>
	/// <returns>A configured S3 client.</returns>
	internal static IAmazonS3 CreateClient(AwsS3ClaimCheckOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var config = new AmazonS3Config();

		if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
		{
			config.ServiceURL = options.ServiceUrl;
			config.ForcePathStyle = true;
		}
		else if (!string.IsNullOrWhiteSpace(options.Region))
		{
			config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
		}

		if (!string.IsNullOrWhiteSpace(options.AccessKey) && !string.IsNullOrWhiteSpace(options.SecretKey))
		{
			return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
		}

		return new AmazonS3Client(config);
	}

	[LoggerMessage(3300, LogLevel.Debug, "Stored claim check '{ClaimCheckId}' in S3 ({Size} bytes)")]
	private partial void LogStoredPayload(string claimCheckId, int size);

	[LoggerMessage(3301, LogLevel.Debug, "Retrieved claim check '{ClaimCheckId}' from S3 ({Size} bytes)")]
	private partial void LogRetrievedPayload(string claimCheckId, int size);

	[LoggerMessage(3302, LogLevel.Debug, "Deleted claim check '{ClaimCheckId}' from S3")]
	private partial void LogDeletedClaimCheck(string claimCheckId);
}
