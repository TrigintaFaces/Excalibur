// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Diagnostics;

namespace Excalibur.Data.Tests.Postgres.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectMeterName()
	{
		PostgresOutboxTelemetryConstants.MeterName.ShouldBe("Excalibur.Data.Postgres.Outbox");
	}

	[Fact]
	public void HaveCorrectVersion()
	{
		PostgresOutboxTelemetryConstants.Version.ShouldBe("1.0");
	}
}
