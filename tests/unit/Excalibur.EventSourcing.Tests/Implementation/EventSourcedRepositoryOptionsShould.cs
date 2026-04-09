// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Implementation;

namespace Excalibur.EventSourcing.Tests.Implementation;

/// <summary>
/// Tests for <see cref="EventSourcedRepositoryOptions"/> property defaults and configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcedRepositoryOptionsShould
{
	[Fact]
	public void DefaultEnableAutoUpcast_ToFalse()
	{
		var options = new EventSourcedRepositoryOptions();
		options.EnableAutoUpcast.ShouldBeFalse();
	}

	[Fact]
	public void DefaultEnableAutoSnapshotUpgrade_ToFalse()
	{
		var options = new EventSourcedRepositoryOptions();
		options.EnableAutoSnapshotUpgrade.ShouldBeFalse();
	}

	[Fact]
	public void DefaultTargetSnapshotVersion_To1()
	{
		var options = new EventSourcedRepositoryOptions();
		options.TargetSnapshotVersion.ShouldBe(1);
	}

	[Fact]
	public void DefaultOutboxStagingStrategy_ToAuto()
	{
		var options = new EventSourcedRepositoryOptions();
		options.OutboxStagingStrategy.ShouldBe(OutboxStagingStrategy.Auto);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var options = new EventSourcedRepositoryOptions
		{
			EnableAutoUpcast = true,
			EnableAutoSnapshotUpgrade = true,
			TargetSnapshotVersion = 5,
			OutboxStagingStrategy = OutboxStagingStrategy.Transactional
		};

		options.EnableAutoUpcast.ShouldBeTrue();
		options.EnableAutoSnapshotUpgrade.ShouldBeTrue();
		options.TargetSnapshotVersion.ShouldBe(5);
		options.OutboxStagingStrategy.ShouldBe(OutboxStagingStrategy.Transactional);
	}
}
