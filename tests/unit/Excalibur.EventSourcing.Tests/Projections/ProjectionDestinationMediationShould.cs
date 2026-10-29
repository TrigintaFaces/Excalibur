// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Encryption.Decorators;
using Excalibur.EventSourcing.Sharding;

using Microsoft.Extensions.Options;

using Projection = Excalibur.EventSourcing.Tests.Encryption.EncryptingProjectionStoreDecoratorShould.TestProjection;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class ProjectionDestinationMediationShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void PreserveDestinationIdentityThroughEncryptionWithoutExposingTheInnerStore(bool available)
	{
		var inner = A.Fake<IProjectionStore<Projection>>();
		var identity = A.Fake<IProjectionDestinationIdentity>();
		A.CallTo(() => identity.Identity).Returns("elasticsearch/people-employees");
		A.CallTo(() => inner.GetService(typeof(IProjectionDestinationIdentity))).Returns(available ? identity : null);
		IProjectionStore<Projection> decorator = new EncryptingProjectionStoreDecorator<Projection>(inner,
			A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()), UntenantedContext.Instance);
		var view = decorator.GetService(typeof(IProjectionDestinationIdentity));
		if (!available) { view.ShouldBeNull(); return; }
		((IProjectionDestinationIdentity)view!).Identity.ShouldBe("elasticsearch/people-employees");
		view.ShouldNotBeSameAs(identity);
		(view is IProjectionStore<Projection>).ShouldBeFalse();
	}

	[Fact]
	public void ResolveDestinationIdentityForTheCurrentTenantOnEveryRead()
	{
		var first = A.Fake<IProjectionStore<Projection>>();
		var second = A.Fake<IProjectionStore<Projection>>();
		var firstIdentity = A.Fake<IProjectionDestinationIdentity>();
		var secondIdentity = A.Fake<IProjectionDestinationIdentity>();
		A.CallTo(() => firstIdentity.Identity).Returns("elasticsearch/first-employees");
		A.CallTo(() => secondIdentity.Identity).Returns("elasticsearch/second-employees");
		A.CallTo(() => first.GetService(typeof(IProjectionDestinationIdentity))).Returns(firstIdentity);
		A.CallTo(() => second.GetService(typeof(IProjectionDestinationIdentity))).Returns(secondIdentity);
		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<Projection>>>();
		A.CallTo(() => resolver.Resolve("first")).Returns(first);
		A.CallTo(() => resolver.Resolve("second")).Returns(second);
		var tenant = "first";
		var context = A.Fake<ITenantContext>();
		A.CallTo(() => context.TenantId).ReturnsLazily(() => tenant);
		var routed = new TenantRoutingProjectionStore<Projection>(resolver, context);
		var view = (IProjectionDestinationIdentity)routed.GetService(typeof(IProjectionDestinationIdentity))!;
		view.Identity.ShouldBe("elasticsearch/first-employees");
		tenant = "second";
		view.Identity.ShouldBe("elasticsearch/second-employees");
		A.CallTo(() => second.GetService(typeof(IProjectionDestinationIdentity))).Returns(null);
		Should.Throw<InvalidOperationException>(() => _ = view.Identity);
		routed.GetService(typeof(IProjectionDestinationIdentity)).ShouldBeNull();
	}

	/// <summary>
	/// A capability view is itself a projection store, so it can be probed in turn. It must answer for
	/// its own capability and route every other probe back through the decorator -- never report the
	/// store beneath as capability-less, and never hand out that store's raw capability.
	/// </summary>
	/// <remarks>
	/// RED when the shared capability-view base stops supplying its capability-resolution seam: the
	/// store interface's default answers only for the instance itself, so the second assertion below
	/// reads null. The architecture guard proves such a seam is REACHABLE from every view; this proves
	/// the seam DELEGATES, which a seam returning null unconditionally would satisfy the guard without
	/// doing.
	/// </remarks>
	[Fact]
	public void ResolveCapabilitiesThroughTheDecoratorWhenAViewIsItselfProbed()
	{
		var inner = A.Fake<IProjectionStore<Projection>>();
		var positioned = A.Fake<IPositionedProjectionStore<Projection>>();
		var identity = A.Fake<IProjectionDestinationIdentity>();
		A.CallTo(() => identity.Identity).Returns("elasticsearch/people-employees");
		A.CallTo(() => inner.GetService(typeof(IPositionedProjectionStore<Projection>))).Returns(positioned);
		A.CallTo(() => inner.GetService(typeof(IProjectionDestinationIdentity))).Returns(identity);
		IProjectionStore<Projection> decorator = new EncryptingProjectionStoreDecorator<Projection>(inner,
			A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()), UntenantedContext.Instance);

		var view = (IProjectionStore<Projection>)decorator.GetService(typeof(IPositionedProjectionStore<Projection>))!;

		// Its own capability resolves to the MEDIATED form -- the view -- not to the raw inner store,
		// which would skip the decryption the view exists to apply.
		view.GetService(typeof(IPositionedProjectionStore<Projection>)).ShouldBeSameAs(view);
		view.GetService(typeof(IPositionedProjectionStore<Projection>)).ShouldNotBeSameAs(positioned);

		// A capability the view does not provide is resolved through the decorator, so the store beneath
		// is not reported as capability-less. The decorator mediates it, so it is not the raw inner one.
		var destination = view.GetService(typeof(IProjectionDestinationIdentity));
		var mediated = destination.ShouldBeAssignableTo<IProjectionDestinationIdentity>(
			"a probed capability view must not report the store it mediates as having no capabilities");
		mediated.Identity.ShouldBe("elasticsearch/people-employees");
		destination.ShouldNotBeSameAs(identity);

		// Liveness has a safety half: an absent capability must still read as absent through the view.
		A.CallTo(() => inner.GetService(typeof(ICursorProjectionStore<Projection>))).Returns(null);
		view.GetService(typeof(ICursorProjectionStore<Projection>)).ShouldBeNull();
		_ = Should.Throw<ArgumentNullException>(() => view.GetService(null!));
	}

	/// <summary>
	/// A routed generation reader is pinned to the tenant whose execution context opened it. Every member
	/// must refuse once the ambient tenant moves -- including the synchronous scope getter, which is the
	/// one member that read through to the inner handle without checking.
	/// </summary>
	/// <remarks>
	/// RED on the pre-fix getter: it forwarded <c>inner.DestinationScope</c> unconditionally, so the
	/// second assertion returned the first tenant's scope instead of throwing. The liveness half is
	/// load-bearing -- a getter that threw for everyone would satisfy the safety half while inert.
	/// </remarks>
	[Fact]
	public void RefuseAScopeReadOnARoutedGenerationReaderFromAnotherTenantsContext()
	{
		var inner = A.Fake<IProjectionStore<Projection>>();
		var innerReader = A.Fake<IProjectionGenerationReader<Projection>>();
		A.CallTo(() => innerReader.DestinationScope).Returns("postgres/acme-db");
		var factory = A.Fake<IProjectionGenerationReaderFactory<Projection>>();
		A.CallTo(() => factory.OpenReader("postgres/acme-db/people")).Returns(innerReader);
		A.CallTo(() => inner.GetService(typeof(IProjectionGenerationReaderFactory<Projection>))).Returns(factory);
		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<Projection>>>();
		A.CallTo(() => resolver.Resolve(A<string>._)).Returns(inner);
		var tenant = "acme";
		var context = A.Fake<ITenantContext>();
		A.CallTo(() => context.TenantId).ReturnsLazily(() => tenant);
		var routed = new TenantRoutingProjectionStore<Projection>(resolver, context);
		var opened = (IProjectionGenerationReaderFactory<Projection>)routed
			.GetService(typeof(IProjectionGenerationReaderFactory<Projection>))!;
		var reader = opened.OpenReader("postgres/acme-db/people");

		// Liveness: the tenant that opened the handle still reads its own scope.
		reader.DestinationScope.ShouldBe("postgres/acme-db");

		// Safety: the handle is pinned, so a moved ambient tenant is refused on EVERY member.
		tenant = "globex";
		_ = Should.Throw<InvalidOperationException>(() => _ = reader.DestinationScope);
	}
}
