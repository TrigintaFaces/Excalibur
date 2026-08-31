// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.TypeResolution;

namespace Excalibur.Dispatch.Tests.TypeResolution;

/// <summary>
/// Binds the two properties the resolution cache has to hold at once: a name is resolved by walking the
/// resolvers at most once, and the answer never outlives the registration that produced it.
/// </summary>
/// <remarks>
/// <para>
/// Resolution sits on the deserialize path, so it runs once per message. Walking every registered
/// resolver on every call -- and doing it while holding a process-wide lock, which is what a registry
/// guarded by a single lock does -- makes that walk a serialization point for every thread deserializing
/// anything. Memoising the successful answers removes both the walk and the contention.
/// </para>
/// <para>
/// Both arms are needed and they pull against each other. The caching arm alone is satisfied by a cache
/// that never invalidates, which would answer from a registration that has since been torn down -- the
/// exact hazard a memoised static registry introduces. The invalidation arm alone is satisfied by a
/// cache that stores nothing. Together they say answers are reused, and only while they are still true.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Collection(TypeResolverRegistryCollection.Name)]
public sealed class TypeResolverRegistryCachingShould
{
	private const string ResolvedName = "Test.Caching.CachedMarker";

	[Fact]
	public void ConsultTheResolversOnlyOnceForARepeatedName()
	{
		TypeResolverRegistry.Clear();

		var resolver = new CountingTypeResolver(ResolvedName, typeof(CachedMarker));
		TypeResolverRegistry.Register(resolver);
		try
		{
			TypeResolverRegistry.TryResolveType(ResolvedName, out var first).ShouldBeTrue();
			TypeResolverRegistry.TryResolveType(ResolvedName, out var second).ShouldBeTrue();
			TypeResolverRegistry.TryResolveType(ResolvedName, out var third).ShouldBeTrue();

			first.ShouldBe(typeof(CachedMarker));
			second.ShouldBe(typeof(CachedMarker));
			third.ShouldBe(typeof(CachedMarker));

			resolver.Calls.ShouldBe(
				1,
				"three resolutions of the same name should consult the resolver chain once and answer the "
				+ "rest from the memoised result -- this walk is on the per-message deserialize path");
		}
		finally
		{
			TypeResolverRegistry.Clear();
		}
	}

	[Fact]
	public void StopResolvingANameOnceItsResolverIsGone()
	{
		TypeResolverRegistry.Clear();

		var resolver = new CountingTypeResolver(ResolvedName, typeof(CachedMarker));
		TypeResolverRegistry.Register(resolver);
		try
		{
			TypeResolverRegistry.TryResolveType(ResolvedName, out _).ShouldBeTrue(
				"liveness: the name must resolve while its resolver is registered, or the arm below proves "
				+ "nothing");
		}
		finally
		{
			TypeResolverRegistry.Clear();
		}

		TypeResolverRegistry.TryResolveType(ResolvedName, out var afterClear).ShouldBeFalse(
			"the resolver that produced this answer has been removed, so a memoised answer must go with "
			+ "it -- otherwise the registry keeps answering from a registration that no longer exists");
		afterClear.ShouldBeNull();
	}

	[Fact]
	public void NotRetainAnythingForANameThatNoResolverAnswers()
	{
		const string unknownName = "Test.Caching.NeverRegistered";

		TypeResolverRegistry.Clear();

		TypeResolverRegistry.TryResolveType(unknownName, out _).ShouldBeFalse();

		var resolver = new CountingTypeResolver(unknownName, typeof(CachedMarker));
		TypeResolverRegistry.Register(resolver);
		try
		{
			TypeResolverRegistry.TryResolveType(unknownName, out var resolved).ShouldBeTrue(
				"a failed lookup must not be remembered as a failure: a name that could not be resolved "
				+ "before a resolver was registered has to resolve after one is");
			resolved.ShouldBe(typeof(CachedMarker));
		}
		finally
		{
			TypeResolverRegistry.Clear();
		}
	}

	private sealed class CachedMarker;

	private sealed class CountingTypeResolver(string name, Type type) : ITypeResolver
	{
		private int _calls;

		public int Calls => Volatile.Read(ref _calls);

		public bool TryGetType(string typeName, [NotNullWhen(true)] out Type? resolved)
		{
			// Count only consultations for the name under test. The registry is process-wide, so while
			// this resolver is registered it is also consulted for every other name anything in the
			// assembly happens to resolve; counting those would make the assertion depend on what else
			// ran first.
			if (!string.Equals(typeName, name, StringComparison.Ordinal))
			{
				resolved = null;
				return false;
			}

			_ = Interlocked.Increment(ref _calls);
			resolved = type;
			return true;
		}
	}
}
