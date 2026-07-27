// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Regression lock: <c>AddOracleInboxStore</c> wires <c>ValidateOnStart()</c> so a misconfigured Oracle
/// inbox fails fast at host startup rather than surfacing on first use.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Persistence")]
public sealed class OracleInboxValidateOnStartShould
{
	[Fact]
	public void WireStartupValidation_ForTheOptionsRegistration()
	{
		var services = new ServiceCollection();

		services.AddOracleInboxStore(o =>
		{
			o.ConnectionString = "User Id=u;Password=p;Data Source=db";
			o.TableName = "Inbox";
		});

		using var provider = services.BuildServiceProvider();

		// ValidateOnStart() registers an IStartupValidator; without it (the pre-fix Configure-only
		// registration) this resolves null and the lock goes RED.
		provider.GetService<IStartupValidator>().ShouldNotBeNull();
	}

	[Fact]
	public void FailFast_WhenOptionsAreInvalid()
	{
		var services = new ServiceCollection();

		// Invalid: empty connection string is rejected by OracleInboxOptionsValidator.
		services.AddOracleInboxStore(o =>
		{
			o.ConnectionString = string.Empty;
			o.TableName = "Inbox";
		});

		using var provider = services.BuildServiceProvider();
		var startupValidator = provider.GetRequiredService<IStartupValidator>();

		Should.Throw<OptionsValidationException>(() => startupValidator.Validate());
	}
}
