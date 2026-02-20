using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.SubjectAccess;

/// <summary>
/// Tests the subject access service lifecycle workflows including
/// request types, auto-fulfill, and concurrent request management.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SubjectAccessServiceWorkflowShould
{
	[Fact]
	public async Task Create_access_request_with_unique_id()
	{
		// Arrange
		var sut = CreateService();

		// Act
		var request1 = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Access },
			CancellationToken.None);
		var request2 = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Access },
			CancellationToken.None);

		// Assert - each request should get a unique ID
		request1.RequestId.ShouldNotBe(request2.RequestId);
	}

	[Fact]
	public async Task Support_multiple_request_types()
	{
		// Arrange
		var sut = CreateService();

		// Act
		var access = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Access },
			CancellationToken.None);
		var erasure = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Erasure },
			CancellationToken.None);
		var rectification = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Rectification },
			CancellationToken.None);

		// Assert
		access.Status.ShouldBe(SubjectAccessRequestStatus.Pending);
		erasure.Status.ShouldBe(SubjectAccessRequestStatus.Pending);
		rectification.Status.ShouldBe(SubjectAccessRequestStatus.Pending);
	}

	[Fact]
	public async Task Auto_fulfill_creates_fulfilled_request()
	{
		// Arrange
		var options = new SubjectAccessOptions { AutoFulfill = true };
		var sut = CreateService(options);

		// Act
		var result = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Access },
			CancellationToken.None);

		// Assert
		result.Status.ShouldBe(SubjectAccessRequestStatus.Fulfilled);
		result.FulfilledAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Fulfill_pending_request_manually()
	{
		// Arrange
		var sut = CreateService();
		var created = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Access },
			CancellationToken.None);

		// Act
		var fulfilled = await sut.FulfillRequestAsync(created.RequestId, CancellationToken.None);

		// Assert
		fulfilled.Status.ShouldBe(SubjectAccessRequestStatus.Fulfilled);
		fulfilled.FulfilledAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Prevent_double_fulfillment()
	{
		// Arrange
		var sut = CreateService();
		var created = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Access },
			CancellationToken.None);
		await sut.FulfillRequestAsync(created.RequestId, CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.FulfillRequestAsync(created.RequestId, CancellationToken.None));
	}

	[Fact]
	public async Task Maintain_deadline_on_created_request()
	{
		// Arrange
		var options = new SubjectAccessOptions { ResponseDeadlineDays = 30 };
		var sut = CreateService(options);

		// Act
		var result = await sut.CreateRequestAsync(
			new SubjectAccessRequest
			{
				SubjectId = "u1",
				RequestType = SubjectAccessRequestType.Access,
				RequestedAt = DateTimeOffset.UtcNow,
			},
			CancellationToken.None);

		// Assert
		result.Deadline.ShouldNotBeNull();
	}

	[Fact]
	public async Task Track_request_status_after_creation()
	{
		// Arrange
		var sut = CreateService();
		var created = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Access },
			CancellationToken.None);

		// Act
		var status = await sut.GetRequestStatusAsync(created.RequestId, CancellationToken.None);

		// Assert
		status.ShouldNotBeNull();
		status.RequestId.ShouldBe(created.RequestId);
		status.Status.ShouldBe(SubjectAccessRequestStatus.Pending);
	}

	[Fact]
	public async Task Track_request_status_after_fulfillment()
	{
		// Arrange
		var sut = CreateService();
		var created = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "u1", RequestType = SubjectAccessRequestType.Erasure },
			CancellationToken.None);
		await sut.FulfillRequestAsync(created.RequestId, CancellationToken.None);

		// Act
		var status = await sut.GetRequestStatusAsync(created.RequestId, CancellationToken.None);

		// Assert
		status.ShouldNotBeNull();
		status.Status.ShouldBe(SubjectAccessRequestStatus.Fulfilled);
		status.FulfilledAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Handle_multiple_subjects_independently()
	{
		// Arrange
		var sut = CreateService();
		var r1 = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "user-A", RequestType = SubjectAccessRequestType.Access },
			CancellationToken.None);
		var r2 = await sut.CreateRequestAsync(
			new SubjectAccessRequest { SubjectId = "user-B", RequestType = SubjectAccessRequestType.Erasure },
			CancellationToken.None);

		// Act - fulfill only first
		await sut.FulfillRequestAsync(r1.RequestId, CancellationToken.None);

		// Assert
		var s1 = await sut.GetRequestStatusAsync(r1.RequestId, CancellationToken.None);
		var s2 = await sut.GetRequestStatusAsync(r2.RequestId, CancellationToken.None);
		s1!.Status.ShouldBe(SubjectAccessRequestStatus.Fulfilled);
		s2!.Status.ShouldBe(SubjectAccessRequestStatus.Pending);
	}

	private static SubjectAccessService CreateService(SubjectAccessOptions? options = null) =>
		new(
			Microsoft.Extensions.Options.Options.Create(options ?? new SubjectAccessOptions()),
			NullLogger<SubjectAccessService>.Instance);
}
