// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Application.Requests.Jobs;

[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class JobBaseShould
{
	private sealed class TestJob : JobBase
	{
		public TestJob() { }
		public TestJob(Guid correlationId, string? tenantId = null)
			: base(correlationId, tenantId) { }
	}

	[Fact]
	public void HaveUniqueId()
	{
		var job = new TestJob();
		job.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void ReturnIdAsMessageId()
	{
		var job = new TestJob();
		job.MessageId.ShouldBe(job.Id.ToString());
	}

	[Fact]
	public void ReturnTypeAsMessageType()
	{
		var job = new TestJob();
		job.MessageType.ShouldContain("TestJob");
	}

	[Fact]
	public void DefaultToActionKind()
	{
		var job = new TestJob();
		job.Kind.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void HaveEmptyHeaders()
	{
		var job = new TestJob();
		job.Headers.ShouldNotBeNull();
		job.Headers.Count.ShouldBe(0);
	}

	[Fact]
	public void ReturnJobActivityType()
	{
		var job = new TestJob();
		job.ActivityType.ShouldBe(ActivityType.Job);
	}

	[Fact]
	public void ResolveActivityName()
	{
		var job = new TestJob();
		job.ActivityName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ResolveActivityDisplayName()
	{
		var job = new TestJob();
		job.ActivityDisplayName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ResolveActivityDescription()
	{
		var job = new TestJob();
		job.ActivityDescription.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AcceptCorrelationId()
	{
		var correlationId = Guid.NewGuid();
		var job = new TestJob(correlationId);
		job.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void AcceptTenantId()
	{
		var job = new TestJob(Guid.NewGuid(), "tenant-1");
		job.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void DefaultTenantId_WhenNotProvided()
	{
		var job = new TestJob(Guid.NewGuid());
		job.TenantId.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultConstructor_SetsEmptyCorrelationId()
	{
		var job = new TestJob();
		job.CorrelationId.ShouldBe(Guid.Empty);
	}

	[Fact]
	public void ProduceDifferentIdsForDifferentInstances()
	{
		var job1 = new TestJob();
		var job2 = new TestJob();
		job1.Id.ShouldNotBe(job2.Id);
	}
}
