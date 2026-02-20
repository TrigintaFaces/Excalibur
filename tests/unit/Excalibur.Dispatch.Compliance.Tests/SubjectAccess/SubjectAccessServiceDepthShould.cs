using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.SubjectAccess;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SubjectAccessServiceDepthShould
{
	private readonly SubjectAccessOptions _options = new();
	private readonly NullLogger<SubjectAccessService> _logger = NullLogger<SubjectAccessService>.Instance;

	[Fact]
	public async Task Create_request_with_pending_status_when_auto_fulfill_disabled()
	{
		_options.AutoFulfill = false;

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

	[Fact]
	public async Task Create_request_with_fulfilled_status_when_auto_fulfill_enabled()
	{
		_options.AutoFulfill = true;

		var sut = CreateService();
		var request = new SubjectAccessRequest
		{
			SubjectId = "user-1",
			RequestedAt = DateTimeOffset.UtcNow,
			RequestType = SubjectAccessRequestType.Access
		};

		var result = await sut.CreateRequestAsync(request, CancellationToken.None).ConfigureAwait(false);

		result.Status.ShouldBe(SubjectAccessRequestStatus.Fulfilled);
		result.FulfilledAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Create_request_sets_deadline_from_options()
	{
		_options.ResponseDeadlineDays = 30;
		_options.AutoFulfill = false;

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
		_options.AutoFulfill = false;
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
		_options.AutoFulfill = false;
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
		_options.AutoFulfill = false;
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
