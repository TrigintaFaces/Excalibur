using System.Collections.Concurrent;
using System.Security.Claims;

using Excalibur.A3.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

using AuthorizationResult = Excalibur.Dispatch.AuthorizationResult;
using MsAuthorizationResult = Microsoft.AspNetCore.Authorization.AuthorizationResult;

namespace Excalibur.Tests.A3.Authorization;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class DispatchAuthorizationServiceShould
{
	private readonly IAuthorizationService _innerService;
	private readonly RecordingLogger _logger;
	private readonly DispatchAuthorizationService _sut;

	public DispatchAuthorizationServiceShould()
	{
		_innerService = A.Fake<IAuthorizationService>();
		_logger = new RecordingLogger();
		_sut = new DispatchAuthorizationService(_innerService, _logger);
	}

	[Fact]
	public void Implement_IDispatchAuthorizationService()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IDispatchAuthorizationService>();
	}

	[Fact]
	public async Task Return_success_when_requirements_authorization_succeeds()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var requirement = A.Fake<IAuthorizationRequirement>();

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		// Act
		var result = await _sut.AuthorizeAsync(user, null, requirement);

		// Assert
		result.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_failure_when_requirements_authorization_fails()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var requirement = A.Fake<IAuthorizationRequirement>();

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.Returns(MsAuthorizationResult.Failed());

		// Act
		var result = await _sut.AuthorizeAsync(user, null, requirement);

		// Assert
		result.IsAuthorized.ShouldBeFalse();
	}

	[Fact]
	public async Task Return_success_when_policy_authorization_succeeds()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var policyName = "TestPolicy";

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		// Act
		var result = await _sut.AuthorizeAsync(user, null, policyName);

		// Assert
		result.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_failure_when_policy_authorization_fails()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var policyName = "TestPolicy";

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Failed());

		// Act
		var result = await _sut.AuthorizeAsync(user, null, policyName);

		// Assert
		result.IsAuthorized.ShouldBeFalse();
	}

	[Fact]
	public async Task Pass_resource_to_inner_service_for_requirements()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var resource = "test-resource";
		var requirement = A.Fake<IAuthorizationRequirement>();

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		// Act
		await _sut.AuthorizeAsync(user, resource, requirement);

		// Assert
		A.CallTo(() => _innerService.AuthorizeAsync(
			user,
			resource,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Pass_resource_to_inner_service_for_policy()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var resource = "test-resource";
		var policyName = "TestPolicy";

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		// Act
		await _sut.AuthorizeAsync(user, resource, policyName);

		// Assert
		A.CallTo(() => _innerService.AuthorizeAsync(
			user,
			resource,
			policyName))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Tell_the_operator_why_a_denial_happened()
	{
		// LIVENESS: the reason must survive the adaptation. Every denial previously collapsed to a bare
		// sentence with the inner service's failure detail dropped on the floor, so an operator diagnosing a
		// denial had nothing to read.
		var user = new ClaimsPrincipal(new ClaimsIdentity());

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Failed(AuthorizationFailure.Failed(
			[
				new AuthorizationFailureReason(A.Fake<IAuthorizationHandler>(), "subject lacks grant admin:tenant-7"),
			])));

		var result = await _sut.AuthorizeAsync(user, null, "TenantAdminPolicy");

		result.IsAuthorized.ShouldBeFalse();
		_logger.Recorded.ShouldContain(m => m.Contains("subject lacks grant admin:tenant-7", StringComparison.Ordinal),
			"the denial reason must reach the operator's log");
		_logger.Recorded.ShouldContain(m => m.Contains("TenantAdminPolicy", StringComparison.Ordinal),
			"the operator needs to know WHICH policy denied the request");
	}

	[Fact]
	public async Task Name_the_unmet_requirement_in_the_operator_log()
	{
		var user = new ClaimsPrincipal(new ClaimsIdentity());

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.Returns(MsAuthorizationResult.Failed(AuthorizationFailure.Failed([new TenantMatchRequirement()])));

		_ = await _sut.AuthorizeAsync(user, null, new TenantMatchRequirement());

		_logger.Recorded.ShouldContain(m => m.Contains(nameof(TenantMatchRequirement), StringComparison.Ordinal),
			"a denial with no stated reason is still diagnosable from the requirement that was not met");
	}

	[Fact]
	public async Task Not_disclose_the_reason_to_the_caller()
	{
		// SAFETY: FailureMessage is copied verbatim into the 403 problem-details Detail the remote caller
		// receives, so it must never name the policy, the requirement, or the grant that was missing --
		// that is an authorization-model oracle an unauthenticated caller can probe one request at a time.
		var user = new ClaimsPrincipal(new ClaimsIdentity());

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Failed(AuthorizationFailure.Failed(
			[
				new AuthorizationFailureReason(A.Fake<IAuthorizationHandler>(), "subject lacks grant admin:tenant-7"),
			])));

		var result = await _sut.AuthorizeAsync(user, null, "TenantAdminPolicy");

		result.IsAuthorized.ShouldBeFalse();
		result.FailureMessage.ShouldBe("Authorization failed");
		result.FailureMessage.ShouldNotContain("admin:tenant-7");
		result.FailureMessage.ShouldNotContain("TenantAdminPolicy");
	}

	[Fact]
	public async Task Say_nothing_to_the_operator_when_authorization_succeeds()
	{
		// The other half of the liveness/safety pair: an implementation that logged a denial unconditionally
		// would satisfy both arms above while reporting denials for requests it in fact allowed.
		var user = new ClaimsPrincipal(new ClaimsIdentity());

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		var result = await _sut.AuthorizeAsync(user, null, "TenantAdminPolicy");

		result.IsAuthorized.ShouldBeTrue();
		_logger.Recorded.ShouldBeEmpty();
	}

	private sealed class TenantMatchRequirement : IAuthorizationRequirement;

	private sealed class RecordingLogger : ILogger<DispatchAuthorizationService>
	{
		private readonly ConcurrentQueue<string> _records = new();

		public IReadOnlyList<string> Recorded => [.. _records];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) => _records.Enqueue(formatter(state, exception));
	}
}
