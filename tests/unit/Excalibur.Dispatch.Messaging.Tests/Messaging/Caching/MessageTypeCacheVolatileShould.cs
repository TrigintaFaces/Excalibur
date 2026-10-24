// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Tests atomic cache publication and one-time initialization.
/// </summary>
[Collection("MessageTypeCacheTests")]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MessageTypeCacheVolatileShould : IDisposable
{
	public MessageTypeCacheVolatileShould()
	{
		ResetCache();
	}

	public void Dispose()
	{
		ResetCache();
	}

	private static void ResetCache() => MessageTypeCache.Reset();

	[Fact]
	public async Task PublishMetadataAndNamesTogetherAfterConstruction()
	{
		var constructing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var release = new ManualResetEventSlim();
		IEnumerable<Type> Types()
		{
			yield return typeof(NamedMessage);
			constructing.SetResult();
			release.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
		}

		var initialize = Task.Run(() => MessageTypeCache.Initialize(Types()));
		try
		{
			await constructing.Task.WaitAsync(TimeSpan.FromSeconds(30));
			MessageTypeCache.GetCachedTypes().ShouldBeEmpty();
			MessageTypeCache.ResolveType("atomic.named").ShouldBeNull();
		}
		finally
		{
			release.Set();
			await initialize;
		}
		MessageTypeCache.GetMetadata(typeof(NamedMessage)).ShouldNotBeNull();
		MessageTypeCache.ResolveType("atomic.named").ShouldBe(typeof(NamedMessage));
	}

	[MessageName("atomic.named")]
	private sealed class NamedMessage;

	[Fact]
	public void PreventDoubleInitialization()
	{
		// Arrange
		var before = MessageTypeCache.GetCachedTypes().ToHashSet();

		// Act
		MessageTypeCache.Initialize([typeof(string)]);
		var afterFirst = MessageTypeCache.GetCachedTypes().ToHashSet();

		MessageTypeCache.Initialize([typeof(int)]);
		var afterSecond = MessageTypeCache.GetCachedTypes().ToHashSet();

		// Assert: once initialized, subsequent Initialize calls are no-ops.
		afterSecond.SetEquals(afterFirst).ShouldBeTrue("Second initialization should not mutate the cache");

		if (before.Count == 0)
		{
			afterFirst.Contains(typeof(string)).ShouldBeTrue("First initialization should include provided type");
		}
	}
}
