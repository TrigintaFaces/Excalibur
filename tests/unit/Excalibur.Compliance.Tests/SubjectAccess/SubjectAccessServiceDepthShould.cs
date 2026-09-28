using Excalibur.Compliance;
using Excalibur.Compliance.SubjectAccess;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.SubjectAccess;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SubjectAccessServiceDepthShould
{
	private readonly SubjectAccessOptions _options = new();
	private readonly NullLogger<SubjectAccessService> _logger = NullLogger<SubjectAccessService>.Instance;

	[Fact]
	public async Task Create_request_with_pending_status_when_auto_fulfill_disabled()
	{

		var sut = CreateService();
		var request = new SubjectAccessRequest
		{
			SubjectId = "user-1",
			RequestedAt = DateTimeOffset.UtcNow,
			RequestType = SubjectAccessRequestType.Access
		};

		var result = await sut.CreateRequestAsync(request, CancellationToken.None).ConfigureAwait(false);

		result.Status.ShouldBe(SubjectAccessRequestStatus.Pending);
		result.FulfilledAt.ShouldBeNull();
		result.RequestId.ShouldNotBeNullOrEmpty();
	}

	/// <summary>
	/// A created request is PENDING, and only an explicit fulfilment call can move it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm replaces one that certified the defect.</b> It read
	/// <c>Create_request_with_fulfilled_status_when_auto_fulfill_enabled</c>, set the AutoFulfill option,
	/// and asserted the request came back <c>Fulfilled</c> with a <c>FulfilledAt</c> timestamp — at the
	/// instant of creation, with nothing gathered and nothing sent. It passed, and what it pinned was a
	/// configuration flag declaring a GDPR Article 15 obligation discharged.
	/// </para>
	/// <para>
	/// The option is gone, so the arm is flipped rather than deleted: fulfilment is an ACT, and the only
	/// thing that may record it is the call that performs it.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Create_a_request_as_pending_and_fulfil_it_only_when_told_to()
	{
		var sut = CreateService();
		var request = new SubjectAccessRequest
		{
			SubjectId = "user-1",
			RequestedAt = DateTimeOffset.UtcNow,
			RequestType = SubjectAccessRequestType.Access
		};

		var created = await sut.CreateRequestAsync(request, CancellationToken.None).ConfigureAwait(false);

		created.Status.ShouldBe(
			SubjectAccessRequestStatus.Pending,
			"a request is pending when it is created. No configuration may declare it otherwise, because "
			+ "nothing has been gathered or sent at that moment.");
		created.FulfilledAt.ShouldBeNull("nothing was fulfilled, so there is no time at which it was");

		var fulfilled = await sut.FulfillRequestAsync(created.RequestId, CancellationToken.None)
			.ConfigureAwait(false);

		fulfilled.Status.ShouldBe(
			SubjectAccessRequestStatus.Fulfilled,
			"the explicit call is what records fulfilment, and it must still work -- otherwise this "
			+ "change would have removed the lie and the capability together");
		fulfilled.FulfilledAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Create_request_sets_deadline_from_options()
	{
		_options.ResponseDeadlineDays = 30;

		var sut = CreateService();
		var requestedAt = DateTimeOffset.UtcNow;
		var request = new SubjectAccessRequest
		{
			SubjectId = "user-1",
			RequestedAt = requestedAt,
			RequestType = SubjectAccessRequestType.Access
		};

		var result = await sut.CreateRequestAsync(request, CancellationToken.None).ConfigureAwait(false);

		result.Deadline.ShouldNotBeNull();
		var expectedDeadline = requestedAt.AddDays(30);
		result.Deadline!.Value.ShouldBeGreaterThanOrEqualTo(expectedDeadline.AddSeconds(-1));
		result.Deadline.Value.ShouldBeLessThanOrEqualTo(expectedDeadline.AddSeconds(1));
	}

	[Fact]
	public async Task Get_request_status_returns_null_for_unknown()
	{
		var sut = CreateService();

		var result = await sut.GetRequestStatusAsync("nonexistent", CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task Get_request_status_returns_existing_request()
	{
		var sut = CreateService();
		var request = new SubjectAccessRequest
		{
			SubjectId = "user-1",
			RequestedAt = DateTimeOffset.UtcNow,
			RequestType = SubjectAccessRequestType.Access
		};
		var created = await sut.CreateRequestAsync(request, CancellationToken.None).ConfigureAwait(false);

		var status = await sut.GetRequestStatusAsync(created.RequestId, CancellationToken.None).ConfigureAwait(false);

		status.ShouldNotBeNull();
		status.RequestId.ShouldBe(created.RequestId);
	}

	[Fact]
	public async Task Fulfill_request_changes_status_to_fulfilled()
	{
		var sut = CreateService();
		var request = new SubjectAccessRequest
		{
			SubjectId = "user-1",
			RequestedAt = DateTimeOffset.UtcNow,
			RequestType = SubjectAccessRequestType.Access
		};
		var created = await sut.CreateRequestAsync(request, CancellationToken.None).ConfigureAwait(false);

		var fulfilled = await sut.FulfillRequestAsync(created.RequestId, CancellationToken.None).ConfigureAwait(false);

		fulfilled.Status.ShouldBe(SubjectAccessRequestStatus.Fulfilled);
		fulfilled.FulfilledAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Throw_when_fulfilling_unknown_request()
	{
		var sut = CreateService();

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.FulfillRequestAsync("nonexistent", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_fulfilling_already_fulfilled_request()
	{
		var sut = CreateService();
		var request = new SubjectAccessRequest
		{
			SubjectId = "user-1",
			RequestedAt = DateTimeOffset.UtcNow,
			RequestType = SubjectAccessRequestType.Access
		};
		var created = await sut.CreateRequestAsync(request, CancellationToken.None).ConfigureAwait(false);
		await sut.FulfillRequestAsync(created.RequestId, CancellationToken.None).ConfigureAwait(false);

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.FulfillRequestAsync(created.RequestId, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_request()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.CreateRequestAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_or_whitespace_request_id_in_status()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetRequestStatusAsync(null!, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetRequestStatusAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_or_whitespace_request_id_in_fulfill()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.FulfillRequestAsync(null!, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.FulfillRequestAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_options_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new SubjectAccessService(null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new SubjectAccessService(
				Microsoft.Extensions.Options.Options.Create(_options), null!));
	}

	private SubjectAccessService CreateService() =>
		new(Microsoft.Extensions.Options.Options.Create(_options), _logger);
}
