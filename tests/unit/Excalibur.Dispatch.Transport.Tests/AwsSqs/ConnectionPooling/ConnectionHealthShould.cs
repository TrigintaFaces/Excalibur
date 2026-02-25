// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.ConnectionPooling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ConnectionHealthShould
{
	[Fact]
	public void HaveExpectedValues()
	{
		ConnectionHealth.Healthy.ShouldBe((ConnectionHealth)0);
		ConnectionHealth.Unhealthy.ShouldBe((ConnectionHealth)1);
		ConnectionHealth.Unknown.ShouldBe((ConnectionHealth)2);
	}

	[Fact]
	public void HaveThreeValues()
	{
		Enum.GetValues<ConnectionHealth>().Length.ShouldBe(3);
	}

	[Fact]
	public void DefaultToHealthy()
	{
		default(ConnectionHealth).ShouldBe(ConnectionHealth.Healthy);
	}
}
