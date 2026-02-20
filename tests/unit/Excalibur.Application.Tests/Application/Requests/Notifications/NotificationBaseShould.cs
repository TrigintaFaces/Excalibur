// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Transactions;

using Excalibur.Application.Requests.Notifications;

namespace Excalibur.Tests.Application.Requests.Notifications;

[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class NotificationBaseShould
{
	private sealed class TestNotification : NotificationBase
	{
		public TestNotification() { }
		public TestNotification(Guid correlationId, string? tenantId = null)
			: base(correlationId, tenantId) { }
	}

	[Fact]
	public void HaveUniqueId()
	{
		var notification = new TestNotification();
		notification.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void ReturnIdAsMessageId()
	{
		var notification = new TestNotification();
		notification.MessageId.ShouldBe(notification.Id.ToString());
	}

	[Fact]
	public void ReturnTypeAsMessageType()
	{
		var notification = new TestNotification();
		notification.MessageType.ShouldContain("TestNotification");
	}

	[Fact]
	public void DefaultToEventKind()
	{
		var notification = new TestNotification();
		notification.Kind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void HaveEmptyHeaders()
	{
		var notification = new TestNotification();
		notification.Headers.ShouldNotBeNull();
		notification.Headers.Count.ShouldBe(0);
	}

	[Fact]
	public void ReturnNotificationActivityType()
	{
		var notification = new TestNotification();
		notification.ActivityType.ShouldBe(ActivityType.Notification);
	}

	[Fact]
	public void ResolveActivityName()
	{
		var notification = new TestNotification();
		notification.ActivityName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ResolveActivityDisplayName()
	{
		var notification = new TestNotification();
		notification.ActivityDisplayName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ResolveActivityDescription()
	{
		var notification = new TestNotification();
		notification.ActivityDescription.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AcceptCorrelationId()
	{
		var correlationId = Guid.NewGuid();
		var notification = new TestNotification(correlationId);
		notification.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void AcceptTenantId()
	{
		var notification = new TestNotification(Guid.NewGuid(), "tenant-1");
		notification.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void DefaultTenantId_WhenNotProvided()
	{
		var notification = new TestNotification(Guid.NewGuid());
		notification.TenantId.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultTransactionBehavior()
	{
		var notification = new TestNotification();
		notification.TransactionBehavior.ShouldBe(TransactionScopeOption.Required);
	}

	[Fact]
	public void DefaultTransactionIsolation()
	{
		var notification = new TestNotification();
		notification.TransactionIsolation.ShouldBe(IsolationLevel.ReadCommitted);
	}

	[Fact]
	public void DefaultTransactionTimeout()
	{
		var notification = new TestNotification();
		notification.TransactionTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void DefaultConstructor_SetsEmptyCorrelationId()
	{
		var notification = new TestNotification();
		notification.CorrelationId.ShouldBe(Guid.Empty);
	}
}
