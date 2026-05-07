// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Requests;

namespace Excalibur.Data.Tests.DataProcessing.Requests;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class UpdateDataTaskCompletedCountShould
{
	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new UpdateDataTaskCompletedCount(Guid.NewGuid(), 100, null, null!, 30, CancellationToken.None));
	}

	[Fact]
	public void CreateWithValidParameters()
	{
		var config = new DataProcessingOptions();
		var request = new UpdateDataTaskCompletedCount(Guid.NewGuid(), 42, null, config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCommandWithUpdateSql()
	{
		var config = new DataProcessingOptions();
		var request = new UpdateDataTaskCompletedCount(Guid.NewGuid(), 100, "cursor-abc", config, 30, CancellationToken.None);

		request.Command.CommandText.ShouldContain("UPDATE");
		request.Command.CommandText.ShouldContain(config.TableName);
		request.Command.CommandText.ShouldContain("CompletedCount");
		request.Command.CommandText.ShouldContain("ProcessedCursor");
	}

	[Fact]
	public void HaveCoalesceSql_ToPreserveExistingCursorWhenNull()
	{
		// Arrange — passing null cursor should not overwrite existing value
		var config = new DataProcessingOptions();

		// Act
		var request = new UpdateDataTaskCompletedCount(Guid.NewGuid(), 50, null, config, 30, CancellationToken.None);

		// Assert — SQL should use COALESCE to preserve existing cursor on null
		request.Command.CommandText.ShouldContain("COALESCE");
	}

	[Fact]
	public void AcceptNonNullProcessedCursor()
	{
		// Arrange
		var config = new DataProcessingOptions();

		// Act — should not throw with a non-null cursor
		var request = new UpdateDataTaskCompletedCount(Guid.NewGuid(), 100, "page-cursor-42", config, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain("ProcessedCursor");
		request.Command.CommandText.ShouldContain("COALESCE");
	}
}
