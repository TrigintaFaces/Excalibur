using Excalibur.A3;
using Excalibur.A3.Audit;
using Excalibur.A3.Audit.Events;
using Excalibur.A3.Audit.Requests;
using Excalibur.A3.Authentication;
using Excalibur.Application.Requests.Jobs;
using Excalibur.Core;
using Excalibur.Data.Outbox;
using Excalibur.Domain;
using Excalibur.Tests.Mothers.Core;

using FakeItEasy;

using Microsoft.Extensions.Logging;

using Shouldly;

namespace Excalibur.Tests.Unit.A3.Audit;

public sealed class AuditBehaviorShould : SharedApplicationContextTestBase
{
	private readonly IActivityContext _activityContext;
	private readonly IAuditMessagePublisher _auditMessagePublisher;
	private readonly IOutbox _outbox;

	public AuditBehaviorShould()
	{
		_activityContext = CreateMockActivityContext();
		_auditMessagePublisher = A.Fake<IAuditMessagePublisher>();
		_outbox = A.Fake<IOutbox>();

		ApplicationContextMother.SetValue("ApplicationName", "TestApp");
	}

	[Fact]
	public async Task ProcessAndAuditAuditableRequest()
	{
		// Arrange
		var logger = A.Fake<ILogger<AuditBehavior<TestAuditableRequest, string>>>();
		var behavior = new AuditBehavior<TestAuditableRequest, string>(
			_activityContext, _auditMessagePublisher, logger, _outbox);

		var request = new TestAuditableRequest();

		async Task<string> NextDelegate() => await Task.FromResult("Success").ConfigureAwait(true);

		// Act
		var result = await behavior.Handle(request, NextDelegate, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe("Success");

		// Verify audit was published
		_ = A.CallTo(() => _auditMessagePublisher.PublishAsync(
				A<ActivityAudited>.That.Matches(a =>
					a.ActivityName == nameof(TestAuditableRequest) &&
					a.StatusCode == 200 &&
					a.UserId == "user1"),
				_activityContext))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipAuditForNonAuditableRequest()
	{
		// Arrange Create a logger for the specific generic type
		var logger = A.Fake<ILogger<AuditBehavior<TestNonAuditableRequest, string>>>();

		// Create the behavior with the correct generic type
		var behavior = new AuditBehavior<TestNonAuditableRequest, string>(
			_activityContext, _auditMessagePublisher, logger, _outbox);

		var request = new TestNonAuditableRequest();

		async Task<string> NextDelegate() => await Task.FromResult("Success").ConfigureAwait(true);

		// Act
		var result = await behavior.Handle(request, NextDelegate, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe("Success");

		// Verify audit was NOT published
		A.CallTo(() => _auditMessagePublisher.PublishAsync(
				A<IActivityAudited>._, A<IActivityContext>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SaveAuditToOutboxWhenPublishingFails()
	{
		// Arrange
		var logger = A.Fake<ILogger<AuditBehavior<TestAuditableRequest, string>>>();
		var behavior = new AuditBehavior<TestAuditableRequest, string>(
			_activityContext, _auditMessagePublisher, logger, _outbox);

		var request = new TestAuditableRequest();

		async Task<string> NextDelegate() => await Task.FromResult("Success").ConfigureAwait(true);

		// Setup publishing to fail
		_ = A.CallTo(() => _auditMessagePublisher.PublishAsync(A<ActivityAudited>._, A<IActivityContext>._))
			.Throws(new InvalidOperationException("Publishing failed"));

		// Act
		var result = await behavior.Handle(request, NextDelegate, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe("Success");

		// Verify outbox was used
		_ = A.CallTo(() => _outbox.SaveMessagesAsync(
				A<IList<OutboxMessage>>.That.Matches(msgs =>
					msgs.Count == 1 &&
					msgs[0].MessageHeaders.ContainsKey(ExcaliburHeaderNames.CorrelationId))))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipAuditForJobsWithNoWorkPerformed()
	{
		// Arrange Create a specific logger for this generic type instantiation
		var logger = A.Fake<ILogger<AuditBehavior<TestAuditableRequest, JobResult>>>();

		// Create the behavior with matching generic type parameters
		var behavior = new AuditBehavior<TestAuditableRequest, JobResult>(
			_activityContext, _auditMessagePublisher, logger, _outbox);

		var request = new TestAuditableRequest();

		async Task<JobResult> NextDelegate() => await Task.FromResult(JobResult.NoWorkPerformed).ConfigureAwait(true);

		// Act
		var result = await behavior.Handle(request, NextDelegate, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(JobResult.NoWorkPerformed);

		// Verify audit was NOT published
		A.CallTo(() => _auditMessagePublisher.PublishAsync(
				A<IActivityAudited>._, A<IActivityContext>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowWhenRequestIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger<AuditBehavior<TestAuditableRequest, string>>>();
		var behavior = new AuditBehavior<TestAuditableRequest, string>(
			_activityContext, _auditMessagePublisher, logger, _outbox);

		async Task<string> NextDelegate() => await Task.FromResult("Success").ConfigureAwait(true);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			// ReSharper disable once ExpressionIsAlwaysNull
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			_ = await behavior.Handle(null!, NextDelegate, CancellationToken.None).ConfigureAwait(true);
#pragma warning restore CS8625
		}).ConfigureAwait(true);
	}

	[Fact]
	public async Task ThrowWhenNextDelegateIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger<AuditBehavior<TestAuditableRequest, string>>>();
		var behavior = new AuditBehavior<TestAuditableRequest, string>(
			_activityContext, _auditMessagePublisher, logger, _outbox);

		var request = new TestAuditableRequest();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			// ReSharper disable once ExpressionIsAlwaysNull
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			_ = await behavior.Handle(request, null!, CancellationToken.None).ConfigureAwait(true);
#pragma warning restore CS8625
		}).ConfigureAwait(true);
	}

	private IActivityContext CreateMockActivityContext()
	{
		var accessToken = new TestAccessToken { Login = "testuser", UserId = "user1", FullName = "Test User", TenantId = "tenant1" };

		var context = new TestActivityContext
		{
			ClientAddress = "127.0.0.1",
			CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			AccessToken = new TestAccessToken { Login = "testuser", UserId = "user1", FullName = "Test User", TenantId = "tenant1" }
		};

		context.Set<IAccessToken>("AccessToken", accessToken);
		context.Set<ITenantId>("TenantId", new TestTenantId("tenant1"));
		context.Set<IClientAddress>("ClientAddress", new TestClientAddress("127.0.0.1"));
		context.Set<ICorrelationId>("CorrelationId", new TestCorrelationId(Guid.Parse("11111111-1111-1111-1111-111111111111")));

		return context;
	}

	private sealed class TestTenantId(string value) : ITenantId
	{
		public string Value { get; set; } = value;
	}

	private sealed class TestClientAddress(string value) : IClientAddress
	{
		public string Value { get; set; } = value;
	}

	private sealed class TestCorrelationId(Guid value) : ICorrelationId
	{
		public Guid Value { get; set; } = value;
	}

	private sealed class TestAccessToken : IAccessToken
	{
		public AuthenticationState AuthenticationState => AuthenticationState.Authenticated;
		public string? Jwt { get; set; }
		public string? FirstName => null;
		public string? LastName => null;
		public string FullName { get; set; } = string.Empty;
		public string? Login { get; set; }
		public string TenantId { get; set; } = "tenant1";
		public string? UserId { get; set; }

		public bool IsAuthorized(string activityName, string? resourceId = null) => true;

		public bool HasGrant(string activityName) => true;

		public bool HasGrant<TActivity>() => true;

		public bool HasGrant(string resourceType, string resourceId) => true;

		public bool HasGrant<TResourceType>(string resourceId) => true;

		/// <inheritdoc />
		public bool IsAnonymous() => !IsAuthenticated();

		/// <inheritdoc />
		public bool IsAuthenticated() => AuthenticationState == AuthenticationState.Authenticated;
	}

	private sealed class TestActivityContext : IActivityContext
	{
		private readonly Dictionary<string, object?> _store = new();

		public string? ClientAddress { get; set; }
		public Guid CorrelationId { get; set; }
		public IAccessToken? AccessToken { get; set; }

		public T? Get<T>(string key) =>
			_store.TryGetValue(key, out var value) ? (T?)value : default;

		public T Get<T>(string key, T defaultValue) =>
			Get<T>(key) ?? defaultValue;

		public bool ContainsKey(string key) =>
			_store.ContainsKey(key);

		public void Remove(string key) =>
			_ = _store.Remove(key);

		public void Set<T>(string key, T value) =>
			_store[key] = value!;
	}
}

// Test classes
public sealed class TestAuditableRequest : IAmAuditable
{
	// Minimal request implementation that is auditable
}

public sealed class TestNonAuditableRequest
{
	// Minimal request implementation that is not auditable
}
