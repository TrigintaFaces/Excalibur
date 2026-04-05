// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CdcFatalErrorOptionsShould
{
	[Fact]
	public void HaveNullOnFatalErrorByDefault()
	{
		var options = new CdcFatalErrorOptions();

		options.OnFatalError.ShouldBeNull();
	}

	[Fact]
	public void AcceptCustomFatalErrorHandler()
	{
		var options = new CdcFatalErrorOptions
		{
			OnFatalError = (_, _) => Task.CompletedTask
		};

		options.OnFatalError.ShouldNotBeNull();
	}
}
