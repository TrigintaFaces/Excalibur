using System.Net;

using Amazon.S3;
using Amazon.S3.Model;

using Excalibur.Dispatch.ClaimCheck.AwsS3;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.ClaimCheck.AwsS3.Tests;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Patterns)]
public sealed class AwsS3ClaimCheckStoreShould : UnitTestBase
{
	[Fact]
	public async Task StoreAsync_ShouldPersistPayloadAndReturnReference()
	{
		var s3Client = A.Fake<IAmazonS3>();
		PutObjectRequest? capturedRequest = null;
		A.CallTo(() => s3Client.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._))
			.Invokes((PutObjectRequest request, CancellationToken _) => capturedRequest = request)
			.Returns(new PutObjectResponse());

		var sut = CreateSut(s3Client);
		var payload = new byte[] { 1, 2, 3, 4, 5 };
		var metadata = new ClaimCheckMetadata { ContentType = "application/json" };

		var reference = await sut.StoreAsync(payload, CancellationToken.None, metadata);

		reference.Id.ShouldStartWith("cc-");
		reference.BlobName.ShouldContain(reference.Id);
		reference.Location.ShouldStartWith("s3://test-bucket/claim-check/");
		reference.Size.ShouldBe(payload.Length);
		reference.Metadata.ShouldNotBeNull();
		reference.Metadata.ContentType.ShouldBe("application/json");
		reference.ExpiresAt.HasValue.ShouldBeTrue();
		reference.ExpiresAt.Value.ShouldBeGreaterThan(reference.StoredAt);

		capturedRequest.ShouldNotBeNull();
		capturedRequest.BucketName.ShouldBe("test-bucket");
		capturedRequest.Key.ShouldContain(reference.Id);
		capturedRequest.ContentType.ShouldBe("application/json");
		capturedRequest.Metadata["claim-check-id"].ShouldBe(reference.Id);
		capturedRequest.Metadata["original-size"].ShouldBe(payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	[Fact]
	public async Task StoreAsync_WithNullMetadata_ShouldUseDefaultContentType()
	{
		var s3Client = A.Fake<IAmazonS3>();
		PutObjectRequest? capturedRequest = null;
		A.CallTo(() => s3Client.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._))
			.Invokes((PutObjectRequest request, CancellationToken _) => capturedRequest = request)
			.Returns(new PutObjectResponse());

		var sut = CreateSut(s3Client);

		_ = await sut.StoreAsync([0x10, 0x20], CancellationToken.None);

		capturedRequest.ShouldNotBeNull();
		capturedRequest.ContentType.ShouldBe("application/octet-stream");
	}

	[Fact]
	public async Task RetrieveAsync_ShouldReturnPayloadFromS3()
	{
		var s3Client = A.Fake<IAmazonS3>();
		var expected = new byte[] { 9, 8, 7, 6 };
		var response = new GetObjectResponse
		{
			ResponseStream = new MemoryStream(expected)
		};

		A.CallTo(() => s3Client.GetObjectAsync("test-bucket", A<string>._, A<CancellationToken>._))
			.Returns(response);

		var sut = CreateSut(s3Client);
		var reference = new ClaimCheckReference { Id = "cc-retrieve" };

		var result = await sut.RetrieveAsync(reference, CancellationToken.None);

		result.ShouldBe(expected);
	}

	[Fact]
	public async Task RetrieveAsync_WhenObjectMissing_ShouldThrowKeyNotFoundException()
	{
		var s3Client = A.Fake<IAmazonS3>();
		A.CallTo(() => s3Client.GetObjectAsync("test-bucket", A<string>._, A<CancellationToken>._))
			.Throws(new AmazonS3Exception("missing")
			{
				StatusCode = HttpStatusCode.NotFound
			});

		var sut = CreateSut(s3Client);
		var reference = new ClaimCheckReference { Id = "cc-missing" };

		var ex = await Should.ThrowAsync<KeyNotFoundException>(() => sut.RetrieveAsync(reference, CancellationToken.None));
		ex.Message.ShouldContain("cc-missing");
	}

	[Fact]
	public async Task DeleteAsync_WhenDeleteSucceeds_ShouldReturnTrue()
	{
		var s3Client = A.Fake<IAmazonS3>();
		A.CallTo(() => s3Client.DeleteObjectAsync("test-bucket", A<string>._, A<CancellationToken>._))
			.Returns(new DeleteObjectResponse());

		var sut = CreateSut(s3Client);
		var reference = new ClaimCheckReference { Id = "cc-delete" };

		var deleted = await sut.DeleteAsync(reference, CancellationToken.None);

		deleted.ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteAsync_WhenS3Throws_ShouldReturnFalse()
	{
		var s3Client = A.Fake<IAmazonS3>();
		A.CallTo(() => s3Client.DeleteObjectAsync("test-bucket", A<string>._, A<CancellationToken>._))
			.Throws(new AmazonS3Exception("boom"));

		var sut = CreateSut(s3Client);
		var reference = new ClaimCheckReference { Id = "cc-delete-fail" };

		var deleted = await sut.DeleteAsync(reference, CancellationToken.None);

		deleted.ShouldBeFalse();
	}

	[Theory]
	[InlineData(127, false)]
	[InlineData(128, true)]
	[InlineData(1024, true)]
	public void ShouldUseClaimCheck_ShouldRespectPayloadThreshold(int payloadSize, bool expected)
	{
		var sut = CreateSut(A.Fake<IAmazonS3>(), claimCheckOptions: new ClaimCheckOptions
		{
			PayloadThreshold = 128
		});

		var result = sut.ShouldUseClaimCheck(new byte[payloadSize]);

		result.ShouldBe(expected);
	}

	[Fact]
	public void Dispose_WhenClientInjected_ShouldNotDisposeInjectedClient()
	{
		var s3Client = A.Fake<IAmazonS3>();
		var sut = CreateSut(s3Client);

		sut.Dispose();
		sut.Dispose();

		A.CallTo(() => s3Client.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public void Constructor_ShouldThrowForNullDependencies()
	{
		var s3Client = A.Fake<IAmazonS3>();
		var options = Microsoft.Extensions.Options.Options.Create(new AwsS3ClaimCheckOptions { BucketName = "bucket" });
		var claimCheckOptions = Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions());
		var logger = CreateEnabledLogger();

		Should.Throw<ArgumentNullException>(() => new AwsS3ClaimCheckStore(null!, options, claimCheckOptions, logger));
		Should.Throw<ArgumentNullException>(() => new AwsS3ClaimCheckStore(s3Client, null!, claimCheckOptions, logger));
		Should.Throw<ArgumentNullException>(() => new AwsS3ClaimCheckStore(s3Client, options, null!, logger));
		Should.Throw<ArgumentNullException>(() => new AwsS3ClaimCheckStore(s3Client, options, claimCheckOptions, null!));
	}

	[Fact]
	public void PrimaryConstructor_ShouldThrowForNullDependencies()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new AwsS3ClaimCheckOptions
		{
			BucketName = "bucket",
			AccessKey = "test-access",
			SecretKey = "test-secret",
		});
		var claimCheckOptions = Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions());
		var logger = CreateEnabledLogger();

		Should.Throw<ArgumentNullException>(() => new AwsS3ClaimCheckStore(null!, claimCheckOptions, logger));
		Should.Throw<ArgumentNullException>(() => new AwsS3ClaimCheckStore(options, null!, logger));
		Should.Throw<ArgumentNullException>(() => new AwsS3ClaimCheckStore(options, claimCheckOptions, null!));
	}

	[Fact]
	public void PrimaryConstructor_ShouldCreateClient_WhenServiceUrlConfigured()
	{
		var sut = new AwsS3ClaimCheckStore(
			Microsoft.Extensions.Options.Options.Create(new AwsS3ClaimCheckOptions
			{
				BucketName = "bucket",
				Prefix = "claim-check/",
				ServiceUrl = "http://localhost:4566",
				AccessKey = "test-access",
				SecretKey = "test-secret",
			}),
			Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions { PayloadThreshold = 16 }),
			CreateEnabledLogger());

		sut.ShouldUseClaimCheck(new byte[32]).ShouldBeTrue();
		sut.Dispose();
	}

	[Fact]
	public void PrimaryConstructor_ShouldCreateClient_WhenRegionConfigured()
	{
		var sut = new AwsS3ClaimCheckStore(
			Microsoft.Extensions.Options.Options.Create(new AwsS3ClaimCheckOptions
			{
				BucketName = "bucket",
				Prefix = "claim-check/",
				Region = "us-east-1",
				AccessKey = "test-access",
				SecretKey = "test-secret",
			}),
			Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions { PayloadThreshold = 16 }),
			CreateEnabledLogger());

		sut.ShouldUseClaimCheck(new byte[4]).ShouldBeFalse();
		sut.Dispose();
	}

	[Fact]
	public void PrimaryConstructor_ShouldCreateClient_WithoutExplicitCredentials()
	{
		var sut = new AwsS3ClaimCheckStore(
			Microsoft.Extensions.Options.Options.Create(new AwsS3ClaimCheckOptions
			{
				BucketName = "bucket",
				Prefix = "claim-check/",
				Region = "us-east-1",
			}),
			Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions { PayloadThreshold = 12 }),
			CreateEnabledLogger());

		sut.ShouldUseClaimCheck(new byte[11]).ShouldBeFalse();
		sut.Dispose();
	}

	[Fact]
	public async Task Methods_ShouldValidateNullArguments()
	{
		var sut = CreateSut(A.Fake<IAmazonS3>());

		_ = await Should.ThrowAsync<ArgumentNullException>(() => sut.StoreAsync(null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() => sut.RetrieveAsync(null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() => sut.DeleteAsync(null!, CancellationToken.None));
		Should.Throw<ArgumentNullException>(() => sut.ShouldUseClaimCheck(null!));
	}

	private static AwsS3ClaimCheckStore CreateSut(
		IAmazonS3 s3Client,
		AwsS3ClaimCheckOptions? options = null,
		ClaimCheckOptions? claimCheckOptions = null)
	{
		return new AwsS3ClaimCheckStore(
			s3Client,
			Microsoft.Extensions.Options.Options.Create(options ?? new AwsS3ClaimCheckOptions
			{
				BucketName = "test-bucket",
				Prefix = "claim-check/"
			}),
			Microsoft.Extensions.Options.Options.Create(claimCheckOptions ?? new ClaimCheckOptions
			{
				IdPrefix = "cc-",
				PayloadThreshold = 128,
				RetentionPeriod = TimeSpan.FromHours(4)
			}),
			CreateEnabledLogger());
	}

	private static ILogger<AwsS3ClaimCheckStore> CreateEnabledLogger()
	{
		var logger = A.Fake<ILogger<AwsS3ClaimCheckStore>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		return logger;
	}
}
