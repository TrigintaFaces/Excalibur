// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.CloudNative;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.DynamoDb.Tests.Streams;

/// <summary>
/// Regression lock for o9y64z: a stop→start cycle must actually resume. The pre-fix
/// <see cref="DynamoDbStreamsSubscription{TDocument}"/> canceled its single instance-level
/// <c>CancellationTokenSource</c> in <c>StopAsync</c> and never recreated it, so a subsequent
/// <c>StartAsync</c> left the CTS permanently canceled → every new <c>ReadChangesAsync</c> linked a
/// pre-canceled token and yield-broke immediately (silently dead subscription).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class DynamoDbStreamsSubscriptionLifecycleShould
{
	private sealed class Doc
	{
	}

	private static CancellationTokenSource GetCts(object subscription) =>
		(CancellationTokenSource)subscription.GetType()
			.GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance)!
			.GetValue(subscription)!;

	private static DynamoDbStreamsSubscription<Doc> CreateSubscription()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		A.CallTo(() => client.DescribeTableAsync("orders", A<CancellationToken>._))
			.Returns(new DescribeTableResponse
			{
				Table = new TableDescription { LatestStreamArn = "arn:aws:dynamodb:us-east-1:0:table/orders/stream/x" },
			});

		var streamsClient = A.Fake<IAmazonDynamoDBStreams>();
		var options = A.Fake<IChangeFeedOptions>();
		return new DynamoDbStreamsSubscription<Doc>(client, streamsClient, "orders", options, NullLogger.Instance);
	}

	[Fact]
	public async Task Recreate_the_cts_on_restart_so_stop_then_start_resumes()
	{
		var subscription = CreateSubscription();

		await subscription.StartAsync(CancellationToken.None);
		GetCts(subscription).IsCancellationRequested.ShouldBeFalse();
		subscription.IsActive.ShouldBeTrue();

		await subscription.StopAsync(CancellationToken.None);
		GetCts(subscription).IsCancellationRequested.ShouldBeTrue(); // stopped → source canceled
		subscription.IsActive.ShouldBeFalse();

		await subscription.StartAsync(CancellationToken.None);

		// Non-vacuity: pre-fix the SAME canceled CTS persisted across restart, so this stayed true (RED).
		GetCts(subscription).IsCancellationRequested.ShouldBeFalse();
		subscription.IsActive.ShouldBeTrue();

		await subscription.DisposeAsync();
	}
}
