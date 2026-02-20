// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Core;
using Excalibur.Jobs.Outbox;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Unit tests for <see cref="OutboxJobConfig"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Outbox")]
public sealed class OutboxJobConfigShould
{
	[Fact]
	public void InheritFromJobConfig()
	{
		// Assert
		typeof(OutboxJobConfig).BaseType.ShouldBe(typeof(JobConfig));
	}

	[Fact]
	public void CreateSuccessfully()
	{
		// Act
		var config = new OutboxJobConfig();

		// Assert
		config.ShouldNotBeNull();
	}

	[Fact]
	public void InheritCronScheduleProperty()
	{
		// Act
		var config = new OutboxJobConfig
		{
			CronSchedule = "0 */5 * * * ?"
		};

		// Assert
		config.CronSchedule.ShouldBe("0 */5 * * * ?");
	}

	[Fact]
	public void InheritJobNameProperty()
	{
		// Act
		var config = new OutboxJobConfig
		{
			JobName = "OutboxProcessor"
		};

		// Assert
		config.JobName.ShouldBe("OutboxProcessor");
	}

	[Fact]
	public void InheritJobGroupProperty()
	{
		// Act
		var config = new OutboxJobConfig
		{
			JobGroup = "Messaging"
		};

		// Assert
		config.JobGroup.ShouldBe("Messaging");
	}

	[Fact]
	public void BeAssignableToJobConfig()
	{
		// Arrange
		var config = new OutboxJobConfig();

		// Act & Assert
		config.ShouldBeAssignableTo<JobConfig>();
	}
}
