// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Requests;

namespace Excalibur.Data.Tests.DataProcessing.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeleteDataTaskShould
{
	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new DeleteDataTask(Guid.NewGuid(), null!, 30, CancellationToken.None));
	}

	[Fact]
	public void CreateWithValidParameters()
	{
		var config = new DataProcessingConfiguration();
		var request = new DeleteDataTask(Guid.NewGuid(), config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCommandWithTableName()
	{
		var config = new DataProcessingConfiguration();
		var request = new DeleteDataTask(Guid.NewGuid(), config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldContain(config.TableName);
	}

	[Fact]
	public void HaveCommandWithDeleteSql()
	{
		var config = new DataProcessingConfiguration();
		var request = new DeleteDataTask(Guid.NewGuid(), config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldContain("DELETE");
	}
}
