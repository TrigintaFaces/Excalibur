// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class NamedPublisherCleanupShould
{
	[Fact]
	public async Task JoinTheSameShutdownWhenTheSdkReentersDisposal()
	{
		var client = A.Fake<global::Google.Cloud.PubSub.V1.PublisherClient>();
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var adapter = new TopicPublisherClientAdapter(client, ownsClient: true);
		Task? reentrant = null;
		A.CallTo(() => client.DisposeAsync()).Invokes(() =>
		{
			if (reentrant is null)
			{
				reentrant = Task.CompletedTask;
				reentrant = adapter.DisposeAsync().AsTask();
			}
		}).Returns(new ValueTask(release.Task));

		var first = adapter.DisposeAsync().AsTask();
		var second = adapter.DisposeAsync().AsTask();
		try
		{
			first.IsCompleted.ShouldBeFalse();
			second.IsCompleted.ShouldBeFalse();
			reentrant.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
			A.CallTo(() => client.DisposeAsync()).MustHaveHappenedOnceExactly();
		}
		finally
		{
			release.TrySetResult();
			await Task.WhenAll(first, second, reentrant.ShouldNotBeNull()).WaitAsync(TimeSpan.FromSeconds(5));
		}
		await adapter.DisposeAsync();
		A.CallTo(() => client.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LeaveBorrowedSdkClientsToTheirOwner()
	{
		var client = A.Fake<global::Google.Cloud.PubSub.V1.PublisherClient>();
		await using var adapter = new TopicPublisherClientAdapter(client);
		await adapter.DisposeAsync();
		A.CallTo(() => client.DisposeAsync()).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ShareTheActualShutdownFailureOrCancellation(bool cancel)
	{
		var client = A.Fake<global::Google.Cloud.PubSub.V1.PublisherClient>();
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => client.DisposeAsync()).Returns(new ValueTask(release.Task));
		var adapter = new TopicPublisherClientAdapter(client, ownsClient: true);
		var first = adapter.DisposeAsync().AsTask();
		var second = adapter.DisposeAsync().AsTask();
		first.ShouldBeSameAs(second);
		first.IsCompleted.ShouldBeFalse();
		if (cancel)
		{
			release.TrySetCanceled();
			_ = await Should.ThrowAsync<OperationCanceledException>(() => first);
			_ = await Should.ThrowAsync<OperationCanceledException>(() => second);
			first.IsCanceled.ShouldBeTrue();
		}
		else
		{
			var expected = new InvalidOperationException("Shutdown failed");
			release.TrySetException(expected);
			(await Should.ThrowAsync<InvalidOperationException>(() => first)).ShouldBeSameAs(expected);
			(await Should.ThrowAsync<InvalidOperationException>(() => second)).ShouldBeSameAs(expected);
		}
		A.CallTo(() => client.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ShareSynchronousSdkShutdownExceptionsWithoutRetryingShutdown()
	{
		var client = A.Fake<global::Google.Cloud.PubSub.V1.PublisherClient>();
		var expected = new InvalidOperationException("Synchronous shutdown failure");
		A.CallTo(() => client.DisposeAsync()).Throws(expected);
		var adapter = new TopicPublisherClientAdapter(client, ownsClient: true);
		(await Should.ThrowAsync<InvalidOperationException>(() => adapter.DisposeAsync().AsTask())).ShouldBeSameAs(expected);
		(await Should.ThrowAsync<InvalidOperationException>(() => adapter.DisposeAsync().AsTask())).ShouldBeSameAs(expected);
		A.CallTo(() => client.DisposeAsync()).MustHaveHappenedOnceExactly();
	}
}
