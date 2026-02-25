// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MySql;

namespace Excalibur.Data.Tests.MySql;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MySqlProviderOptionsShould
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		var options = new MySqlProviderOptions();

		options.Name.ShouldBeNull();
		options.ConnectionString.ShouldBe(string.Empty);
		options.CommandTimeout.ShouldBe(30);
		options.ConnectTimeout.ShouldBe(15);
		options.MaxRetryCount.ShouldBe(3);
		options.MaxPoolSize.ShouldBe(100);
		options.MinPoolSize.ShouldBe(0);
		options.EnablePooling.ShouldBeTrue();
		options.ApplicationName.ShouldBeNull();
		options.UseSsl.ShouldBeFalse();
		options.ClearPoolOnDispose.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomName()
	{
		var options = new MySqlProviderOptions { Name = "my-mysql" };
		options.Name.ShouldBe("my-mysql");
	}

	[Fact]
	public void AllowCustomConnectionString()
	{
		var options = new MySqlProviderOptions { ConnectionString = "Server=db;Database=test;" };
		options.ConnectionString.ShouldBe("Server=db;Database=test;");
	}

	[Fact]
	public void AllowCustomTimeouts()
	{
		var options = new MySqlProviderOptions
		{
			CommandTimeout = 60,
			ConnectTimeout = 30
		};

		options.CommandTimeout.ShouldBe(60);
		options.ConnectTimeout.ShouldBe(30);
	}

	[Fact]
	public void AllowCustomPoolSettings()
	{
		var options = new MySqlProviderOptions
		{
			MaxPoolSize = 50,
			MinPoolSize = 5,
			EnablePooling = false
		};

		options.MaxPoolSize.ShouldBe(50);
		options.MinPoolSize.ShouldBe(5);
		options.EnablePooling.ShouldBeFalse();
	}

	[Fact]
	public void AllowSslConfiguration()
	{
		var options = new MySqlProviderOptions { UseSsl = true };
		options.UseSsl.ShouldBeTrue();
	}

	[Fact]
	public void AllowClearPoolOnDispose()
	{
		var options = new MySqlProviderOptions { ClearPoolOnDispose = true };
		options.ClearPoolOnDispose.ShouldBeTrue();
	}

	[Fact]
	public void AllowApplicationName()
	{
		var options = new MySqlProviderOptions { ApplicationName = "MyApp" };
		options.ApplicationName.ShouldBe("MyApp");
	}

	[Fact]
	public void AllowMaxRetryCount()
	{
		var options = new MySqlProviderOptions { MaxRetryCount = 5 };
		options.MaxRetryCount.ShouldBe(5);
	}
}
