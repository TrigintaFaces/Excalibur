// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Diagnostics;

namespace Excalibur.Caching.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="CachingTelemetryConstants"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CachingTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectMeterName()
	{
		// Assert
		CachingTelemetryConstants.MeterName.ShouldBe("Excalibur.Caching");
	}

	[Fact]
	public void HaveCorrectActivitySourceName()
	{
		// Assert
		CachingTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Caching");
	}

	[Fact]
	public void HaveCorrectVersion()
	{
		// Assert
		CachingTelemetryConstants.Version.ShouldBe("1.0.0");
	}
}
