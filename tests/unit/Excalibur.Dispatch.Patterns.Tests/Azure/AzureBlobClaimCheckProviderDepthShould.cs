// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns.Tests.Azure;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AzureBlobClaimCheckProviderDepthShould
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		var logger = NullLogger<AzureBlobClaimCheckProvider>.Instance;
		_ = Should.Throw<ArgumentNullException>(() => new AzureBlobClaimCheckProvider(null!, logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(CreateDefaultOptions());
		_ = Should.Throw<ArgumentNullException>(() => new AzureBlobClaimCheckProvider(options, null!));
	}

	[Fact]
	public void ShouldUseClaimCheck_ReturnsTrue_WhenPayloadExceedsThreshold()
	{
		var provider = CreateProvider(new ClaimCheckOptions { PayloadThreshold = 100 }).Provider;
		provider.ShouldUseClaimCheck(new byte[200]).ShouldBeTrue();
	}

	[Fact]
	public void ShouldUseClaimCheck_ReturnsFalse_WhenPayloadBelowThreshold()
	{
		var provider = CreateProvider(new ClaimCheckOptions { PayloadThreshold = 100 }).Provider;
		provider.ShouldUseClaimCheck(new byte[50]).ShouldBeFalse();
	}

	[Fact]
	public void ShouldUseClaimCheck_ReturnsTrue_WhenPayloadEqualsThreshold()
	{
		var provider = CreateProvider(new ClaimCheckOptions { PayloadThreshold = 100 }).Provider;
		provider.ShouldUseClaimCheck(new byte[100]).ShouldBeTrue();
	}

	[Fact]
	public void ShouldUseClaimCheck_ThrowsArgumentNullException_WhenPayloadIsNull()
	{
		var provider = CreateProvider().Provider;
		_ = Should.Throw<ArgumentNullException>(() => provider.ShouldUseClaimCheck(null!));
	}

	[Fact]
	public void ShouldUseClaimCheck_ReturnsFalse_WhenPayloadIsEmpty()
	{
		var provider = CreateProvider(new ClaimCheckOptions { PayloadThreshold = 100 }).Provider;
		provider.ShouldUseClaimCheck([]).ShouldBeFalse();
	}

	[Fact]
	public void ImplementsIClaimCheckProvider()
	{
		typeof(AzureBlobClaimCheckProvider).IsAssignableTo(typeof(IClaimCheckProvider)).ShouldBeTrue();
	}

	[Fact]
	public async Task StoreAsync_StoresCompressedPayload_WithMetadataAndChecksum()
	{
		var setup = CreateProvider(new ClaimCheckOptions
		{
			EnableCompression = true,
			CompressionThreshold = 16,
			ValidateChecksum = true,
			IdPrefix = "cc-",
		});

		var payload = Encoding.UTF8.GetBytes(new string('a', 1024));
		var metadata = new ClaimCheckMetadata
		{
			ContentType = "application/json",
		};
		metadata.Properties["tenant"] = "alpha";

		BinaryData? uploaded = null;
		BlobUploadOptions? uploadOptions = null;

		A.CallTo(() => setup.BlobClient.UploadAsync(A<BinaryData>._, A<BlobUploadOptions>._, A<CancellationToken>._))
			.Invokes((BinaryData data, BlobUploadOptions options, CancellationToken _) =>
			{
				uploaded = data;
				uploadOptions = options;
			})
			.Returns(Task.FromResult<Response<BlobContentInfo>>(null!));

		var reference = await setup.Provider.StoreAsync(payload, CancellationToken.None, metadata);

		reference.Id.ShouldStartWith("cc-");
		reference.Metadata.IsCompressed.ShouldBeTrue();
		reference.Metadata.OriginalSize.ShouldBe(payload.Length);
		setup.EnsureContainerCallCount().ShouldBe(1);
		uploaded.ShouldNotBeNull();
		uploaded!.ToArray().Length.ShouldBeLessThan(payload.Length);
		uploadOptions.ShouldNotBeNull();
		uploadOptions!.Metadata.ShouldContainKey("checksum");
		uploadOptions.Metadata["custom_tenant"].ShouldBe("alpha");
		uploadOptions.HttpHeaders.ContentEncoding.ShouldBe("gzip");
	}

	[Fact]
	public async Task RetrieveAsync_ReturnsOriginalPayload_WhenCompressedAndChecksumMatches()
	{
		var setup = CreateProvider(new ClaimCheckOptions
		{
			EnableCompression = true,
			CompressionThreshold = 16,
			ValidateChecksum = true,
		});

		var payload = Encoding.UTF8.GetBytes(new string('b', 768));
		BinaryData? storedPayload = null;
		Dictionary<string, string>? storedMetadata = null;

		A.CallTo(() => setup.BlobClient.UploadAsync(A<BinaryData>._, A<BlobUploadOptions>._, A<CancellationToken>._))
			.Invokes((BinaryData data, BlobUploadOptions options, CancellationToken _) =>
			{
				storedPayload = data;
				storedMetadata = new Dictionary<string, string>(options.Metadata, StringComparer.Ordinal);
			})
			.Returns(Task.FromResult<Response<BlobContentInfo>>(null!));

		A.CallTo(() => setup.BlobClient.DownloadContentAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var details = BlobsModelFactory.BlobDownloadDetails(
					lastModified: DateTimeOffset.UtcNow,
					metadata: storedMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal));
				var result = BlobsModelFactory.BlobDownloadResult(storedPayload ?? BinaryData.FromBytes([]), details);
				return Task.FromResult(Response.FromValue(result, A.Fake<Response>()));
			});

		var reference = await setup.Provider.StoreAsync(payload, CancellationToken.None);
		var retrieved = await setup.Provider.RetrieveAsync(reference, CancellationToken.None);

		retrieved.ShouldBe(payload);
		setup.EnsureContainerCallCount().ShouldBe(1);
	}

	[Fact]
	public async Task RetrieveAsync_ThrowsInvalidOperationException_WhenBlobMissing()
	{
		var setup = CreateProvider();

		A.CallTo(() => setup.BlobClient.DownloadContentAsync(A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "missing"));

		var reference = new ClaimCheckReference { Id = "missing-id" };
		var ex = await Should.ThrowAsync<InvalidOperationException>(() => setup.Provider.RetrieveAsync(reference, CancellationToken.None));
		ex.Message.ShouldContain("not found");
	}

	[Fact]
	public async Task RetrieveAsync_ThrowsInvalidOperationException_WhenChecksumDoesNotMatch()
	{
		var setup = CreateProvider(new ClaimCheckOptions
		{
			EnableCompression = false,
			ValidateChecksum = true,
		});

		var payload = Encoding.UTF8.GetBytes("checksum");
		var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["checksum"] = "invalid",
		};

		var details = BlobsModelFactory.BlobDownloadDetails(lastModified: DateTimeOffset.UtcNow, metadata: metadata);
		var result = BlobsModelFactory.BlobDownloadResult(BinaryData.FromBytes(payload), details);

		A.CallTo(() => setup.BlobClient.DownloadContentAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(result, A.Fake<Response>())));

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			setup.Provider.RetrieveAsync(new ClaimCheckReference { Id = "mismatch" }, CancellationToken.None));

		ex.Message.ShouldContain("Checksum validation failed");
	}

	[Fact]
	public async Task DeleteAsync_ReturnsTrue_WhenDeleted()
	{
		var setup = CreateProvider();
		A.CallTo(() => setup.BlobClient.DeleteIfExistsAsync(A<DeleteSnapshotsOption>._, A<BlobRequestConditions>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(true, A.Fake<Response>())));

		var deleted = await setup.Provider.DeleteAsync(new ClaimCheckReference { Id = "id-1" }, CancellationToken.None);
		deleted.ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteAsync_ReturnsFalse_WhenDeleteThrows()
	{
		var setup = CreateProvider();
		A.CallTo(() => setup.BlobClient.DeleteIfExistsAsync(A<DeleteSnapshotsOption>._, A<BlobRequestConditions>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(500, "error"));

		var deleted = await setup.Provider.DeleteAsync(new ClaimCheckReference { Id = "id-2" }, CancellationToken.None);
		deleted.ShouldBeFalse();
	}

	[Fact]
	public async Task StoreAsync_ThrowsArgumentNullException_WhenPayloadIsNull()
	{
		var provider = CreateProvider().Provider;
		_ = await Should.ThrowAsync<ArgumentNullException>(() => provider.StoreAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task RetrieveAsync_ThrowsArgumentNullException_WhenReferenceIsNull()
	{
		var provider = CreateProvider().Provider;
		_ = await Should.ThrowAsync<ArgumentNullException>(() => provider.RetrieveAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowsArgumentNullException_WhenReferenceIsNull()
	{
		var provider = CreateProvider().Provider;
		_ = await Should.ThrowAsync<ArgumentNullException>(() => provider.DeleteAsync(null!, CancellationToken.None));
	}

	private static (AzureBlobClaimCheckProvider Provider, BlobContainerClient ContainerClient, BlobClient BlobClient, Func<int> EnsureContainerCallCount) CreateProvider(
		ClaimCheckOptions? overrides = null)
	{
		var options = CreateDefaultOptions();
		ApplyOverrides(options, overrides);

		var provider = new AzureBlobClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(options), NullLogger<AzureBlobClaimCheckProvider>.Instance);
		var fakeContainer = A.Fake<BlobContainerClient>();
		var fakeBlob = A.Fake<BlobClient>();
		var ensureCalls = 0;

		A.CallTo(() => fakeContainer.CreateIfNotExistsAsync(
				A<PublicAccessType>._,
				A<IDictionary<string, string>>._,
				A<BlobContainerEncryptionScopeOptions>._,
				A<CancellationToken>._))
			.Invokes(() => ensureCalls++)
			.Returns(Task.FromResult<Response<BlobContainerInfo>>(null!));

		A.CallTo(() => fakeContainer.GetBlobClient(A<string>._)).Returns(fakeBlob);
		A.CallTo(() => fakeBlob.Uri).Returns(new Uri("https://unit-tests.local/container/blob"));

		SetPrivateField(provider, "_containerClient", fakeContainer);

		return (provider, fakeContainer, fakeBlob, () => ensureCalls);
	}

	private static ClaimCheckOptions CreateDefaultOptions() =>
		new()
		{
			ConnectionString = "UseDevelopmentStorage=true",
			ContainerName = "test",
			PayloadThreshold = 100,
			CompressionThreshold = 256,
			EnableCompression = false,
			ValidateChecksum = false,
			RetentionPeriod = TimeSpan.FromDays(7),
			IdPrefix = "claim-",
		};

	private static void ApplyOverrides(ClaimCheckOptions options, ClaimCheckOptions? overrides)
	{
		if (overrides is null)
		{
			return;
		}

		options.ConnectionString = string.IsNullOrWhiteSpace(overrides.ConnectionString) ? options.ConnectionString : overrides.ConnectionString;
		options.ContainerName = string.IsNullOrWhiteSpace(overrides.ContainerName) ? options.ContainerName : overrides.ContainerName;
		options.PayloadThreshold = overrides.PayloadThreshold;
		options.CompressionThreshold = overrides.CompressionThreshold;
		options.EnableCompression = overrides.EnableCompression;
		options.ValidateChecksum = overrides.ValidateChecksum;
		options.RetentionPeriod = overrides.RetentionPeriod == default ? options.RetentionPeriod : overrides.RetentionPeriod;
		options.IdPrefix = string.IsNullOrWhiteSpace(overrides.IdPrefix) ? options.IdPrefix : overrides.IdPrefix;
	}

	private static void SetPrivateField<T>(object instance, string fieldName, T value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}
}
