// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.AwsS3;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "EventSourcing")]
public sealed class AwsS3ColdArchiveMigrationStorageShould
{
	private static readonly Uri Endpoint = new("https://s3.us-east-1.amazonaws.com");
	private static readonly byte[] Body = [1, 2, 3];
	private readonly IAmazonS3 _client = A.Fake<IAmazonS3>();

	[Fact]
	public void ExplainMissingEndpointIdentityWithoutGuessing()
	{
		A.CallTo(() => _client.DetermineServiceOperationEndpoint(A<Amazon.Runtime.AmazonWebServiceRequest>._))
			.Returns(null!);
		var adapter = new AwsS3ColdArchiveMigrationStorage(_client, "bucket", "");
		var error = Should.Throw<NotSupportedException>(() => _ = adapter.NamespaceId);
		error.Message.ShouldContain("fixed bucket endpoint");
		error.InnerException.ShouldBeOfType<NotSupportedException>();
	}

	[Fact]
	public void PreserveUnsupportedResolverCause()
	{
		var cause = new NotSupportedException("custom client");
		A.CallTo(() => _client.DetermineServiceOperationEndpoint(A<Amazon.Runtime.AmazonWebServiceRequest>._)).Throws(cause);
		var adapter = new AwsS3ColdArchiveMigrationStorage(_client, "bucket", "");
		Should.Throw<NotSupportedException>(() => _ = adapter.NamespaceId).InnerException.ShouldBeSameAs(cause);
	}

	[Theory]
	[InlineData("layout-v1.json")]
	[InlineData("receipt")]
	public async Task GuardLegacyWithoutRequiringEndpointDiscovery(string marker)
	{
		var tenant = KeyedTenantPartition.Scoped("tenant");
		A.CallTo(() => _client.DetermineServiceOperationEndpoint(A<Amazon.Runtime.AmazonWebServiceRequest>._))
			.Throws(new NotSupportedException("Supplied client does not expose endpoint routing."));
		A.CallTo(() => _client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._))
			.ThrowsAsync(Error(HttpStatusCode.NotFound, "NoSuchKey"));
		A.CallTo(() => _client.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._))
			.Returns(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
		var legacy = new AwsS3ColdEventStore(_client, "bucket", "", NullLogger<AwsS3ColdEventStore>.Instance);
		(await legacy.ReadAsync(tenant, "aggregate", "Order", CancellationToken.None)).ShouldBeEmpty();
		(await legacy.WriteAsync(tenant, "aggregate", "Order", [], CancellationToken.None)).ShouldBe(-1);
		var item = new StoredEvent("event", "aggregate", "Order", "Created", [1], null, 0, DateTimeOffset.UnixEpoch);
		(await legacy.WriteAsync(tenant, "aggregate", "Order", [item], CancellationToken.None)).ShouldBe(0);
		A.CallTo(() => _client.DetermineServiceOperationEndpoint(A<Amazon.Runtime.AmazonWebServiceRequest>._)).MustNotHaveHappened();

		var adapter = new AwsS3ColdArchiveMigrationStorage(_client, "bucket", "");
		var key = marker == "receipt" ? adapter.GetReceiptKey(tenant, "aggregate") : marker;
		A.CallTo(() => _client.GetObjectAsync(A<GetObjectRequest>.That.Matches(r => r.Key == key), A<CancellationToken>._))
			.ReturnsLazily(() => Response());
		await Should.ThrowAsync<InvalidOperationException>(() => legacy.ReadAsync(tenant, "aggregate", "Order", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => legacy.WriteAsync(tenant, "aggregate", "Order", [], CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => legacy.WriteAsync(tenant, "aggregate", "Order", [item], CancellationToken.None));
		A.CallTo(() => _client.DetermineServiceOperationEndpoint(A<Amazon.Runtime.AmazonWebServiceRequest>._)).MustNotHaveHappened();

		var typed = new AwsS3ColdEventStore(_client, "bucket", "", NullLogger<AwsS3ColdEventStore>.Instance, ColdArchiveLayout.TypedV2);
		await Should.ThrowAsync<NotSupportedException>(() => typed.WriteAsync(tenant, "aggregate", "Order", [item], CancellationToken.None));
		await Should.ThrowAsync<NotSupportedException>(() => legacy.ActivateTypedLayoutAsync(CancellationToken.None));
		await Should.ThrowAsync<NotSupportedException>(() => legacy.MigrateAsync(tenant, "aggregate", "Order", CancellationToken.None));
		A.CallTo(() => _client.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void BindToTheCapturedSdkClientEndpoint()
	{
		using var client = new AmazonS3Client(new Amazon.Runtime.AnonymousAWSCredentials(),
			new AmazonS3Config { ServiceURL = "http://localhost:4566", ForcePathStyle = true });
		var resolved = new AwsS3ColdArchiveMigrationStorage(client, "archive", "exact-prefix/");
		var explicitBinding = new AwsS3ColdArchiveMigrationStorage(client, new Uri("http://localhost:4566/archive/"), "archive", "exact-prefix/");
		resolved.NamespaceId.ShouldBe(explicitBinding.NamespaceId);
		resolved.NamespaceId.ShouldNotBe(new AwsS3ColdArchiveMigrationStorage(client, "other-bucket", "exact-prefix/").NamespaceId);
		resolved.NamespaceId.ShouldNotBe(new AwsS3ColdArchiveMigrationStorage(client, "archive", "exact-prefix").NamespaceId);
	}

	[Theory]
	[InlineData("arn:aws:s3::123456789012:accesspoint/example.mrap")]
	[InlineData("example.mrap")]
	public void RejectRoutingThatDoesNotIdentifyOneDirectBucket(string bucket)
	{
		Should.Throw<ArgumentException>(() => _ = new AwsS3ColdArchiveMigrationStorage(_client, bucket, "").NamespaceId);
		A.CallTo(() => _client.DetermineServiceOperationEndpoint(A<Amazon.Runtime.AmazonWebServiceRequest>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(HttpStatusCode.NotFound, "NoSuchBucket")]
	[InlineData(HttpStatusCode.Forbidden, "AccessDenied")]
	[InlineData(HttpStatusCode.NotFound, "Unknown")]
	public async Task PropagateReadFailures(HttpStatusCode status, string code)
	{
		var error = Error(status, code);
		A.CallTo(() => _client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._)).ThrowsAsync(error);
		(await Should.ThrowAsync<AmazonS3Exception>(() => Store().ReadAsync("key", CancellationToken.None))).ShouldBeSameAs(error);
	}

	[Fact]
	public async Task RecognizeOnlyMissingObjectAsAbsent()
	{
		A.CallTo(() => _client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._))
			.ThrowsAsync(Error(HttpStatusCode.NotFound, "NoSuchKey"));
		(await Store().ReadAsync("key", CancellationToken.None)).ShouldBeNull();
	}

	[Theory]
	[InlineData("truncated")]
	[InlineData("partial")]
	[InlineData("range")]
	[InlineData("revision")]
	[InlineData("deleted")]
	public async Task RejectIncompleteOrDeletedResponse(string defect)
	{
		using var response = Response();
		switch (defect)
		{
			case "truncated": response.ContentLength = Body.Length + 1; break;
			case "partial": response.HttpStatusCode = HttpStatusCode.PartialContent; break;
			case "range": response.ContentRange = "bytes 0-2/10"; break;
			case "revision": response.ETag = ""; break;
			case "deleted": response.DeleteMarker = "true"; break;
		}

		A.CallTo(() => _client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._)).Returns(response);
		await Should.ThrowAsync<InvalidDataException>(() => Store().ReadAsync("key", CancellationToken.None));
	}

	[Fact]
	public async Task ReturnExactBytesAndRevisionFromOneRead()
	{
		using var response = Response();
		var responseStream = response.ResponseStream;
		A.CallTo(() => _client.GetObjectAsync(A<GetObjectRequest>._, A<CancellationToken>._)).Returns(response);
		var result = await Store().ReadAsync("key", CancellationToken.None);
		result.ShouldNotBeNull().CopyContent().ShouldBe(Body);
		result.Revision.ShouldBe("\"revision\"");
		A.CallTo(() => _client.GetObjectAsync(A<GetObjectRequest>.That.Matches(r => r.BucketName == "bucket" && r.Key == "key"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		responseStream.CanRead.ShouldBeFalse();
		response.ResponseStream.ShouldBeNull();
	}

	[Theory]
	[InlineData(HttpStatusCode.Conflict, "ConditionalRequestConflict")]
	[InlineData(HttpStatusCode.Forbidden, "AccessDenied")]
	[InlineData(HttpStatusCode.PreconditionFailed, "Unknown")]
	public async Task NotTreatOtherCreateFailuresAsExistingObjects(HttpStatusCode status, string code)
	{
		var error = Error(status, code);
		A.CallTo(() => _client.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._)).ThrowsAsync(error);
		(await Should.ThrowAsync<AmazonS3Exception>(() => Store().TryCreateAsync("key", Body, CancellationToken.None))).ShouldBeSameAs(error);
	}

	[Fact]
	public async Task ReturnFalseForFailedCreatePrecondition()
	{
		A.CallTo(() => _client.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._))
			.ThrowsAsync(Error(HttpStatusCode.PreconditionFailed, "PreconditionFailed"));
		(await Store().TryCreateAsync("key", Body, CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task SendExactBytesWithCreateOnlyCondition()
	{
		byte[]? captured = null;
		A.CallTo(() => _client.PutObjectAsync(A<PutObjectRequest>.That.Matches(r => r.IfNoneMatch == "*" && r.Key == "key" && r.BucketName == "bucket"), A<CancellationToken>._))
			.Invokes((PutObjectRequest request, CancellationToken _) =>
			{
				using var bytes = new MemoryStream();
				request.InputStream.CopyTo(bytes);
				captured = bytes.ToArray();
			})
			.Returns(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
		(await Store().TryCreateAsync("key", Body, CancellationToken.None)).ShouldBeTrue();
		captured.ShouldBe(Body);
	}

	[Fact]
	public void PreservePrefixAndDistinguishNamespacesAndTypes()
	{
		var tenant = KeyedTenantPartition.Scoped("tenant");
		var store = Store("prefix/");
		store.GetLegacyKey(tenant, "id").ShouldBe($"prefix//{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment("id")}/events.json.gz");
		store.NamespaceId.ShouldNotBe(Store("prefix").NamespaceId);
		store.GetTypedKey(tenant, "id", "A").ShouldNotBe(store.GetTypedKey(tenant, "id", "B"));
		store.GetReceiptKey(tenant, "id").ShouldNotBe(store.GetLegacyKey(tenant, "id"));
		Should.Throw<ArgumentException>(() => Store(new string('\u00e9', 512)).GetLegacyKey(tenant, "id"));
	}

	private AwsS3ColdArchiveMigrationStorage Store(string prefix = "prefix") => new(_client, Endpoint, "bucket", prefix);
	private static AmazonS3Exception Error(HttpStatusCode status, string code) => new("test") { StatusCode = status, ErrorCode = code };
	private static GetObjectResponse Response() => new()
	{
		HttpStatusCode = HttpStatusCode.OK, ContentLength = Body.Length, ETag = "\"revision\"", ResponseStream = new MemoryStream(Body),
	};
}
