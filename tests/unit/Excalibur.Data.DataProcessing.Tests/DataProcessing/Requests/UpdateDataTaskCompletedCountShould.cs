// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Requests;

namespace Excalibur.Data.Tests.DataProcessing.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class UpdateDataTaskCompletedCountShould
{
	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new UpdateDataTaskCompletedCount(Guid.NewGuid(), 100, null!, 30, CancellationToken.None));
	}

	[Fact]
	public void CreateWithValidParameters()
	{
		var config = new DataProcessingConfiguration();
		var request = new UpdateDataTaskCompletedCount(Guid.NewGuid(), 42, config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCommandWithUpdateSql()
	{
		var config = new DataProcessingConfiguration();
		var request = new UpdateDataTaskCompletedCount(Guid.NewGuid(), 100, config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldContain("UPDATE");
		request.Command.CommandText.ShouldContain(config.TableName);
		request.Command.CommandText.ShouldContain("CompletedCount");
	}
}
