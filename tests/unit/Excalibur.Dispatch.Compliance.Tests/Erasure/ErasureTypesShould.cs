using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureRequestShould
{
	[Fact]
	public void Have_expected_defaults()
	{
		var lowerBound = DateTimeOffset.UtcNow;

		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin"
		};
		var upperBound = DateTimeOffset.UtcNow;

		request.RequestId.ShouldNotBe(Guid.Empty);
		request.Scope.ShouldBe(ErasureScope.User);
		request.TenantId.ShouldBeNull();
		request.ExternalReference.ShouldBeNull();
		request.GracePeriodOverride.ShouldBeNull();
		request.DataCategories.ShouldBeNull();
		request.Metadata.ShouldBeNull();
		request.RequestedAt.ShouldBeGreaterThanOrEqualTo(lowerBound);
		request.RequestedAt.ShouldBeLessThanOrEqualTo(upperBound);
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.Email,
			TenantId = "tenant-1",
			Scope = ErasureScope.Selective,
			LegalBasis = ErasureLegalBasis.RightToObject,
			ExternalReference = "TICKET-42",
			RequestedBy = "admin",
			GracePeriodOverride = TimeSpan.FromHours(24),
			DataCategories = ["profile", "orders"],
			Metadata = new Dictionary<string, string> { ["channel"] = "web" }
		};

		request.IdType.ShouldBe(DataSubjectIdType.Email);
		request.Scope.ShouldBe(ErasureScope.Selective);
		request.DataCategories!.Count.ShouldBe(2);
		request.GracePeriodOverride.ShouldBe(TimeSpan.FromHours(24));
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureResultShould
{
	[Fact]
	public void Create_scheduled_result()
	{
		var requestId = Guid.NewGuid();
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(72);

		var result = ErasureResult.Scheduled(requestId, scheduledTime);

		result.RequestId.ShouldBe(requestId);
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
		result.ScheduledExecutionTime.ShouldBe(scheduledTime);
		result.EstimatedCompletionTime.ShouldBe(scheduledTime.AddMinutes(5));
	}

	[Fact]
	public void Create_blocked_result()
	{
		var requestId = Guid.NewGuid();
		var hold = new LegalHoldInfo
		{
			HoldId = Guid.NewGuid(),
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-001",
			CreatedAt = DateTimeOffset.UtcNow
		};

		var result = ErasureResult.Blocked(requestId, hold);

		result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
		result.BlockingHold.ShouldBeSameAs(hold);
		result.Message.ShouldContain("CASE-001");
	}

	[Fact]
	public void Create_failed_result()
	{
		var requestId = Guid.NewGuid();

		var result = ErasureResult.Failed(requestId, "Key deletion failed");

		result.Status.ShouldBe(ErasureRequestStatus.Failed);
		result.Message.ShouldBe("Key deletion failed");
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureStatusShould
{
	[Theory]
	[InlineData(ErasureRequestStatus.Pending, true)]
	[InlineData(ErasureRequestStatus.Scheduled, true)]
	[InlineData(ErasureRequestStatus.InProgress, false)]
	[InlineData(ErasureRequestStatus.Completed, false)]
	[InlineData(ErasureRequestStatus.Failed, false)]
	public void Report_cancellability_correctly(ErasureRequestStatus status, bool canCancel)
	{
		var erasureStatus = new ErasureStatus
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Status = status,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		};

		erasureStatus.CanCancel.ShouldBe(canCancel);
	}

	[Theory]
	[InlineData(ErasureRequestStatus.Completed, true)]
	[InlineData(ErasureRequestStatus.PartiallyCompleted, true)]
	[InlineData(ErasureRequestStatus.InProgress, false)]
	[InlineData(ErasureRequestStatus.Pending, false)]
	public void Report_execution_status_correctly(ErasureRequestStatus status, bool isExecuted)
	{
		var erasureStatus = new ErasureStatus
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Status = status,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		};

		erasureStatus.IsExecuted.ShouldBe(isExecuted);
	}

	[Fact]
	public void Calculate_days_until_deadline()
	{
		var erasureStatus = new ErasureStatus
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Status = ErasureRequestStatus.Scheduled,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		};

		erasureStatus.DaysUntilDeadline.ShouldBeInRange(29, 30);
	}

	[Fact]
	public void Return_zero_days_when_past_deadline()
	{
		var erasureStatus = new ErasureStatus
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Status = ErasureRequestStatus.Scheduled,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow.AddDays(-60),
			UpdatedAt = DateTimeOffset.UtcNow
		};

		erasureStatus.DaysUntilDeadline.ShouldBe(0);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureRequestStatusShould
{
	[Theory]
	[InlineData(ErasureRequestStatus.Pending, 0)]
	[InlineData(ErasureRequestStatus.Scheduled, 1)]
	[InlineData(ErasureRequestStatus.InProgress, 2)]
	[InlineData(ErasureRequestStatus.Completed, 3)]
	[InlineData(ErasureRequestStatus.BlockedByLegalHold, 4)]
	[InlineData(ErasureRequestStatus.Cancelled, 5)]
	[InlineData(ErasureRequestStatus.Failed, 6)]
	[InlineData(ErasureRequestStatus.PartiallyCompleted, 7)]
	public void Have_expected_integer_values(ErasureRequestStatus status, int expectedValue)
	{
		((int)status).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have_exactly_eight_values()
	{
		Enum.GetValues<ErasureRequestStatus>().Length.ShouldBe(8);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DataSubjectIdTypeShould
{
	[Theory]
	[InlineData(DataSubjectIdType.UserId, 0)]
	[InlineData(DataSubjectIdType.Email, 1)]
	[InlineData(DataSubjectIdType.ExternalId, 2)]
	[InlineData(DataSubjectIdType.NationalId, 3)]
	[InlineData(DataSubjectIdType.Hash, 4)]
	[InlineData(DataSubjectIdType.Custom, 99)]
	public void Have_expected_integer_values(DataSubjectIdType type, int expectedValue)
	{
		((int)type).ShouldBe(expectedValue);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureScopeShould
{
	[Theory]
	[InlineData(ErasureScope.User, 0)]
	[InlineData(ErasureScope.Tenant, 1)]
	[InlineData(ErasureScope.Selective, 2)]
	public void Have_expected_integer_values(ErasureScope scope, int expectedValue)
	{
		((int)scope).ShouldBe(expectedValue);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureLegalBasisShould
{
	[Fact]
	public void Have_exactly_seven_values()
	{
		Enum.GetValues<ErasureLegalBasis>().Length.ShouldBe(7);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class LegalHoldBasisShould
{
	[Fact]
	public void Have_exactly_seven_values()
	{
		Enum.GetValues<LegalHoldBasis>().Length.ShouldBe(7);
	}
}
