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
}
