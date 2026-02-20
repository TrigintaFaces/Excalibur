// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Requests;

namespace Excalibur.Data.Tests.DataProcessing.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SelectPendingDataTasksShould
{
	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new SelectPendingDataTasks(null!, 30, CancellationToken.None));
	}

	[Fact]
	public void CreateWithValidParameters()
	{
		var config = new DataProcessingConfiguration();
		var request = new SelectPendingDataTasks(config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCommandWithSelectSql()
	{
		var config = new DataProcessingConfiguration();
		var request = new SelectPendingDataTasks(config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain(config.TableName);
	}

	[Fact]
	public void HaveCommandWithAttemptsFilter()
	{
		var config = new DataProcessingConfiguration();
		var request = new SelectPendingDataTasks(config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldContain("Attempts < MaxAttempts");
	}
}
