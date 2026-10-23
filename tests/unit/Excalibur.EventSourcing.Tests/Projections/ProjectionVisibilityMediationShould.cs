// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;
using Excalibur.EventSourcing.Encryption.Decorators;
using Excalibur.EventSourcing.Sharding;

using Microsoft.Extensions.Options;

using Projection = Excalibur.EventSourcing.Tests.Encryption.EncryptingProjectionStoreDecoratorShould.TestProjection;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class ProjectionVisibilityMediationShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task MediateVisibilityThroughEncryptionWithoutExposingTheInnerStore(bool provided)
	{
		var inner = A.Fake<IProjectionStore<Projection>>();
		var barrier = A.Fake<IProjectionVisibilityBarrier>();
		A.CallTo(() => inner.GetService(typeof(IProjectionVisibilityBarrier))).Returns(provided ? barrier : null);
		IProjectionStore<Projection> decorator = new EncryptingProjectionStoreDecorator<Projection>(inner,
			A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()), UntenantedContext.Instance);
		var capability = decorator.GetService(typeof(IProjectionVisibilityBarrier));
		if (!provided)
		{
			capability.ShouldBeNull();
			return;
		}
		capability.ShouldNotBeNull();
		(capability is IProjectionStore<Projection>).ShouldBeFalse("visibility must not expose an undecorated projection store");
		capability.ShouldNotBeSameAs(barrier);
		using var token = new CancellationTokenSource();
		await ((IProjectionVisibilityBarrier)capability).EnsureVisibleAsync(token.Token).ConfigureAwait(false);
		A.CallTo(() => barrier.EnsureVisibleAsync(token.Token)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreserveVisibilityFailureInsteadOfReturningSuccessfulAcknowledgment()
	{
		var inner = A.Fake<IProjectionStore<Projection>>();
		var barrier = A.Fake<IProjectionVisibilityBarrier>();
		A.CallTo(() => inner.GetService(typeof(IProjectionVisibilityBarrier))).Returns(barrier);
		A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).ThrowsAsync(new InvalidOperationException("refresh failed"));
		var decorator = A.Fake<IsolatingProjectionStoreDecorator<Projection>>(
			options => options.WithArgumentsForConstructor([inner]));
		var mediated = (IProjectionVisibilityBarrier)decorator.GetService(typeof(IProjectionVisibilityBarrier))!;
		await Should.ThrowAsync<InvalidOperationException>(() => mediated.EnsureVisibleAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ReResolveVisibilityForTheCurrentTenantRatherThanCaptureTheFirstShard()
	{
		var first = A.Fake<IProjectionStore<Projection>>();
		var second = A.Fake<IProjectionStore<Projection>>();
		var firstBarrier = A.Fake<IProjectionVisibilityBarrier>();
		var secondBarrier = A.Fake<IProjectionVisibilityBarrier>();
		A.CallTo(() => first.GetService(typeof(IProjectionVisibilityBarrier))).Returns(firstBarrier);
		A.CallTo(() => second.GetService(typeof(IProjectionVisibilityBarrier))).Returns(secondBarrier);
		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<Projection>>>();
		A.CallTo(() => resolver.Resolve("first")).Returns(first);
		A.CallTo(() => resolver.Resolve("second")).Returns(second);
		var tenant = "first";
		var context = A.Fake<ITenantContext>();
		A.CallTo(() => context.TenantId).ReturnsLazily(() => tenant);
		var routed = new TenantRoutingProjectionStore<Projection>(resolver, context);
		var barrier = (IProjectionVisibilityBarrier)routed.GetService(typeof(IProjectionVisibilityBarrier))!;
		tenant = "second";
		await barrier.EnsureVisibleAsync(CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => firstBarrier.EnsureVisibleAsync(A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => secondBarrier.EnsureVisibleAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		// Losing the capability after resolution must fail, never silently acknowledge visibility.
		A.CallTo(() => second.GetService(typeof(IProjectionVisibilityBarrier))).Returns(null);
		await Should.ThrowAsync<InvalidOperationException>(() => barrier.EnsureVisibleAsync(CancellationToken.None));
		routed.GetService(typeof(IProjectionVisibilityBarrier)).ShouldBeNull();
	}

}
