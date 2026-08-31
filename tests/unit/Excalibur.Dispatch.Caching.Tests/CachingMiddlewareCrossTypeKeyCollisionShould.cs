// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Regression lock: a cache entry must never be served to an action expecting a different type.
/// </summary>
/// <remarks>
/// <para>
/// On the <see cref="ICacheable{T}"/> path the base key is <c>GetCacheKey()</c> verbatim, with no type
/// component, so two different action types whose key strings match address one entry.
/// <c>$"user:{UserId}"</c> on a profile query and a permissions query is ordinary code.
/// </para>
/// <para>
/// Where the two actions declare different response types the stored value itself gives the collision
/// away. Where they declare the same one — <c>ICacheable&lt;string&gt;</c> on a name query and an email
/// query — it does not, and no inspection of the value can tell a legitimate hit from another action's
/// data. That case is decided by attributing the entry to the action that stored it.
/// </para>
/// <para>
/// The consequence is asymmetric across runtimes, which is what made it dangerous. Where dynamic code is
/// available the middleware builds a generic wrapper by reflection and a mismatched value throws — loud,
/// and therefore survivable. Where it is not, the value is wrapped as an untyped object with no check,
/// and the caller is handed another type's data with no error at all. That path is invisible to any test
/// run under dynamic code, so the whole suite could pass while the shipped ahead-of-time behaviour was
/// wrong.
/// </para>
/// <para>
/// These arms assert observable behaviour only — what the caller receives — so a fix by any mechanism
/// satisfies them.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareCrossTypeKeyCollisionShould : IDisposable
{
	/// <summary>The colliding logical key: two different action types below both return it.</summary>
	private const string SharedLogicalKey = "user:42";

	private readonly ServiceProvider _services;
	private readonly HybridCache _cache;
	private readonly IMeterFactory _meterFactory;
	private readonly DispatchJsonSerializer _serializer = new();
	private readonly DefaultCacheKeyBuilder _keyBuilder;
	private bool _disposed;

	public CachingMiddlewareCrossTypeKeyCollisionShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddHybridCache();
		_services = services.BuildServiceProvider();
		_cache = _services.GetRequiredService<HybridCache>();
		_meterFactory = _services.GetRequiredService<IMeterFactory>();

		// The real key builder and a real cache, so the collision arises the way it would in production
		// rather than being stubbed into existence.
		_keyBuilder = new DefaultCacheKeyBuilder(_serializer);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_meterFactory is IDisposable disposableMeterFactory)
		{
			disposableMeterFactory.Dispose();
		}

		_serializer.Dispose();
		_services.Dispose();
	}

	/// <summary>
	/// SAFETY. The second action receives its own type. Before the fix this dispatch either threw or, on
	/// the ahead-of-time path, returned the first action's value.
	/// </summary>
	[Fact]
	public async Task Serve_its_own_type_to_the_second_action_sharing_a_cache_key()
	{
		var middleware = CreateMiddleware();

		DispatchRequestDelegate profileHandler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success(new ProfileDto("Ada")));

		_ = await middleware.InvokeAsync(new ProfileQuery(), NewContext(), profileHandler, CancellationToken.None);

		var permissionsRan = false;
		DispatchRequestDelegate permissionsHandler = (_, _, _) =>
		{
			permissionsRan = true;
			return new ValueTask<IMessageResult>(MessageResult.Success(new PermissionsDto("admin")));
		};

		var result = await middleware.InvokeAsync(
			new PermissionsQuery(), NewContext(), permissionsHandler, CancellationToken.None);

		permissionsRan.ShouldBeTrue(
			"the entry under this key holds a profile, so the permissions handler must run rather than the entry being served");
		_ = result.UntypedReturnValue.ShouldBeOfType<PermissionsDto>(
			"an action must never receive a value of another action's type");
		((PermissionsDto)result.UntypedReturnValue!).Role.ShouldBe("admin");
	}

	/// <summary>
	/// SAFETY, stated as the consumer sees it. The first action's value must not reach the second, whatever
	/// wrapper it arrives in.
	/// </summary>
	[Fact]
	public async Task Not_hand_the_first_actions_value_to_the_second()
	{
		var middleware = CreateMiddleware();

		DispatchRequestDelegate profileHandler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success(new ProfileDto("Ada")));

		_ = await middleware.InvokeAsync(new ProfileQuery(), NewContext(), profileHandler, CancellationToken.None);

		DispatchRequestDelegate permissionsHandler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success(new PermissionsDto("admin")));

		var result = await middleware.InvokeAsync(
			new PermissionsQuery(), NewContext(), permissionsHandler, CancellationToken.None);

		result.UntypedReturnValue.ShouldNotBeOfType<ProfileDto>(
			"returning the profile here is the silent wrong-type defect this lock exists to prevent");
	}

	/// <summary>
	/// LIVENESS. Caching still works. A guard that treated every hit as a mismatch would satisfy both
	/// safety arms above while disabling the cache entirely, and nothing else here would notice.
	/// </summary>
	[Fact]
	public async Task Still_serve_a_matching_type_from_cache_without_rerunning_the_handler()
	{
		var middleware = CreateMiddleware();

		var executions = 0;
		DispatchRequestDelegate handler = (_, _, _) =>
		{
			executions++;
			return new ValueTask<IMessageResult>(MessageResult.Success(new ProfileDto("Ada")));
		};

		var first = await middleware.InvokeAsync(new ProfileQuery(), NewContext(), handler, CancellationToken.None);
		var second = await middleware.InvokeAsync(new ProfileQuery(), NewContext(), handler, CancellationToken.None);

		executions.ShouldBe(1, "the second dispatch of the same action and key must be served from cache");
		_ = first.UntypedReturnValue.ShouldBeOfType<ProfileDto>();
		_ = second.UntypedReturnValue.ShouldBeOfType<ProfileDto>();
		((ProfileDto)second.UntypedReturnValue!).Name.ShouldBe("Ada");
	}

	/// <summary>
	/// SAFETY for the case the response-type check structurally cannot see. Two actions sharing a cache key
	/// AND a response type are indistinguishable by the stored value alone, so the entry must be attributed
	/// to the action that stored it or the second caller silently receives the first's data.
	/// </summary>
	[Fact]
	public async Task Serve_its_own_value_to_a_second_action_sharing_both_the_key_and_the_response_type()
	{
		var middleware = CreateMiddleware();

		DispatchRequestDelegate nameHandler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success("Ada Lovelace"));

		_ = await middleware.InvokeAsync(new NameQuery(), NewContext(), nameHandler, CancellationToken.None);

		var emailRan = false;
		DispatchRequestDelegate emailHandler = (_, _, _) =>
		{
			emailRan = true;
			return new ValueTask<IMessageResult>(MessageResult.Success("ada@example.com"));
		};

		var result = await middleware.InvokeAsync(new EmailQuery(), NewContext(), emailHandler, CancellationToken.None);

		emailRan.ShouldBeTrue(
			"the entry under this key was stored by the name query, so the email handler must run rather than the entry being served");
		result.UntypedReturnValue.ShouldBe(
			"ada@example.com",
			"an action must never receive another action's value, and a shared response type makes the value itself no evidence");
	}

	/// <summary>
	/// LIVENESS for the arm above, and the arm that fails if the storing identity is not persisted. A guard
	/// that never admits anything satisfies the safety arm while disabling the cache; so does a serializer
	/// that drops the identity on the way through, which is why this asserts across a stored entry rather
	/// than within a single call.
	/// </summary>
	[Fact]
	public async Task Still_serve_the_storing_action_from_cache_when_the_response_type_is_shared()
	{
		var middleware = CreateMiddleware();

		var executions = 0;
		DispatchRequestDelegate handler = (_, _, _) =>
		{
			executions++;
			return new ValueTask<IMessageResult>(MessageResult.Success("Ada Lovelace"));
		};

		_ = await middleware.InvokeAsync(new NameQuery(), NewContext(), handler, CancellationToken.None);
		var second = await middleware.InvokeAsync(new NameQuery(), NewContext(), handler, CancellationToken.None);

		executions.ShouldBe(1, "the same action under the same key must still be served from cache");
		second.UntypedReturnValue.ShouldBe("Ada Lovelace");
	}

	private CachingMiddleware CreateMiddleware()
		=> new(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Create(new CacheOptions { Enabled = true }),
			NullLogger<CachingMiddleware>.Instance);

	private static IMessageContext NewContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	// Fixtures: two pairs of DIFFERENT action types sharing one cache key. The first pair declares
	// different response types, so the stored value itself distinguishes them. The second pair declares the
	// SAME response type, so it does not — that pair is the case a response-type check cannot decide.

	private sealed class ProfileQuery : ICacheable<ProfileDto>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class PermissionsQuery : ICacheable<PermissionsDto>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class NameQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class EmailQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}
}

/// <summary>A response type belonging to the profile query.</summary>
/// <param name="Name">The profile name.</param>
internal sealed record ProfileDto(string Name);

/// <summary>A response type belonging to the permissions query, sharing a cache key with the profile query.</summary>
/// <param name="Role">The granted role.</param>
internal sealed record PermissionsDto(string Role);
