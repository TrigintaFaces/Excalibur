// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis.Inbox;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RedisInboxStoreShould
{
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		var logger = NullLogger<RedisInboxStore>.Instance;

		Should.Throw<ArgumentNullException>(
			() => new RedisInboxStore(null!, logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});

		Should.Throw<ArgumentNullException>(
			() => new RedisInboxStore(options, null!));
	}

	[Fact]
	public void ThrowWhenConnectionIsNullInOverload()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;

		Should.Throw<ArgumentNullException>(
			() => new RedisInboxStore(null!, options, logger));
	}

	[Fact]
	public void ThrowWhenOptionsIsNullInOverload()
	{
		var logger = NullLogger<RedisInboxStore>.Instance;

		// Cannot test the connection overload without a real ConnectionMultiplexer,
		// but we can verify the options-only constructor validates
		Should.Throw<ArgumentNullException>(
			() => new RedisInboxStore(null!, logger));
	}

	[Fact]
	public async Task CreateEntryAsync_ThrowsForNullMessageId()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;
		await using var store = new RedisInboxStore(options, logger);

		await Should.ThrowAsync<ArgumentException>(
			() => store.CreateEntryAsync(null!, "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task CreateEntryAsync_ThrowsForEmptyHandlerType()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;
		await using var store = new RedisInboxStore(options, logger);

		await Should.ThrowAsync<ArgumentException>(
			() => store.CreateEntryAsync("msg-1", string.Empty, "type", [], new Dictionary<string, object>(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkProcessedAsync_ThrowsForNullMessageId()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;
		await using var store = new RedisInboxStore(options, logger);

		await Should.ThrowAsync<ArgumentException>(
			() => store.MarkProcessedAsync(null!, "handler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task IsProcessedAsync_ThrowsForNullMessageId()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;
		await using var store = new RedisInboxStore(options, logger);

		await Should.ThrowAsync<ArgumentException>(
			() => store.IsProcessedAsync(null!, "handler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetEntryAsync_ThrowsForNullMessageId()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;
		await using var store = new RedisInboxStore(options, logger);

		await Should.ThrowAsync<ArgumentException>(
			() => store.GetEntryAsync(null!, "handler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkFailedAsync_ThrowsForNullMessageId()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;
		await using var store = new RedisInboxStore(options, logger);

		await Should.ThrowAsync<ArgumentException>(
			() => store.MarkFailedAsync(null!, "handler", "error", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DisposeAsync_DoesNotThrow()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;
		var store = new RedisInboxStore(options, logger);

		await Should.NotThrowAsync(() => store.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisInboxStore>.Instance;
		var store = new RedisInboxStore(options, logger);

		await store.DisposeAsync();
		await Should.NotThrowAsync(() => store.DisposeAsync().AsTask());
	}
}
