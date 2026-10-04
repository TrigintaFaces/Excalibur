// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using System.Net.Http.Headers;

using Google;
using Google.Apis.Download;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.Apis.Upload;
using Google.Cloud.Storage.V1;

using Excalibur.EventSourcing.Gcs;
using Excalibur.Dispatch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

using StorageObject = Google.Apis.Storage.v1.Data.Object;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "EventSourcing")]
public sealed class GcsColdArchiveMigrationStorageShould : IDisposable
{
	private static readonly byte[] Body = "123456789"u8.ToArray();
	private readonly StorageClient _client = A.Fake<StorageClient>();
	private readonly StorageService _service = new(new BaseClientService.Initializer { GZipEnabled = false });

	public GcsColdArchiveMigrationStorageShould()
	{
		_service.HttpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
		A.CallTo(() => _client.Service).Returns(_service);
	}

	public void Dispose() => _service.Dispose();

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RespectDiOwnershipOfSuppliedClients(bool suppliedByFactory)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburEventSourcing(es => es.UseGcsColdEventStore(gcs =>
		{
			gcs.BucketName("bucket").ObjectPrefix("prefix");
			if (suppliedByFactory)
			{
				gcs.ClientFactory(_ => _client);
			}
			else
			{
				gcs.Client(_client);
			}
		}));
		using (var provider = services.BuildServiceProvider())
		{
			_ = provider.GetRequiredService<IColdEventStore>();
		}

		if (suppliedByFactory)
		{
			// DI owns factory results; the store must not add a second disposal.
			A.CallTo(() => _client.Dispose()).MustHaveHappenedOnceExactly();
		}
		else
		{
			A.CallTo(() => _client.Dispose()).MustNotHaveHappened();
			_client.Service.ShouldBeSameAs(_service);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void DisposeOnlyOwnedClientsAndOnlyOnce(bool ownsClient)
	{
		var store = new GcsColdEventStore(_client, "bucket", "", NullLogger<GcsColdEventStore>.Instance,
			ColdArchiveLayout.Legacy, ownsClient);
		store.Dispose();
		store.Dispose();
		if (ownsClient)
		{
			A.CallTo(() => _client.Dispose()).MustHaveHappenedOnceExactly();
		}
		else
		{
			A.CallTo(() => _client.Dispose()).MustNotHaveHappened();
			_client.Service.ShouldBeSameAs(_service);
		}
	}

	[Fact]
	public void BuildAnOwnedRawClientUsingSdkEndpointConfiguration()
	{
		using var client = new GcsRawStorageClientBuilder
		{
			ApiKey = "unit-test-key",
			BaseUri = "http://localhost:12345/",
		}.Build();
		client.Service.GZipEnabled.ShouldBeFalse();
		client.Service.BaseUri.ShouldStartWith("http://localhost:12345/");
		client.Service.HttpClient.DefaultRequestHeaders.AcceptEncoding.ShouldContain(e => e.Value == "gzip");
		_ = new GcsColdArchiveMigrationStorage(client, "bucket", "prefix");
	}

	[Fact]
	public async Task GuardLegacyWithoutAccessingTheBorrowedHttpService()
	{
		A.CallTo(() => _client.Service).Throws(new NotSupportedException("No raw HTTP service exposed."));
		A.CallTo(() => _client.GetObjectAsync("bucket", A<string>._, A<GetObjectOptions>._, A<CancellationToken>._))
			.ThrowsAsync(Error(HttpStatusCode.NotFound));
		A.CallTo(() => _client.GetBucketAsync("bucket", A<GetBucketOptions>._, A<CancellationToken>._)).Returns(new Bucket { Name = "bucket" });
		A.CallTo(() => _client.DownloadObjectAsync("bucket", A<string>._, A<Stream>._, A<DownloadObjectOptions>._,
			A<CancellationToken>._, A<IProgress<IDownloadProgress>>._)).ThrowsAsync(Error(HttpStatusCode.NotFound));
		var tenant = KeyedTenantPartition.Scoped("tenant");
		var legacy = new GcsColdEventStore(_client, "bucket", "", NullLogger<GcsColdEventStore>.Instance);
		(await legacy.ReadAsync(tenant, "aggregate", "Order", CancellationToken.None)).ShouldBeEmpty();
		(await legacy.WriteAsync(tenant, "aggregate", "Order", [], CancellationToken.None)).ShouldBe(-1);
		A.CallTo(() => _client.Service).MustNotHaveHappened();
		A.CallTo(() => _client.GetObjectAsync("bucket", "layout-v1.json", A<GetObjectOptions>._, A<CancellationToken>._))
			.Returns(new StorageObject { Name = "layout-v1.json", Bucket = "bucket" });
		await Should.ThrowAsync<InvalidOperationException>(() => legacy.ReadAsync(tenant, "aggregate", "Order", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => legacy.WriteAsync(tenant, "aggregate", "Order", [], CancellationToken.None));
		A.CallTo(() => _client.Service).MustNotHaveHappened();
		var typed = new GcsColdEventStore(_client, "bucket", "", NullLogger<GcsColdEventStore>.Instance, ColdArchiveLayout.TypedV2);
		await Should.ThrowAsync<NotSupportedException>(() => typed.ReadAsync(tenant, "aggregate", "Order", CancellationToken.None));
	}

	[Fact]
	public async Task RefuseToInterpretAnUnavailableLegacyBucketAsEmpty()
	{
		A.CallTo(() => _client.GetObjectAsync("bucket", A<string>._, A<GetObjectOptions>._, A<CancellationToken>._))
			.ThrowsAsync(Error(HttpStatusCode.NotFound));
		var failure = Error(HttpStatusCode.Forbidden);
		A.CallTo(() => _client.GetBucketAsync("bucket", A<GetBucketOptions>._, A<CancellationToken>._)).ThrowsAsync(failure);
		var legacy = new GcsColdEventStore(_client, "bucket", "", NullLogger<GcsColdEventStore>.Instance);
		(await Should.ThrowAsync<GoogleApiException>(() => legacy.ReadAsync(KeyedTenantPartition.Scoped("tenant"), "aggregate", "Order", CancellationToken.None)))
			.ShouldBeSameAs(failure);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PinObservedGenerationAndValidateAgainstOriginalMetadata(bool omitMediaChecksum)
	{
		A.CallTo(() => _client.GetObjectAsync("bucket", "key", A<GetObjectOptions>._, A<CancellationToken>._)).Returns(Metadata());
		var media = Metadata();
		media.Size = null;
		if (omitMediaChecksum)
		{
			media.Crc32c = null;
		}

		Download(Body, media);
		var result = (await Store().ReadAsync("key", CancellationToken.None)).ShouldNotBeNull();
		result.Revision.ShouldBe("42");
		result.CopyContent().ShouldBe(Body);
		A.CallTo(() => _client.DownloadObjectAsync("bucket", "key", A<Stream>._,
			A<DownloadObjectOptions>.That.Matches(o => o.Generation == 42 && o.IfGenerationMatch == 42 && o.DownloadValidationMode == DownloadValidationMode.Always),
			A<CancellationToken>._, A<IProgress<IDownloadProgress>>._)).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData("generation")]
	[InlineData("bucket")]
	[InlineData("key")]
	[InlineData("truncated")]
	[InlineData("corrupt")]
	[InlineData("checksum")]
	[InlineData("size")]
	public async Task RejectMismatchedDownloadOrMetadata(string defect)
	{
		var metadata = Metadata();
		var observed = Metadata();
		var bytes = Body.ToArray();
		switch (defect)
		{
			case "generation": observed.Generation = 43; break;
			case "bucket": observed.Bucket = "other"; break;
			case "key": observed.Name = "other"; break;
			case "truncated": bytes = bytes[..^1]; break;
			case "corrupt": bytes[0] ^= 1; break;
			case "checksum": metadata.Crc32c = null; break;
			case "size": metadata.Size = null; break;
		}

		A.CallTo(() => _client.GetObjectAsync("bucket", "key", A<GetObjectOptions>._, A<CancellationToken>._)).Returns(metadata);
		Download(bytes, observed);
		await Should.ThrowAsync<InvalidDataException>(() => Store().ReadAsync("key", CancellationToken.None));
	}

	[Theory]
	[InlineData(HttpStatusCode.NotFound)]
	[InlineData(HttpStatusCode.PreconditionFailed)]
	public async Task NeverClassifyDisappearanceAfterMetadataAsAbsence(HttpStatusCode status)
	{
		A.CallTo(() => _client.GetObjectAsync("bucket", "key", A<GetObjectOptions>._, A<CancellationToken>._)).Returns(Metadata());
		var error = Error(status);
		A.CallTo(() => _client.DownloadObjectAsync("bucket", "key", A<Stream>._, A<DownloadObjectOptions>._,
			A<CancellationToken>._, A<IProgress<IDownloadProgress>>._)).ThrowsAsync(error);
		(await Should.ThrowAsync<GoogleApiException>(() => Store().ReadAsync("key", CancellationToken.None))).ShouldBeSameAs(error);
		A.CallTo(() => _client.GetBucketAsync(A<string>._, A<GetBucketOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task RequireExistingBucketBeforeReturningAbsence()
	{
		A.CallTo(() => _client.GetObjectAsync("bucket", "key", A<GetObjectOptions>._, A<CancellationToken>._)).ThrowsAsync(Error(HttpStatusCode.NotFound));
		A.CallTo(() => _client.GetBucketAsync("bucket", A<GetBucketOptions>._, A<CancellationToken>._)).Returns(new Bucket { Name = "bucket" });
		(await Store().ReadAsync("key", CancellationToken.None)).ShouldBeNull();
		A.CallTo(() => _client.GetBucketAsync("bucket", A<GetBucketOptions>._, A<CancellationToken>._)).ThrowsAsync(Error(HttpStatusCode.NotFound));
		await Should.ThrowAsync<GoogleApiException>(() => Store().ReadAsync("key", CancellationToken.None));
	}

	[Fact]
	public async Task RequireCreateOnlyAndVerifyAcknowledgedContent()
	{
		byte[]? uploaded = null;
		A.CallTo(() => _client.UploadObjectAsync("bucket", "key", "application/octet-stream", A<Stream>._,
			A<UploadObjectOptions>.That.Matches(o => o.IfGenerationMatch == 0), A<CancellationToken>._, A<IProgress<IUploadProgress>>._))
			.Invokes((string _, string _, string _, Stream input, UploadObjectOptions _, CancellationToken _, IProgress<IUploadProgress> _) =>
			{
				using var copy = new MemoryStream();
				input.CopyTo(copy);
				uploaded = copy.ToArray();
			}).Returns(Metadata());
		(await Store().TryCreateAsync("key", Body, CancellationToken.None)).ShouldBeTrue();
		uploaded.ShouldBe(Body);
	}

	[Theory]
	[InlineData(HttpStatusCode.Forbidden)]
	[InlineData(HttpStatusCode.NotFound)]
	[InlineData(HttpStatusCode.Conflict)]
	public async Task PropagateOtherUploadFailures(HttpStatusCode status)
	{
		UploadFailure(status);
		await Should.ThrowAsync<GoogleApiException>(() => Store().TryCreateAsync("key", Body, CancellationToken.None));
	}

	[Fact]
	public async Task RecognizeCreatePreconditionFailure()
	{
		UploadFailure(HttpStatusCode.PreconditionFailed);
		(await Store().TryCreateAsync("key", Body, CancellationToken.None)).ShouldBeFalse();
	}

	[Theory]
	[InlineData("generation")]
	[InlineData("bucket")]
	[InlineData("key")]
	[InlineData("size")]
	[InlineData("checksum")]
	public async Task RejectUnverifiableUploadAcknowledgement(string defect)
	{
		var acknowledgement = Metadata();
		switch (defect)
		{
			case "generation": acknowledgement.Generation = null; break;
			case "bucket": acknowledgement.Bucket = "other"; break;
			case "key": acknowledgement.Name = "other"; break;
			case "size": acknowledgement.Size = 8; break;
			case "checksum": acknowledgement.Crc32c = "AAAAAA=="; break;
		}

		A.CallTo(() => _client.UploadObjectAsync("bucket", "key", "application/octet-stream", A<Stream>._,
			A<UploadObjectOptions>._, A<CancellationToken>._, A<IProgress<IUploadProgress>>._)).Returns(acknowledgement);
		await Should.ThrowAsync<InvalidDataException>(() => Store().TryCreateAsync("key", Body, CancellationToken.None));
	}

	[Fact]
	public void RejectClientWithoutRawGzipAcceptance()
	{
		_service.HttpClient.DefaultRequestHeaders.AcceptEncoding.Clear();
		Should.Throw<ArgumentException>(() => Store());
	}

	private void UploadFailure(HttpStatusCode status) =>
		A.CallTo(() => _client.UploadObjectAsync("bucket", "key", "application/octet-stream", A<Stream>._,
			A<UploadObjectOptions>._, A<CancellationToken>._, A<IProgress<IUploadProgress>>._)).ThrowsAsync(Error(status));

	private void Download(byte[] bytes, StorageObject observed) =>
		A.CallTo(() => _client.DownloadObjectAsync("bucket", "key", A<Stream>._, A<DownloadObjectOptions>._,
			A<CancellationToken>._, A<IProgress<IDownloadProgress>>._))
			.Invokes((string _, string _, Stream output, DownloadObjectOptions _, CancellationToken _, IProgress<IDownloadProgress> _) => output.Write(bytes))
			.Returns(observed);

	private GcsColdArchiveMigrationStorage Store() => new(_client, "bucket", "events");
	private static GoogleApiException Error(HttpStatusCode status) => new("storage") { HttpStatusCode = status };
	private static StorageObject Metadata() => new() { Bucket = "bucket", Name = "key", Generation = 42, Size = 9, Crc32c = "4waSgw==" };
}
