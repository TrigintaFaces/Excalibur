// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Functional tests for <see cref="DefaultCacheKeyBuilder"/> verifying
/// cache key generation, ICacheable detection, and SHA256 hashing.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultCacheKeyBuilderFunctionalShould
{
	private sealed class TestCacheableAction : ICacheable<string>
	{
		public string Id { get; init; } = "test";

		public string GetCacheKey() => $"cacheable:{Id}";
	}

	private sealed class NonCacheableAction : IDispatchAction<int>;

	[Fact]
	public void Create_consistent_key_for_same_input()
	{
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"Id\":\"test\"}");
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("tenant-1");
		A.CallTo(() => context.UserId).Returns("user-1");

		var action = new NonCacheableAction();
		var key1 = builder.CreateKey(action, context);
		var key2 = builder.CreateKey(action, context);

		key1.ShouldBe(key2);
	}

	[Fact]
	public void Create_different_keys_for_different_tenants()
	{
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"Id\":\"test\"}");
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context1 = A.Fake<IMessageContext>();
		A.CallTo(() => context1.TenantId).Returns("tenant-a");
		A.CallTo(() => context1.UserId).Returns("user-1");

		var context2 = A.Fake<IMessageContext>();
		A.CallTo(() => context2.TenantId).Returns("tenant-b");
		A.CallTo(() => context2.UserId).Returns("user-1");

		var action = new NonCacheableAction();
		var key1 = builder.CreateKey(action, context1);
		var key2 = builder.CreateKey(action, context2);

		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void Create_different_keys_for_different_users()
	{
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"Id\":\"test\"}");
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context1 = A.Fake<IMessageContext>();
		A.CallTo(() => context1.TenantId).Returns("tenant-1");
		A.CallTo(() => context1.UserId).Returns("user-a");

		var context2 = A.Fake<IMessageContext>();
		A.CallTo(() => context2.TenantId).Returns("tenant-1");
		A.CallTo(() => context2.UserId).Returns("user-b");

		var action = new NonCacheableAction();
		var key1 = builder.CreateKey(action, context1);
		var key2 = builder.CreateKey(action, context2);

		key1.ShouldNotBe(key2);
	}

	[Fact]
	public void Use_global_for_null_tenant()
	{
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{}");
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns((string?)null);
		A.CallTo(() => context.UserId).Returns("user-1");

		var action = new NonCacheableAction();
		// Should not throw - uses "global" for null tenant
		var key = builder.CreateKey(action, context);
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Use_anonymous_for_null_user()
	{
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{}");
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("tenant-1");
		A.CallTo(() => context.UserId).Returns((string?)null);

		var action = new NonCacheableAction();
		// Should not throw - uses "anonymous" for null user
		var key = builder.CreateKey(action, context);
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Use_cacheable_interface_for_key_when_available()
	{
		var serializer = A.Fake<IJsonSerializer>();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("t1");
		A.CallTo(() => context.UserId).Returns("u1");

		var action = new TestCacheableAction { Id = "product-42" };
		var key = builder.CreateKey(action, context);

		key.ShouldNotBeNullOrWhiteSpace();
		// The key uses ICacheable.GetCacheKey(), so serializer should NOT be called
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).MustNotHaveHappened();
	}

	[Fact]
	public void Produce_url_safe_hashed_keys()
	{
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"data\":\"value\"}");
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("t");
		A.CallTo(() => context.UserId).Returns("u");

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
		var serializer = A.Fake<IJsonSerializer>();
		var builder = new DefaultCacheKeyBuilder(serializer);
		var context = A.Fake<IMessageContext>();

		Should.Throw<ArgumentNullException>(() => builder.CreateKey(null!, context));
	}

	[Fact]
	public void Throw_for_null_context()
	{
		var serializer = A.Fake<IJsonSerializer>();
		var builder = new DefaultCacheKeyBuilder(serializer);

		var action = new NonCacheableAction();
		Should.Throw<ArgumentNullException>(() => builder.CreateKey(action, null!));
	}

	[Fact]
	public void Generate_deterministic_hash()
	{
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"Id\":\"deterministic\"}");
		var builder = new DefaultCacheKeyBuilder(serializer);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("t1");
		A.CallTo(() => context.UserId).Returns("u1");

		var action = new NonCacheableAction();
		var key1 = builder.CreateKey(action, context);

		// Create a new builder instance - should produce the same key
		var builder2 = new DefaultCacheKeyBuilder(serializer);
		var key2 = builder2.CreateKey(action, context);

		key1.ShouldBe(key2);
	}
}
