// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Auditing;

namespace Excalibur.Tests.A3.Abstractions.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditEventShould
{
	private static readonly DateTimeOffset TestTimestamp = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

	[Fact]
	public void StoreAllRequiredProperties()
	{
		var evt = new AuditEvent(
			TestTimestamp, "tenant-1", "user-42", "CreateOrder", "/orders/123", "Success");

		evt.TimestampUtc.ShouldBe(TestTimestamp);
		evt.TenantId.ShouldBe("tenant-1");
		evt.ActorId.ShouldBe("user-42");
		evt.Action.ShouldBe("CreateOrder");
		evt.Resource.ShouldBe("/orders/123");
		evt.Outcome.ShouldBe("Success");
	}

	[Fact]
	public void DefaultCorrelationIdToNull()
	{
		var evt = new AuditEvent(
			TestTimestamp, "tenant-1", "user-42", "CreateOrder", "/orders/123", "Success");

		evt.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void DefaultAttributesToNull()
	{
		var evt = new AuditEvent(
			TestTimestamp, "tenant-1", "user-42", "CreateOrder", "/orders/123", "Success");

		evt.Attributes.ShouldBeNull();
	}

	[Fact]
	public void StoreOptionalCorrelationId()
	{
		var evt = new AuditEvent(
			TestTimestamp, "tenant-1", "user-42", "CreateOrder", "/orders/123", "Success",
			correlationId: "corr-abc");

		evt.CorrelationId.ShouldBe("corr-abc");
	}

	[Fact]
	public void StoreOptionalAttributes()
	{
		var attrs = new Dictionary<string, string> { ["ip"] = "192.168.1.1", ["source"] = "api" };
		var evt = new AuditEvent(
			TestTimestamp, "tenant-1", "user-42", "CreateOrder", "/orders/123", "Success",
			attributes: attrs);

		evt.Attributes.ShouldNotBeNull();
		evt.Attributes.ShouldContainKeyAndValue("ip", "192.168.1.1");
		evt.Attributes.ShouldContainKeyAndValue("source", "api");
	}

	[Fact]
	public void ImplementIAuditEvent()
	{
		var evt = new AuditEvent(
			TestTimestamp, "tenant-1", "user-42", "CreateOrder", "/orders/123", "Success");

		evt.ShouldBeAssignableTo<IAuditEvent>();
	}

	[Fact]
	public void StoreAllParametersIncludingOptional()
	{
		var attrs = new Dictionary<string, string> { ["key"] = "val" };
		var evt = new AuditEvent(
			TestTimestamp, "tenant-1", "actor-1", "DeleteItem", "/items/99", "Denied",
			correlationId: "corr-xyz", attributes: attrs);

		evt.TimestampUtc.ShouldBe(TestTimestamp);
		evt.TenantId.ShouldBe("tenant-1");
		evt.ActorId.ShouldBe("actor-1");
		evt.Action.ShouldBe("DeleteItem");
		evt.Resource.ShouldBe("/items/99");
		evt.Outcome.ShouldBe("Denied");
		evt.CorrelationId.ShouldBe("corr-xyz");
		evt.Attributes.ShouldNotBeNull();
		evt.Attributes.Count.ShouldBe(1);
	}
}
