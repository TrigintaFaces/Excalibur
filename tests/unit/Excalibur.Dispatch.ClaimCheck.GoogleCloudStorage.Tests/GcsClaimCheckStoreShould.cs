using System.Net;
using System.Security.Cryptography;

using Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Google.Apis.Download;
using Google.Apis.Upload;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage.Tests;

using GcsObject = Google.Apis.Storage.v1.Data.Object;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Patterns)]
public sealed class GcsClaimCheckStoreShould : UnitTestBase
{
	[Fact]
	public async Task StoreAsync_ShouldUploadPayloadAndReturnReference()
	{
		var storageClient = A.Fake<StorageClient>();
		GcsObject? capturedObject = null;
		var uploadedLength = -1;
		A.CallTo(() => storageClient.UploadObjectAsync(
				A<GcsObject>._,
				A<Stream>._,
				A<UploadObjectOptions?>._,
				A<CancellationToken>._,
				A<IProgress<IUploadProgress>?>._))
			.Invokes((GcsObject gcsObject, Stream stream, UploadObjectOptions? _, CancellationToken _, IProgress<IUploadProgress>? _) =>
			{
				capturedObject = gcsObject;
				var bytes = new MemoryStream();
				stream.CopyTo(bytes);
				uploadedLength = (int)bytes.Length;
			})
			.Returns(new GcsObject());

		var sut = CreateSut(storageClient);
		var payload = new byte[] { 42, 43, 44 };
		var metadata = new ClaimCheckMetadata { ContentType = "application/json" };

		var reference = await sut.StoreAsync(payload, CancellationToken.None, metadata);

		reference.Id.ShouldStartWith("cc-");
		reference.BlobName.ShouldContain(reference.Id);
		reference.Location.ShouldStartWith("gs://test-bucket/claim-check/");
		reference.Size.ShouldBe(payload.Length);
		reference.Metadata.ShouldNotBeNull();
		reference.Metadata.ContentType.ShouldBe("application/json");
		capturedObject.ShouldNotBeNull();
		capturedObject!.Bucket.ShouldBe("test-bucket");
		capturedObject.ContentType.ShouldBe("application/json");
		capturedObject.Metadata["content-type"].ShouldBe("application/json");
		capturedObject.Metadata["claim-check-id"].ShouldBe(reference.Id);
		capturedObject.Metadata["original-size"].ShouldBe(payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
		uploadedLength.ShouldBe(payload.Length);
	}

	[Fact]
	public async Task StoreAsync_WithNullMetadata_ShouldUseDefaultContentType()
	{
		var storageClient = A.Fake<StorageClient>();
		GcsObject? capturedObject = null;
		A.CallTo(() => storageClient.UploadObjectAsync(
				A<GcsObject>._,
				A<Stream>._,
				A<UploadObjectOptions?>._,
				A<CancellationToken>._,
				A<IProgress<IUploadProgress>?>._))
			.Invokes((GcsObject gcsObject, Stream _, UploadObjectOptions? _, CancellationToken _, IProgress<IUploadProgress>? _) =>
			{
				capturedObject = gcsObject;
			})
			.Returns(new GcsObject());

		var sut = CreateSut(storageClient);

		_ = await sut.StoreAsync([1, 2, 3], CancellationToken.None);

		capturedObject.ShouldNotBeNull();
		capturedObject!.ContentType.ShouldBe("application/octet-stream");
		capturedObject.Metadata.ContainsKey("content-type").ShouldBeFalse();
	}

	[Fact]
	public async Task RetrieveAsync_ShouldReturnPayloadFromBucket()
	{
		var expected = new byte[] { 11, 12, 13 };
		var storageClient = A.Fake<StorageClient>();

		A.CallTo(() => storageClient.DownloadObjectAsync(
				"test-bucket",
				A<string>._,
				A<Stream>._,
				A<DownloadObjectOptions?>._,
				A<CancellationToken>._,
				A<IProgress<IDownloadProgress>?>._))
			.Invokes((string _, string _, Stream stream, DownloadObjectOptions? _, CancellationToken _, IProgress<IDownloadProgress>? _) =>
			{
				stream.Write(expected, 0, expected.Length);
				stream.Position = 0;
			})
			.Returns(new GcsObject());

		var sut = CreateSut(storageClient);
		var reference = new ClaimCheckReference { Id = "cc-retrieve" };

		var data = await sut.RetrieveAsync(reference, CancellationToken.None);

		data.ShouldBe(expected);
	}

	[Fact]
	public async Task RetrieveAsync_WhenObjectMissing_ShouldThrowKeyNotFoundException()
	{
		var storageClient = A.Fake<StorageClient>();
		A.CallTo(() => storageClient.DownloadObjectAsync(
				"test-bucket",
				A<string>._,
				A<Stream>._,
				A<DownloadObjectOptions?>._,
				A<CancellationToken>._,
				A<IProgress<IDownloadProgress>?>._))
			.Throws(CreateGoogleApiException(HttpStatusCode.NotFound));

		var sut = CreateSut(storageClient);
		var reference = new ClaimCheckReference { Id = "cc-missing" };

		var ex = await Should.ThrowAsync<KeyNotFoundException>(() => sut.RetrieveAsync(reference, CancellationToken.None));
		ex.Message.ShouldContain("cc-missing");
	}

	[Fact]
	public async Task DeleteAsync_WhenDeleteSucceeds_ShouldReturnTrue()
	{
		var storageClient = A.Fake<StorageClient>();
		A.CallTo(() => storageClient.DeleteObjectAsync(
				"test-bucket",
				A<string>._,
				A<DeleteObjectOptions?>._,
				A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var sut = CreateSut(storageClient);
		var deleted = await sut.DeleteAsync(new ClaimCheckReference { Id = "cc-delete" }, CancellationToken.None);

		deleted.ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteAsync_WhenProviderThrows_ShouldReturnFalse()
	{
		var storageClient = A.Fake<StorageClient>();
		A.CallTo(() => storageClient.DeleteObjectAsync(
				"test-bucket",
				A<string>._,
				A<DeleteObjectOptions?>._,
				A<CancellationToken>._))
			.Throws(new Google.GoogleApiException("storage", "boom"));

		var sut = CreateSut(storageClient);
		var deleted = await sut.DeleteAsync(new ClaimCheckReference { Id = "cc-delete-fail" }, CancellationToken.None);

		deleted.ShouldBeFalse();
	}

	[Theory]
	[InlineData(255, false)]
	[InlineData(256, true)]
	public void ShouldUseClaimCheck_ShouldRespectThreshold(int payloadSize, bool expected)
	{
		var sut = CreateSut(A.Fake<StorageClient>(), claimCheckOptions: new ClaimCheckOptions
		{
			PayloadThreshold = 256
		});

		var result = sut.ShouldUseClaimCheck(new byte[payloadSize]);

		result.ShouldBe(expected);
	}

	[Fact]
	public void Constructor_ShouldThrowForNullDependencies()
	{
		var storageClient = A.Fake<StorageClient>();
		var options = Microsoft.Extensions.Options.Options.Create(new GcsClaimCheckOptions { BucketName = "bucket" });
		var claimCheckOptions = Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions());
		var logger = CreateEnabledLogger();

		Should.Throw<ArgumentNullException>(() => new GcsClaimCheckStore(null!, options, claimCheckOptions, logger));
		Should.Throw<ArgumentNullException>(() => new GcsClaimCheckStore(storageClient, null!, claimCheckOptions, logger));
		Should.Throw<ArgumentNullException>(() => new GcsClaimCheckStore(storageClient, options, null!, logger));
		Should.Throw<ArgumentNullException>(() => new GcsClaimCheckStore(storageClient, options, claimCheckOptions, null!));
	}

	[Fact]
	public void PrimaryConstructor_ShouldThrowForNullDependencies_BeforeClientCreation()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new GcsClaimCheckOptions
		{
			BucketName = "bucket"
		});
		var claimCheckOptions = Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions());
		var logger = CreateEnabledLogger();

		Should.Throw<ArgumentNullException>(() => new GcsClaimCheckStore(null!, claimCheckOptions, logger));
		Should.Throw<ArgumentNullException>(() => new GcsClaimCheckStore(options, null!, logger));
		Should.Throw<ArgumentNullException>(() => new GcsClaimCheckStore(options, claimCheckOptions, null!));
	}

	[Fact]
	public void PrimaryConstructor_ShouldCreateClient_WhenApplicationCredentialsAreProvided()
	{
		var previousCredentialsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
		var credentialsPath = Path.Combine(Path.GetTempPath(), $"gcs-adc-{Guid.NewGuid():N}.json");

		try
		{
			using var rsa = RSA.Create(2048);
			var privateKey = rsa.ExportPkcs8PrivateKeyPem().Replace("\r\n", "\n", StringComparison.Ordinal);
			var escapedPrivateKey = privateKey.Replace("\n", "\\n", StringComparison.Ordinal);

			var credentialsJson = $$"""
{
  "type": "service_account",
  "project_id": "excalibur-test",
  "private_key_id": "test-key-id",
  "private_key": "{{escapedPrivateKey}}",
  "client_email": "dispatch-test@excalibur-test.iam.gserviceaccount.com",
  "client_id": "1234567890",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token",
  "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
  "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/dispatch-test%40excalibur-test.iam.gserviceaccount.com"
}
""";

			File.WriteAllText(credentialsPath, credentialsJson);
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);

			var sut = new GcsClaimCheckStore(
				Microsoft.Extensions.Options.Options.Create(new GcsClaimCheckOptions
				{
					BucketName = "test-bucket",
					Prefix = "claim-check/",
				}),
				Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions { PayloadThreshold = 8 }),
				CreateEnabledLogger());

			sut.ShouldUseClaimCheck(new byte[8]).ShouldBeTrue();
		}
		finally
		{
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", previousCredentialsPath);
			if (File.Exists(credentialsPath))
			{
				File.Delete(credentialsPath);
			}
		}
	}

	[Fact]
	public async Task Methods_ShouldValidateNullArguments()
	{
		var sut = CreateSut(A.Fake<StorageClient>());

		_ = await Should.ThrowAsync<ArgumentNullException>(() => sut.StoreAsync(null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() => sut.RetrieveAsync(null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() => sut.DeleteAsync(null!, CancellationToken.None));
		Should.Throw<ArgumentNullException>(() => sut.ShouldUseClaimCheck(null!));
	}

	private static GcsClaimCheckStore CreateSut(
		StorageClient storageClient,
		GcsClaimCheckOptions? options = null,
		ClaimCheckOptions? claimCheckOptions = null)
	{
		return new GcsClaimCheckStore(
			storageClient,
			Microsoft.Extensions.Options.Options.Create(options ?? new GcsClaimCheckOptions
			{
				BucketName = "test-bucket",
				Prefix = "claim-check/"
			}),
			Microsoft.Extensions.Options.Options.Create(claimCheckOptions ?? new ClaimCheckOptions
			{
				IdPrefix = "cc-",
				PayloadThreshold = 256,
				RetentionPeriod = TimeSpan.FromHours(4)
			}),
			CreateEnabledLogger());
	}

	private static Google.GoogleApiException CreateGoogleApiException(HttpStatusCode statusCode)
	{
		var exception = new Google.GoogleApiException("storage", "not found");
		typeof(Google.GoogleApiException)
			.GetProperty(nameof(Google.GoogleApiException.HttpStatusCode))?
			.SetValue(exception, statusCode);
		return exception;
	}

	private static ILogger<GcsClaimCheckStore> CreateEnabledLogger()
	{
		var logger = A.Fake<ILogger<GcsClaimCheckStore>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		return logger;
	}
}
