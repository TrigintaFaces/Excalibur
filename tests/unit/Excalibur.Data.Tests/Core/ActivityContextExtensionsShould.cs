// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Domain;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
	public void CorrelationId_ReturnsEmptyWhenNotSet()
	{
		var context = A.Fake<IActivityContext>();

		var result = context.CorrelationId();
		result.ShouldBe(Guid.Empty);
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
