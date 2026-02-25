// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Unit tests for <see cref="JobInstanceInfo"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Coordination")]
public sealed class JobInstanceInfoShould : UnitTestBase
{
	private static readonly string[] TestJobTypes = ["TestJob"];
	private static readonly string[] MultipleJobTypes = ["JobA", "JobB"];

	private static JobInstanceCapabilities CreateTestCapabilities()
		=> new(5, TestJobTypes);

	[Fact]
	public void RequireInstanceId()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new JobInstanceInfo(null!, "host", CreateTestCapabilities()));
	}

	[Fact]
	public void RequireHostName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new JobInstanceInfo("instance-1", null!, CreateTestCapabilities()));
	}

	[Fact]
	public void RequireCapabilities()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new JobInstanceInfo("instance-1", "host", null!));
	}

	[Fact]
	public void StoreInstanceId()
	{
		// Act
		var info = new JobInstanceInfo("instance-123", "host", CreateTestCapabilities());

		// Assert
		info.InstanceId.ShouldBe("instance-123");
	}

	[Fact]
	public void StoreHostName()
	{
		// Act
		var info = new JobInstanceInfo("instance-1", "worker-01.example.com", CreateTestCapabilities());

		// Assert
		info.HostName.ShouldBe("worker-01.example.com");
	}

	[Fact]
	public void StoreCapabilities()
	{
		// Arrange
		var capabilities = new JobInstanceCapabilities(10, MultipleJobTypes);

		// Act
		var info = new JobInstanceInfo("instance-1", "host", capabilities);

		// Assert
		info.Capabilities.ShouldBe(capabilities);
		info.Capabilities.MaxConcurrentJobs.ShouldBe(10);
	}

	[Fact]
	public void HaveRegisteredAtTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Assert
		var after = DateTimeOffset.UtcNow;
		info.RegisteredAt.ShouldBeGreaterThanOrEqualTo(before);
		info.RegisteredAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveLastHeartbeatSetToNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Assert
		var after = DateTimeOffset.UtcNow;
		info.LastHeartbeat.ShouldBeGreaterThanOrEqualTo(before);
		info.LastHeartbeat.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveActiveStatusByDefault()
	{
		// Act
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Assert
		info.Status.ShouldBe(JobInstanceStatus.Active);
	}

	[Fact]
	public void HaveZeroActiveJobCountByDefault()
	{
		// Act
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Assert
		info.ActiveJobCount.ShouldBe(0);
	}

	[Fact]
	public void HaveNullMetadataByDefault()
	{
		// Act
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Assert
		info.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingStatus()
	{
		// Arrange
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Act
		info.Status = JobInstanceStatus.Draining;

		// Assert
		info.Status.ShouldBe(JobInstanceStatus.Draining);
	}

	[Fact]
	public void AllowSettingActiveJobCount()
	{
		// Arrange
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Act
		info.ActiveJobCount = 5;

		// Assert
		info.ActiveJobCount.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingMetadata()
	{
		// Arrange
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Act
		info.Metadata = "{\"region\":\"us-west-2\"}";

		// Assert
		info.Metadata.ShouldBe("{\"region\":\"us-west-2\"}");
	}

	[Fact]
	public void UpdateHeartbeat()
	{
		// Arrange
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());
		var originalHeartbeat = info.LastHeartbeat;
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10); // Small delay to ensure time difference

		// Act
		info.UpdateHeartbeat();

		// Assert
		info.LastHeartbeat.ShouldBeGreaterThan(originalHeartbeat);
	}

	[Fact]
	public void BeHealthyWhenActiveAndRecentHeartbeat()
	{
		// Arrange
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());

		// Act & Assert
		info.IsHealthy(TimeSpan.FromMinutes(5)).ShouldBeTrue();
	}

	[Fact]
	public void BeUnhealthyWhenNotActive()
	{
		// Arrange
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());
		info.Status = JobInstanceStatus.Draining;

		// Act & Assert
		info.IsHealthy(TimeSpan.FromMinutes(5)).ShouldBeFalse();
	}

	[Fact]
	public void BeUnhealthyWhenFailed()
	{
		// Arrange
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());
		info.Status = JobInstanceStatus.Failed;

		// Act & Assert
		info.IsHealthy(TimeSpan.FromMinutes(5)).ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingLastHeartbeatDirectly()
	{
		// Arrange
		var info = new JobInstanceInfo("instance-1", "host", CreateTestCapabilities());
		var oldTime = DateTimeOffset.UtcNow.AddHours(-1);

		// Act
		info.LastHeartbeat = oldTime;

		// Assert
		info.LastHeartbeat.ShouldBe(oldTime);
		info.IsHealthy(TimeSpan.FromMinutes(5)).ShouldBeFalse();
	}
}
