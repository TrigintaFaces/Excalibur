// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
