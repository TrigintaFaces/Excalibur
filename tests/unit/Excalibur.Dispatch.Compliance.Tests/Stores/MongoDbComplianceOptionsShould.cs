// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Tests.Stores;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MongoDbComplianceOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new MongoDbComplianceOptions();

		// Assert
		options.ConnectionString.ShouldBeNull();
		options.DatabaseName.ShouldBe("compliance");
		options.CollectionPrefix.ShouldBe("dispatch_");
		options.ServerSelectionTimeoutSeconds.ShouldBe(30);
		options.ConnectTimeoutSeconds.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Act
		var options = new MongoDbComplianceOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "custom_db",
			CollectionPrefix = "app_",
			ServerSelectionTimeoutSeconds = 60,
			ConnectTimeoutSeconds = 20
		};

		// Assert
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
		options.DatabaseName.ShouldBe("custom_db");
		options.CollectionPrefix.ShouldBe("app_");
		options.ServerSelectionTimeoutSeconds.ShouldBe(60);
		options.ConnectTimeoutSeconds.ShouldBe(20);
	}
}
