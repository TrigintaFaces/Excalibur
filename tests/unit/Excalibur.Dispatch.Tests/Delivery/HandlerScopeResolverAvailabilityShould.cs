// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "DependencyInjection")]
public sealed class HandlerScopeResolverAvailabilityShould
{
	[Fact]
	public async Task RefuseKnownScopedActivationWhenNoScopeIsAvailable()
	{
		var services = new ServiceCollection();
		services.AddScoped<ScopedHandler>();
		var resolver = CreateResolver(services);
		resolver.CanCreateScope.ShouldBeFalse();
		resolver.RequiresScope(typeof(ScopedHandler)).ShouldBeTrue();
		var invoked = 0;
		var error = await Should.ThrowAsync<InvalidOperationException>(() => resolver.RunAsync(
			typeof(ScopedHandler), null, 42, (_, value) =>
			{
				invoked++; return ValueTask.FromResult(value);
			}).AsTask());
		error.Message.ShouldContain(nameof(ScopedHandler));
		invoked.ShouldBe(0);
	}

	[Fact]
	public void RetainTheProvenRootSafeFastPathWithoutAScopeFactory()
	{
		var services = new ServiceCollection();
		services.AddSingleton<RootSafeHandler>();
		var resolver = CreateResolver(services);
		resolver.RequiresScope(typeof(RootSafeHandler)).ShouldBeFalse();
		resolver.RequiresScope(typeof(RootSafeHandler)).ShouldBeFalse();
	}

	[Theory]
	[InlineData(false, 0)]
	[InlineData(true, 0)]
	[InlineData(false, 1)]
	[InlineData(true, 1)]
	[InlineData(false, 2)]
	[InlineData(true, 2)]
	public async Task BorrowAnExplicitScopeWithoutAFactoryEvenWhenMetadataIsMissing(bool missingMetadata, int outcome)
	{
		var services = new ServiceCollection();
		services.AddScoped<ScopedHandler>();
		var resolver = CreateResolver(missingMetadata ? null : services);
		var borrowed = A.Fake<IServiceProvider>(o => o.Implements<IDisposable>());
		resolver.RequiresScope(typeof(ScopedHandler)).ShouldBeTrue();
		var execution = resolver.RunAsync(typeof(ScopedHandler), borrowed, outcome, (provider, value) =>
		{
			provider.ShouldBeSameAs(borrowed);
			return value switch
			{
				1 => ValueTask.FromException<int>(new IOException("BORROWED-HANDLER-FAULT")),
				2 => ValueTask.FromException<int>(new OperationCanceledException("BORROWED-HANDLER-CANCELLATION")),
				_ => ValueTask.FromResult(42),
			};
		}).AsTask();
		if (outcome == 1) { await Should.ThrowAsync<IOException>(() => execution); }
		else if (outcome == 2) { await Should.ThrowAsync<OperationCanceledException>(() => execution); }
		else { (await execution).ShouldBe(42); }
		A.CallTo(() => ((IDisposable)borrowed).Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public async Task BorrowTheAmbientScopeWithoutAFactory()
	{
		var services = new ServiceCollection();
		services.AddScoped<ScopedHandler>();
		var borrowed = A.Fake<IServiceProvider>(o => o.Implements<IDisposable>());
		var ambient = A.Fake<IDispatchAmbientScopeAccessor>();
		A.CallTo(() => ambient.CurrentServiceProvider).Returns(borrowed);
		var resolver = CreateResolver(services, ambient: ambient);
		resolver.RequiresScope(typeof(ScopedHandler)).ShouldBeTrue();
		(await resolver.RunAsync(typeof(ScopedHandler), null, 42, (provider, value) =>
		{
			provider.ShouldBeSameAs(borrowed); return ValueTask.FromResult(value);
		})).ShouldBe(42);
		A.CallTo(() => ((IDisposable)borrowed).Dispose()).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public async Task DisposeAnOwnedAsyncScopeOnceOnSuccessFaultOrCancellationWithoutLifetimeMetadata(int outcome)
	{
		var scopedProvider = A.Fake<IServiceProvider>();
		var scope = A.Fake<IServiceScope>(o => o.Implements<IAsyncDisposable>());
		A.CallTo(() => scope.ServiceProvider).Returns(scopedProvider);
		var factory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => factory.CreateScope()).Returns(scope);
		var resolver = CreateResolver(null, factory);
		resolver.RequiresScope(typeof(ScopedHandler)).ShouldBeTrue();
		var execution = resolver.RunAsync(typeof(ScopedHandler), null, outcome, (provider, value) =>
		{
			provider.ShouldBeSameAs(scopedProvider);
			return value switch
			{
				1 => ValueTask.FromException<int>(new IOException("CONTROLLED-HANDLER-FAULT")),
				2 => ValueTask.FromException<int>(new OperationCanceledException("CONTROLLED-HANDLER-CANCELLATION")),
				_ => ValueTask.FromResult(42),
			};
		}).AsTask();
		if (outcome == 1) { await Should.ThrowAsync<IOException>(() => execution); }
		else if (outcome == 2) { await Should.ThrowAsync<OperationCanceledException>(() => execution); }
		else { (await execution).ShouldBe(42); }
		A.CallTo(() => factory.CreateScope()).MustHaveHappenedOnceExactly();
		A.CallTo(() => ((IAsyncDisposable)scope).DisposeAsync()).MustHaveHappenedOnceExactly();
		A.CallTo(() => scope.Dispose()).MustNotHaveHappened();
	}

	private static HandlerScopeResolver CreateResolver(IServiceCollection? services,
		IServiceScopeFactory? factory = null, IDispatchAmbientScopeAccessor? ambient = null)
	{
		var root = A.Fake<IServiceProvider>();
		A.CallTo(() => root.GetService(A<Type>._)).Returns(null);
		if (services is not null)
		{
			A.CallTo(() => root.GetService(typeof(HandlerLifetimeRegistry))).Returns(new HandlerLifetimeRegistry(services));
		}
		A.CallTo(() => root.GetService(typeof(IServiceScopeFactory))).Returns(factory);
		A.CallTo(() => root.GetService(typeof(IDispatchAmbientScopeAccessor))).Returns(ambient);
		return new HandlerScopeResolver(root);
	}

	private sealed class ScopedHandler;
	private sealed class RootSafeHandler;
}
