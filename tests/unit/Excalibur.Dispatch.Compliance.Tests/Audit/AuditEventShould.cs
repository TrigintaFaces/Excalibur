using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditEventShould
{
	[Fact]
	public void Store_all_required_properties()
	{
		var timestamp = DateTimeOffset.UtcNow;

		var evt = new AuditEvent
		{
			EventId = "evt-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = "user-42"
		};

		evt.EventId.ShouldBe("evt-1");
		evt.EventType.ShouldBe(AuditEventType.DataAccess);
		evt.Action.ShouldBe("Read");
		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.Timestamp.ShouldBe(timestamp);
		evt.ActorId.ShouldBe("user-42");
	}

	[Fact]
	public void Have_null_optional_properties_by_default()
	{
		var evt = new AuditEvent
		{
			EventId = "evt-1",
			EventType = AuditEventType.System,
			Action = "Start",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "system"
		};

		evt.ActorType.ShouldBeNull();
		evt.ResourceId.ShouldBeNull();
		evt.ResourceType.ShouldBeNull();
		evt.ResourceClassification.ShouldBeNull();
		evt.TenantId.ShouldBeNull();
		evt.CorrelationId.ShouldBeNull();
		evt.SessionId.ShouldBeNull();
		evt.IpAddress.ShouldBeNull();
		evt.UserAgent.ShouldBeNull();
		evt.Reason.ShouldBeNull();
		evt.Metadata.ShouldBeNull();
		evt.PreviousEventHash.ShouldBeNull();
		evt.EventHash.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_all_optional_properties()
	{
		var metadata = new Dictionary<string, string> { ["key"] = "value" };

		var evt = new AuditEvent
		{
			EventId = "evt-2",
			EventType = AuditEventType.Authentication,
			Action = "Login",
			Outcome = AuditOutcome.Denied,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			ActorType = "User",
			ResourceId = "res-1",
			ResourceType = "Customer",
			ResourceClassification = DataClassification.Restricted,
			TenantId = "tenant-1",
			CorrelationId = "corr-1",
			SessionId = "sess-1",
			IpAddress = "10.0.0.1",
			UserAgent = "TestAgent/1.0",
			Reason = "failed-mfa",
			Metadata = metadata,
			PreviousEventHash = "abc123",
			EventHash = "def456"
		};

		evt.ActorType.ShouldBe("User");
		evt.ResourceId.ShouldBe("res-1");
		evt.ResourceType.ShouldBe("Customer");
		evt.ResourceClassification.ShouldBe(DataClassification.Restricted);
		evt.TenantId.ShouldBe("tenant-1");
		evt.CorrelationId.ShouldBe("corr-1");
		evt.SessionId.ShouldBe("sess-1");
		evt.IpAddress.ShouldBe("10.0.0.1");
		evt.UserAgent.ShouldBe("TestAgent/1.0");
		evt.Reason.ShouldBe("failed-mfa");
		evt.Metadata.ShouldBeSameAs(metadata);
		evt.PreviousEventHash.ShouldBe("abc123");
		evt.EventHash.ShouldBe("def456");
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditEventIdShould
{
	[Fact]
	public void Store_all_required_properties()
	{
		var recordedAt = DateTimeOffset.UtcNow;

		var id = new AuditEventId
		{
			EventId = "evt-1",
			EventHash = "hash-abc",
			SequenceNumber = 42,
			RecordedAt = recordedAt
		};

		id.EventId.ShouldBe("evt-1");
		id.EventHash.ShouldBe("hash-abc");
		id.SequenceNumber.ShouldBe(42);
		id.RecordedAt.ShouldBe(recordedAt);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditEventTypeShould
{
	[Theory]
	[InlineData(AuditEventType.System, 0)]
	[InlineData(AuditEventType.Authentication, 1)]
	[InlineData(AuditEventType.Authorization, 2)]
	[InlineData(AuditEventType.DataAccess, 3)]
	[InlineData(AuditEventType.DataModification, 4)]
	[InlineData(AuditEventType.ConfigurationChange, 5)]
	[InlineData(AuditEventType.Security, 6)]
	[InlineData(AuditEventType.Compliance, 7)]
	[InlineData(AuditEventType.Administrative, 8)]
	[InlineData(AuditEventType.Integration, 9)]
	public void Have_expected_integer_values(AuditEventType type, int expectedValue)
	{
		((int)type).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have_exactly_ten_values()
	{
		Enum.GetValues<AuditEventType>().Length.ShouldBe(10);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditOutcomeShould
{
	[Theory]
	[InlineData(AuditOutcome.Success, 0)]
	[InlineData(AuditOutcome.Failure, 1)]
	[InlineData(AuditOutcome.Denied, 2)]
	[InlineData(AuditOutcome.Error, 3)]
	[InlineData(AuditOutcome.Pending, 4)]
	public void Have_expected_integer_values(AuditOutcome outcome, int expectedValue)
	{
		((int)outcome).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have_exactly_five_values()
	{
		Enum.GetValues<AuditOutcome>().Length.ShouldBe(5);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditIntegrityResultShould
{
	[Fact]
	public void Create_valid_result()
	{
		var start = DateTimeOffset.UtcNow.AddDays(-7);
		var end = DateTimeOffset.UtcNow;

		var result = AuditIntegrityResult.Valid(1000, start, end);

		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(1000);
		result.StartDate.ShouldBe(start);
		result.EndDate.ShouldBe(end);
		result.VerifiedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(-5));
		result.FirstViolationEventId.ShouldBeNull();
		result.ViolationDescription.ShouldBeNull();
		result.ViolationCount.ShouldBe(0);
	}

	[Fact]
	public void Create_invalid_result()
	{
		var start = DateTimeOffset.UtcNow.AddDays(-7);
		var end = DateTimeOffset.UtcNow;

		var result = AuditIntegrityResult.Invalid(
			500, start, end, "evt-42", "Hash mismatch", 3);

		result.IsValid.ShouldBeFalse();
		result.EventsVerified.ShouldBe(500);
		result.FirstViolationEventId.ShouldBe("evt-42");
		result.ViolationDescription.ShouldBe("Hash mismatch");
		result.ViolationCount.ShouldBe(3);
	}

	[Fact]
	public void Default_violation_count_to_one_when_invalid()
	{
		var start = DateTimeOffset.UtcNow.AddDays(-1);
		var end = DateTimeOffset.UtcNow;

		var result = AuditIntegrityResult.Invalid(
			100, start, end, "evt-1", "Tampered");

		result.ViolationCount.ShouldBe(1);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditQueryShould
{
	[Fact]
	public void Have_expected_defaults()
	{
		var query = new AuditQuery();

		query.StartDate.ShouldBeNull();
		query.EndDate.ShouldBeNull();
		query.EventTypes.ShouldBeNull();
		query.Outcomes.ShouldBeNull();
		query.ActorId.ShouldBeNull();
		query.ResourceId.ShouldBeNull();
		query.ResourceType.ShouldBeNull();
		query.MinimumClassification.ShouldBeNull();
		query.TenantId.ShouldBeNull();
		query.CorrelationId.ShouldBeNull();
		query.Action.ShouldBeNull();
		query.IpAddress.ShouldBeNull();
		query.MaxResults.ShouldBe(100);
		query.Skip.ShouldBe(0);
		query.OrderByDescending.ShouldBeTrue();
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var start = DateTimeOffset.UtcNow.AddDays(-7);
		var end = DateTimeOffset.UtcNow;

		var query = new AuditQuery
		{
			StartDate = start,
			EndDate = end,
			EventTypes = [AuditEventType.Security, AuditEventType.Authentication],
			Outcomes = [AuditOutcome.Failure],
			ActorId = "user-1",
			ResourceId = "res-1",
			ResourceType = "Customer",
			MinimumClassification = DataClassification.Confidential,
			TenantId = "tenant-1",
			CorrelationId = "corr-1",
			Action = "Login",
			IpAddress = "10.0.0.1",
			MaxResults = 50,
			Skip = 10,
			OrderByDescending = false
		};

		query.StartDate.ShouldBe(start);
		query.EndDate.ShouldBe(end);
		query.EventTypes!.Count.ShouldBe(2);
		query.Outcomes!.Count.ShouldBe(1);
		query.ActorId.ShouldBe("user-1");
		query.MaxResults.ShouldBe(50);
		query.Skip.ShouldBe(10);
		query.OrderByDescending.ShouldBeFalse();
	}
}
