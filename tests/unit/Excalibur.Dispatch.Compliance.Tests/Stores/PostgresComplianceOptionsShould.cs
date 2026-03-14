// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Tests.Stores;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class PostgresComplianceOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new PostgresComplianceOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
		options.SchemaName.ShouldBe("compliance");
		options.TablePrefix.ShouldBe("dispatch_");
		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Act
		var options = new PostgresComplianceOptions
		{
			ConnectionString = "Host=localhost;Database=test",
			SchemaName = "custom_schema",
			TablePrefix = "app_",
			CommandTimeoutSeconds = 60
		};

		// Assert
		options.ConnectionString.ShouldBe("Host=localhost;Database=test");
		options.SchemaName.ShouldBe("custom_schema");
		options.TablePrefix.ShouldBe("app_");
		options.CommandTimeoutSeconds.ShouldBe(60);
	}
}
