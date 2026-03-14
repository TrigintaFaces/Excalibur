// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Functional tests for <see cref="DefaultCacheKeyBuilder"/> verifying
/// cache key generation, ICacheable detection, and SHA256 hashing.
/// Uses concrete action types instead of FakeItEasy proxies because
/// <see cref="DispatchJsonSerializer"/> is a sealed class that performs
/// real serialization -- Castle.Proxies types are not in the source-gen context.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultCacheKeyBuilderFunctionalShould
{
	private sealed class TestCacheableAction : ICacheable<string>
	{
		public string Id { get; init; } = "test";

		public string GetCacheKey() => $"cacheable:{Id}";
	}

	private sealed class NonCacheableAction : IDispatchAction<int>
	{
		public string Data { get; init; } = "default";
	}

	private static IMessageContext CreateFakeContext(string? tenantId, string? userId)
	{
		var context = A.Fake<IMessageContext>();
		var features = new Dictionary<Type, object>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Features).Returns(features);
		A.CallTo(() => context.Items).Returns(items);

		var identity = new MessageIdentityFeature { TenantId = tenantId, UserId = userId };
		features[typeof(IMessageIdentityFeature)] = identity;

		return context;
	}

	[Fact]
	public void Create_consistent_key_for_same_input()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = CreateFakeContext("tenant-1", "user-1");

		var action = new NonCacheableAction();
		var key1 = builder.CreateKey(action, context);
		var key2 = builder.CreateKey(action, context);

		key1.ShouldBe(key2);
	}

	[Fact]
	public void Create_different_keys_for_different_tenants()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context1 = CreateFakeContext("tenant-a", "user-1");
		var context2 = CreateFakeContext("tenant-b", "user-1");

		var action = new NonCacheableAction();
		var key1 = builder.CreateKey(action, context1);
		var key2 = builder.CreateKey(action, context2);

		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void Create_different_keys_for_different_users()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context1 = CreateFakeContext("tenant-1", "user-a");
		var context2 = CreateFakeContext("tenant-1", "user-b");

		var action = new NonCacheableAction();
		var key1 = builder.CreateKey(action, context1);
		var key2 = builder.CreateKey(action, context2);

		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void Use_global_for_null_tenant()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = CreateFakeContext(null, "user-1");

		var action = new NonCacheableAction();
		// Should not throw - uses "global" for null tenant
		var key = builder.CreateKey(action, context);
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Use_anonymous_for_null_user()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = CreateFakeContext("tenant-1", null);

		var action = new NonCacheableAction();
		// Should not throw - uses "anonymous" for null user
		var key = builder.CreateKey(action, context);
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Use_cacheable_interface_for_key_when_available()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = CreateFakeContext("t1", "u1");

		var action = new TestCacheableAction { Id = "product-42" };
		var key = builder.CreateKey(action, context);

		key.ShouldNotBeNullOrWhiteSpace();
		// The ICacheable path is used, so the serializer is not involved in key generation.
		// We verify the key is deterministic by checking consistency.
		var key2 = builder.CreateKey(action, context);
		key.ShouldBe(key2);
	}

	[Fact]
	public void Produce_url_safe_hashed_keys()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = CreateFakeContext("t", "u");

		var action = new NonCacheableAction();
		var key = builder.CreateKey(action, context);

		// SHA256 base64 with url-safe replacements: no =, / replaced with _, + replaced with -
		key.ShouldNotContain("=");
		key.ShouldNotContain("/");
		key.ShouldNotContain("+");
	}

	[Fact]
	public void Throw_for_null_action()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);
		var context = A.Fake<IMessageContext>();

		Should.Throw<ArgumentNullException>(() => builder.CreateKey(null!, context));
	}

	[Fact]
	public void Throw_for_null_context()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var action = new NonCacheableAction();
		Should.Throw<ArgumentNullException>(() => builder.CreateKey(action, null!));
	}

	[Fact]
	public void Generate_deterministic_hash()
	{
		using var serializer = new DispatchJsonSerializer();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = CreateFakeContext("t1", "u1");

		var action = new NonCacheableAction { Data = "deterministic" };
		var key1 = builder.CreateKey(action, context);

		// Create a new builder instance with a fresh serializer - should produce the same key
		using var serializer2 = new DispatchJsonSerializer();
		var builder2 = new DefaultCacheKeyBuilder(serializer2);
		var key2 = builder2.CreateKey(action, context);

		key1.ShouldBe(key2);
	}
}
