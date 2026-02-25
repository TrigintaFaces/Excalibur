// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis.Outbox;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RedisOutboxStoreShould
{
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		var logger = NullLogger<RedisOutboxStore>.Instance;

		Should.Throw<ArgumentNullException>(
			() => new RedisOutboxStore(null!, logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});

		Should.Throw<ArgumentNullException>(
			() => new RedisOutboxStore(options, null!));
	}

	[Fact]
	public void ThrowWhenConnectionIsNullInOverload()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;

		Should.Throw<ArgumentNullException>(
			() => new RedisOutboxStore(null!, options, logger));
	}

	[Fact]
	public async Task StageMessageAsync_ThrowsForNullMessage()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;
		await using var store = new RedisOutboxStore(options, logger);

		await Should.ThrowAsync<ArgumentNullException>(
			() => store.StageMessageAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task EnqueueAsync_ThrowsForNullMessage()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;
		await using var store = new RedisOutboxStore(options, logger);

		await Should.ThrowAsync<ArgumentNullException>(
			() => store.EnqueueAsync(null!, A.Fake<Excalibur.Dispatch.Abstractions.IMessageContext>(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetUnsentMessagesAsync_ThrowsForInvalidBatchSize()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;
		await using var store = new RedisOutboxStore(options, logger);

		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => store.GetUnsentMessagesAsync(0, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkSentAsync_ThrowsForNullMessageId()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;
		await using var store = new RedisOutboxStore(options, logger);

		await Should.ThrowAsync<ArgumentException>(
			() => store.MarkSentAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkFailedAsync_ThrowsForNullMessageId()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;
		await using var store = new RedisOutboxStore(options, logger);

		await Should.ThrowAsync<ArgumentException>(
			() => store.MarkFailedAsync(null!, "error", 1, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DisposeAsync_DoesNotThrow()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;
		var store = new RedisOutboxStore(options, logger);

		await Should.NotThrowAsync(() => store.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = "localhost:6379"
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;
		var store = new RedisOutboxStore(options, logger);

		await store.DisposeAsync();
		await Should.NotThrowAsync(() => store.DisposeAsync().AsTask());
	}
}
