// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.LeaderElection.Tests.Helpers;

internal static class LeaderIdentityAssertions
{
	internal static void AssertDefault(string identity)
	{
		var hostname = Environment.GetEnvironmentVariable("HOSTNAME");
		var podName = Environment.GetEnvironmentVariable("POD_NAME");
		if (!string.IsNullOrWhiteSpace(hostname))
		{
			identity.ShouldBe(hostname);
		}
		else if (!string.IsNullOrWhiteSpace(podName))
		{
			identity.ShouldBe(podName);
		}
		else
		{
			Guid.TryParseExact(identity, "N", out var generated).ShouldBeTrue();
			generated.ShouldNotBe(Guid.Empty);
		}
	}
}
