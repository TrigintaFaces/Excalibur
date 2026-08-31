// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Binds the change feed to the Streams client it actually needs.
/// </summary>
/// <remarks>
/// <para>
/// A provider built from a consumer-supplied document client has no Streams client, and previously
/// reported itself initialized regardless. The safety arm proves the change feed now refuses in terms
/// the caller can act on rather than dereferencing a field it knows is null; the liveness arm proves a
/// provider given a Streams client gets past that refusal and into the real subscription path -- which
/// a provider that simply always refused would fail.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class DynamoDbChangeFeedStreamsClientShould : UnitTestBase
{
	private sealed class Doc
	{
		public string Id { get; set; } = string.Empty;
	}

	private static IOptions<DynamoDbOptions> ProviderOptions()
	{
		var opts = new DynamoDbOptions();
		opts.Connection.Region = "us-east-1";
		return Options.Create(opts);
	}

	/// <summary>
	/// A document client whose table reports no stream. This lets the subscription's own start-up path
	/// terminate deterministically, without reaching AWS, at a point that is unambiguously past the
	/// Streams-client guard.
	/// </summary>
	private static IAmazonDynamoDB StreamlessTableClient()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		A.CallTo(() => client.DescribeTableAsync(A<string>._, A<CancellationToken>._))
			.Returns(new DescribeTableResponse
			{
				Table = new TableDescription { TableName = "orders", LatestStreamArn = null }
			});
		return client;
	}

	// ---- SAFETY: no Streams client means an actionable refusal, not a null dereference ----

	[Fact]
	public async Task RefuseTheChangeFeed_WhenConstructedWithoutAStreamsClient()
	{
		await using var provider = new DynamoDbPersistenceProvider(
			StreamlessTableClient(), ProviderOptions(), NullLogger<DynamoDbPersistenceProvider>.Instance);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => provider.CreateChangeFeedSubscriptionAsync<Doc>("orders", null, CancellationToken.None));

		// The message has to tell the caller how to fix it, not merely that something was null.
		ex.Message.ShouldContain("Streams");
		ex.Message.ShouldContain("IAmazonDynamoDBStreams");
	}

	[Fact]
	public async Task NotReportAChangeFeedCapabilityItCannotServe()
	{
		await using var provider = new DynamoDbPersistenceProvider(
			StreamlessTableClient(), ProviderOptions(), NullLogger<DynamoDbPersistenceProvider>.Instance);

		// The provider is genuinely usable for documents -- the refusal is scoped to the change feed.
		provider.IsAvailable.ShouldBeTrue();

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => provider.CreateChangeFeedSubscriptionAsync<Doc>("orders", null, CancellationToken.None));
	}

	// ---- LIVENESS: given a Streams client, the change feed proceeds into the real path ----

	[Fact]
	public async Task AcceptAConsumerSuppliedStreamsClient_AndProceedPastTheGuard()
	{
		var streams = A.Fake<IAmazonDynamoDBStreams>();

		await using var provider = new DynamoDbPersistenceProvider(
			StreamlessTableClient(), streams, ProviderOptions(), NullLogger<DynamoDbPersistenceProvider>.Instance);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => provider.CreateChangeFeedSubscriptionAsync<Doc>("orders", null, CancellationToken.None));

		// It got past the Streams-client guard and into the subscription, which then stopped for its own
		// reason: this fixture's table has streams disabled. A provider that always refused would report
		// the guard's message here instead.
		ex.Message.ShouldContain("does not have streams enabled");
		ex.Message.ShouldNotContain("IAmazonDynamoDBStreams");
	}

	[Fact]
	public void RejectANullStreamsClient_OnTheStreamsConstructor()
	{
		var ex = Should.Throw<ArgumentNullException>(() =>
			new DynamoDbPersistenceProvider(
				A.Fake<IAmazonDynamoDB>(), null!, ProviderOptions(),
				NullLogger<DynamoDbPersistenceProvider>.Instance));

		ex.ParamName.ShouldBe("streamsClient");
	}
}
