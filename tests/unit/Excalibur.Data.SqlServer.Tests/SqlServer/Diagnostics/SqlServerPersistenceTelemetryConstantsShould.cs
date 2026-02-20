// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Diagnostics;

namespace Excalibur.Data.Tests.SqlServer.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerPersistenceTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectMeterName()
	{
		SqlServerPersistenceTelemetryConstants.MeterName.ShouldBe("Excalibur.Data.SqlServer.Persistence");
	}

	[Fact]
	public void HaveCorrectVersion()
	{
		SqlServerPersistenceTelemetryConstants.Version.ShouldBe("1.0.0");
	}
}
