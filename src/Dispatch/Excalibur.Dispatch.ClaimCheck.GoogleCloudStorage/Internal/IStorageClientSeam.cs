// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using GcsObject = Google.Apis.Storage.v1.Data.Object;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Google.Cloud.Storage.V1.StorageClient"/>
/// used by <see cref="GcsClaimCheckStore"/>. Exposes <b>use-case</b> operations
/// so tests can substitute the SDK without depending on which
/// <see cref="Google.Cloud.Storage.V1.StorageClient"/> overloads remain virtual
/// in a given SDK minor version. Not a consumer-facing abstraction; do not make
/// this public.
/// </summary>
/// <remarks>
/// Follows the ADR-142 §D7 canonical template set by
/// <c>IServiceBusClient</c> (S798, <c>bd-wy56o5</c>): flat use-case methods,
/// not SDK topology mirroring. Data-shaped SDK types
/// (<see cref="Object"/>) cross the seam — they are property bags and
/// are safe to construct directly.
/// </remarks>
internal interface IStorageClientSeam
{
	/// <summary>
	/// Uploads an object to a GCS bucket. Wraps
	/// <see cref="Google.Cloud.Storage.V1.StorageClient.UploadObjectAsync(GcsObject, System.IO.Stream, Google.Cloud.Storage.V1.UploadObjectOptions, CancellationToken, IProgress{Google.Apis.Upload.IUploadProgress})"/>.
	/// </summary>
	Task<GcsObject> UploadObjectAsync(
		GcsObject destination,
		Stream source,
		CancellationToken cancellationToken);

	/// <summary>
	/// Downloads an object from a GCS bucket. Wraps
	/// <see cref="Google.Cloud.Storage.V1.StorageClient.DownloadObjectAsync(string, string, System.IO.Stream, Google.Cloud.Storage.V1.DownloadObjectOptions, CancellationToken)"/>.
	/// </summary>
	Task DownloadObjectAsync(
		string bucket,
		string objectName,
		Stream destination,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes an object from a GCS bucket. Wraps
	/// <see cref="Google.Cloud.Storage.V1.StorageClient.DeleteObjectAsync(string, string, Google.Cloud.Storage.V1.DeleteObjectOptions, CancellationToken)"/>.
	/// </summary>
	Task DeleteObjectAsync(
		string bucket,
		string objectName,
		CancellationToken cancellationToken);
}
