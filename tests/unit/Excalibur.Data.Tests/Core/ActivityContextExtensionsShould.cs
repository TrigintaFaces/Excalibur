// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ActivityContextExtensionsShould
{
	[Fact]
	public void ApplicationName_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.ApplicationName());
	}

	[Fact]
	public void ClientAddress_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.ClientAddress());
	}

	[Fact]
	public void Configuration_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.Configuration());
	}

	[Fact]
	public void CorrelationId_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.CorrelationId());
	}

	[Fact]
	public void ETag_Get_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.ETag());
	}

	[Fact]
	public void ETag_Set_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.ETag("etag-value"));
	}

	[Fact]
	public void LatestETag_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.LatestETag());
	}

	[Fact]
	public void Get_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.Get<string>("key"));
	}

	[Fact]
	public void ServiceProvider_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.ServiceProvider());
	}

	[Fact]
	public void DomainDb_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.DomainDb());
	}

	[Fact]
	public void TenantId_ThrowsForNullContext()
	{
		IActivityContext? context = null;
		Should.Throw<ArgumentNullException>(() => context!.TenantId());
	}

	[Fact]
	public void CorrelationId_ReturnsNullWhenTheContextCarriesNone()
	{
		// This arm used to be named ...ReturnsEmptyWhenNotSet and assert Guid.Empty. It passed, and it
		// passed for a reason that had nothing to do with the accessor: A.Fake<IActivityContext>()
		// auto-fakes the ICorrelationId the accessor asks for, so the context DID carry one, and its
		// Value was default(Guid) — which is Guid.Empty. The name said "not set"; the fixture said
		// "set, to the default". Both the old sentinel contract and the new null contract satisfy it.
		//
		// Configured explicitly so the premise in the name is the premise under test.
		var context = A.Fake<IActivityContext>();
		_ = A.CallTo(() => context.GetValue(nameof(ActivityContextExtensions.CorrelationId), default(ICorrelationId)))
			.Returns(null);

		var result = context.CorrelationId();

		result.ShouldBeNull();
	}

	[Fact]
	public void CorrelationId_ReturnsTheValueWhenTheContextCarriesOne()
	{
		// LIVENESS partner: without it, an accessor that returned null unconditionally would satisfy the
		// arm above, and "absent reads as absent" would be indistinguishable from "nothing works".
		var expected = Guid.NewGuid();
		var correlationId = A.Fake<ICorrelationId>();
		_ = A.CallTo(() => correlationId.Value).Returns(expected);

		var context = A.Fake<IActivityContext>();
		_ = A.CallTo(() => context.GetValue(nameof(ActivityContextExtensions.CorrelationId), default(ICorrelationId)))
			.Returns(correlationId);

		context.CorrelationId().ShouldBe(expected);
	}

	[Fact]
	public void Get_DelegatesGetValueOnContext()
	{
		var context = A.Fake<IActivityContext>();
		A.CallTo(() => context.GetValue("mykey", A<string?>._)).Returns("myvalue");

		var result = context.Get<string>("mykey");
		result.ShouldBe("myvalue");
	}
}
