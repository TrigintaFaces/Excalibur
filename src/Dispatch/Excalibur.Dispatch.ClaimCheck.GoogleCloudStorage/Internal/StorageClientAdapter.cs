// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Google;
using Google.Cloud.Storage.V1;

using GcsObject = Google.Apis.Storage.v1.Data.Object;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage.Internal;

/// <summary>
/// Default <see cref="IStorageClientSeam"/> implementation that forwards
/// to a real <see cref="StorageClient"/>. This adapter is the only place
/// in the claim check path that touches the live Google Cloud Storage SDK
/// client type — tests substitute at the seam, never at the SDK type
/// directly.
/// </summary>
internal sealed class StorageClientAdapter : IStorageClientSeam
{
	private readonly StorageClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="StorageClientAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Google Cloud Storage client.</param>
	public StorageClientAdapter(StorageClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<GcsObject> UploadObjectAsync(
		GcsObject destination,
		Stream source,
		CancellationToken cancellationToken)
		=> await _inner.UploadObjectAsync(destination, source, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

	/// <inheritdoc/>
	public Task DownloadObjectAsync(
		string bucket,
		string objectName,
		Stream destination,
		CancellationToken cancellationToken)
		=> _inner.DownloadObjectAsync(bucket, objectName, destination, cancellationToken: cancellationToken);

	/// <inheritdoc/>
	public async Task<bool> ObjectExistsAsync(
		string bucket,
		string objectName,
		CancellationToken cancellationToken)
	{
		try
		{
			_ = await _inner.GetObjectAsync(bucket, objectName, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			return true;
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			// Only a 404 is translated. Any other status is a real fault -- a permission failure or an
			// unreachable bucket must not be reported as "the object is not there", because the caller
			// would then record a successful delete of something it never looked at.
			return false;
		}
	}

	/// <inheritdoc/>
	public Task DeleteObjectAsync(
		string bucket,
		string objectName,
		CancellationToken cancellationToken)
		=> _inner.DeleteObjectAsync(bucket, objectName, cancellationToken: cancellationToken);
}
