// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching.Diagnostics;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class DispatchCachingTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectMeterName()
	{
		DispatchCachingTelemetryConstants.MeterName.ShouldBe("Excalibur.Dispatch.Caching");
	}

	[Fact]
	public void HaveCorrectActivitySourceName()
	{
		DispatchCachingTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Dispatch.Caching");
	}

	[Fact]
	public void HaveCorrectVersion()
	{
		DispatchCachingTelemetryConstants.Version.ShouldBe("1.0.0");
	}

	[Fact]
	public void HaveConsistentMeterAndActivitySourceNames()
	{
		// By convention, Meter and ActivitySource should share the same name
		DispatchCachingTelemetryConstants.MeterName
			.ShouldBe(DispatchCachingTelemetryConstants.ActivitySourceName);
	}

	[Fact]
	public void HaveNonEmptyValues()
	{
		DispatchCachingTelemetryConstants.MeterName.ShouldNotBeNullOrWhiteSpace();
		DispatchCachingTelemetryConstants.ActivitySourceName.ShouldNotBeNullOrWhiteSpace();
		DispatchCachingTelemetryConstants.Version.ShouldNotBeNullOrWhiteSpace();
	}
}
