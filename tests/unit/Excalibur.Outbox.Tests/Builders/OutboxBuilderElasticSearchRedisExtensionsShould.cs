// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;
using Excalibur.Outbox.ElasticSearch;
using Excalibur.Outbox.InMemory;
using Excalibur.Outbox.Redis;

using Microsoft.Extensions.DependencyInjection;

using StackExchange.Redis;

namespace Excalibur.Outbox.Tests.Builders;

/// <summary>
/// Sprint 637 B.2/B.6: Tests for IOutboxBuilder UseElasticSearch(), UseRedis(), and UseInMemory()
/// provider extension methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBuilderElasticSearchRedisExtensionsShould
{
	#region UseElasticSearch

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseElasticSearch()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IOutboxBuilder)null!).UseElasticSearch(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseElasticSearch()
	{
		var services = new ServiceCollection();
		IOutboxBuilder? capturedBuilder = null;

		services.AddExcaliburOutbox(outbox =>
		{
			capturedBuilder = outbox;
		});

		capturedBuilder.ShouldNotBeNull();
		Should.Throw<ArgumentNullException>(() =>
			capturedBuilder!.UseElasticSearch(null!));
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining_UseElasticSearch()
	{
		var services = new ServiceCollection();
		IOutboxBuilder? capturedResult = null;

		services.AddExcaliburOutbox(outbox =>
		{
			capturedResult = outbox.UseElasticSearch(opts =>
			{
				opts.IndexName = "test-outbox";
			});
		});

		capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterOutboxStore_WhenCallingUseElasticSearch()
	{
		var services = new ServiceCollection();

		services.AddExcaliburOutbox(outbox => outbox.UseElasticSearch(opts =>
		{
			opts.IndexName = "test-outbox";
		}));

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore));
	}

	#endregion

	#region UseRedis (Action<RedisOutboxOptions>)

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseRedis()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IOutboxBuilder)null!).UseRedis(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseRedis()
	{
		var services = new ServiceCollection();
		IOutboxBuilder? capturedBuilder = null;

		services.AddExcaliburOutbox(outbox =>
		{
			capturedBuilder = outbox;
		});

		capturedBuilder.ShouldNotBeNull();
		Should.Throw<ArgumentNullException>(() =>
			capturedBuilder!.UseRedis((Action<RedisOutboxOptions>)null!));
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining_UseRedis()
	{
		var services = new ServiceCollection();
		IOutboxBuilder? capturedResult = null;

		services.AddExcaliburOutbox(outbox =>
		{
			capturedResult = outbox.UseRedis(opts =>
			{
				opts.ConnectionString = "localhost:6379";
			});
		});

		capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterOutboxStore_WhenCallingUseRedis()
	{
		var services = new ServiceCollection();

		services.AddExcaliburOutbox(outbox => outbox.UseRedis(opts =>
		{
			opts.ConnectionString = "localhost:6379";
		}));

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore));
	}

	#endregion

	#region UseInMemory

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseInMemory()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IOutboxBuilder)null!).UseInMemory());
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining_UseInMemory()
	{
		var services = new ServiceCollection();
		IOutboxBuilder? capturedResult = null;

		services.AddExcaliburOutbox(outbox =>
		{
			capturedResult = outbox.UseInMemory();
		});

		capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterOutboxStore_WhenCallingUseInMemory()
	{
		var services = new ServiceCollection();

		services.AddExcaliburOutbox(outbox => outbox.UseInMemory());

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore));
	}

	#endregion
}
