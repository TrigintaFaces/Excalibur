// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Patterns.Tests.Azure;

/// <summary>
/// Binds the retention contract to the blob provider: a zero retention period disables expiry, and an
/// expired payload is reported as missing rather than as an invalid state.
/// </summary>
/// <remarks>
/// These arms build their own provider rather than reusing the depth suite's factory, because that
/// factory treats a zero retention period as "not supplied" and substitutes its default -- which is
/// exactly the value under test here.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AzureBlobClaimCheckProviderExpiryShould
{
	[Fact]
	public async Task StoreAsync_WithZeroRetentionPeriod_ShouldLeaveExpiryUnset()
	{
		// A zero retention period means "never expires". Adding it to the current instant would stamp an
		// expiry equal to the store time, marking every payload expired the moment it is written.
		var setup = CreateProvider(TimeSpan.Zero);

		A.CallTo(() => setup.BlobClient.UploadAsync(A<BinaryData>._, A<BlobUploadOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<Response<BlobContentInfo>>(null!));

		var reference = await setup.Provider.StoreAsync("payload"u8.ToArray(), CancellationToken.None);

		reference.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public async Task RetrieveAsync_WithZeroRetentionPeriod_ShouldReturnPayload()
	{
		var setup = CreateProvider(TimeSpan.Zero);
		var payload = "payload that outlives its message"u8.ToArray();
		BinaryData? stored = null;
		Dictionary<string, string>? storedMetadata = null;

		A.CallTo(() => setup.BlobClient.UploadAsync(A<BinaryData>._, A<BlobUploadOptions>._, A<CancellationToken>._))
			.Invokes((BinaryData data, BlobUploadOptions options, CancellationToken _) =>
			{
				stored = data;
				storedMetadata = new Dictionary<string, string>(options.Metadata, StringComparer.Ordinal);
			})
			.Returns(Task.FromResult<Response<BlobContentInfo>>(null!));

		A.CallTo(() => setup.BlobClient.DownloadContentAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var details = BlobsModelFactory.BlobDownloadDetails(
					lastModified: DateTimeOffset.UtcNow,
					metadata: storedMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal));
				var result = BlobsModelFactory.BlobDownloadResult(stored ?? BinaryData.FromBytes([]), details);
				return Task.FromResult(Response.FromValue(result, A.Fake<Response>()));
			});

		var reference = await setup.Provider.StoreAsync(payload, CancellationToken.None);
		var retrieved = await setup.Provider.RetrieveAsync(reference, CancellationToken.None);

		retrieved.ShouldBe(payload);
	}

	[Fact]
	public async Task RetrieveAsync_WithExpiredReference_ShouldThrowKeyNotFoundException()
	{
		// An expired payload is a form of missing payload, so it raises the same exception a deleted or
		// never-stored one does.
		var setup = CreateProvider(TimeSpan.FromHours(1));
		var idExpired = MintedId("claim-");
		var reference = new ClaimCheckReference
		{
			Id = idExpired,
			BlobName = "claim-check/claim-expired",
			ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
		};

		var ex = await Should.ThrowAsync<KeyNotFoundException>(
			() => setup.Provider.RetrieveAsync(reference, CancellationToken.None));

		ex.Message.ShouldContain(idExpired);

		// The blob is never downloaded: expiry is decided before the request goes out.
		A.CallTo(() => setup.BlobClient.DownloadContentAsync(A<CancellationToken>._)).MustNotHaveHappened();
	}

	private static (AzureBlobClaimCheckProvider Provider, BlobClient BlobClient) CreateProvider(TimeSpan retentionPeriod)
	{
		var options = new ClaimCheckOptions
		{
			ConnectionString = "UseDevelopmentStorage=true",
			ContainerName = "test",
			PayloadThreshold = 100,
			EnableCompression = false,
			ValidateChecksum = false,
			RetentionPeriod = retentionPeriod,
			IdPrefix = "claim-",
		};

		var provider = new AzureBlobClaimCheckProvider(
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<AzureBlobClaimCheckProvider>.Instance);

		var fakeContainer = A.Fake<BlobContainerClient>();
		var fakeBlob = A.Fake<BlobClient>();

		A.CallTo(() => fakeContainer.CreateIfNotExistsAsync(
				A<PublicAccessType>._,
				A<IDictionary<string, string>>._,
				A<BlobContainerEncryptionScopeOptions>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<Response<BlobContainerInfo>>(null!));

		A.CallTo(() => fakeContainer.GetBlobClient(A<string>._)).Returns(fakeBlob);
		A.CallTo(() => fakeBlob.Uri).Returns(new Uri("https://unit-tests.local/container/blob"));

		var field = typeof(AzureBlobClaimCheckProvider)
			.GetField("_containerClient", BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(provider, fakeContainer);

		return (provider, fakeBlob);
	}
	/// <summary>
	/// A well-formed identifier of the shape the provider mints: the configured prefix, the date partition
	/// the identifier carries, and a 128-bit body.
	/// </summary>
	/// <remarks>
	/// These arms used descriptive literals. The provider now refuses any identifier it did not mint,
	/// because a claim-check reference arrives on the wire and resolving a blob name from one let a
	/// publisher name any blob in the container. A descriptive literal is exactly the shape that is now
	/// refused, so the arms mint a real identifier.
	/// </remarks>
	private static string MintedId(string prefix = "cc-") =>
		$"{prefix}{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";

}
