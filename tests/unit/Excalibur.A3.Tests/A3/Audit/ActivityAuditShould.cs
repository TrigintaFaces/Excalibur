#pragma warning disable IL2026 // Members annotated with RequiresUnreferencedCodeAttribute
#pragma warning disable IL3050 // Members annotated with RequiresDynamicCodeAttribute

using Excalibur.A3;
using Excalibur.A3.Audit;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.A3.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ActivityAuditShould
{
	[Fact]
	public void Initialize_with_context_and_request()
	{
		// Arrange
		var accessToken = A.Fake<IAccessToken>();
		A.CallTo(() => accessToken.Login).Returns("jdoe");
		A.CallTo(() => accessToken.UserId).Returns("user-123");
		A.CallTo(() => accessToken.FullName).Returns("John Doe");

		var clientAddress = A.Fake<IClientAddress>();
		A.CallTo(() => clientAddress.Value).Returns("127.0.0.1");

		var correlationId = A.Fake<ICorrelationId>();
		var expectedCorrelationId = Guid.NewGuid();
		A.CallTo(() => correlationId.Value).Returns(expectedCorrelationId);

		var tenantId = A.Fake<ITenantId>();
		A.CallTo(() => tenantId.Value).Returns("tenant-1");

		var config = A.Fake<IConfiguration>();
		A.CallTo(() => config["ApplicationName"]).Returns("TestApp");

		var context = A.Fake<IActivityContext>();
		A.CallTo(() => context.GetValue("AccessToken", A<IAccessToken>.That.IsNull())).Returns(accessToken);
		A.CallTo(() => context.GetValue("IConfiguration", A<IConfiguration>.That.IsNull())).Returns(config);
		A.CallTo(() => context.GetValue("ClientAddress", A<IClientAddress>.That.IsNull())).Returns(clientAddress);
		A.CallTo(() => context.GetValue("CorrelationId", A<ICorrelationId>.That.IsNull())).Returns(correlationId);
		A.CallTo(() => context.GetValue("TenantId", A<ITenantId>.That.IsNull())).Returns(tenantId);

		var request = new TestRequest();

		// Act
		var audit = new ActivityAudit<TestRequest, string>(context, request);

		// Assert
		audit.ActivityName.ShouldBe(nameof(TestRequest));
		audit.ApplicationName.ShouldBe("TestApp");
		audit.ClientAddress.ShouldBe("127.0.0.1");
		audit.Login.ShouldBe("jdoe");
		audit.UserId.ShouldBe("user-123");
		audit.UserName.ShouldBe("John Doe");
		audit.TenantId.ShouldBe("tenant-1");
		audit.StatusCode.ShouldBe(0);
		audit.Exception.ShouldBeNull();
		audit.Response.ShouldBeNull();
	}

	[Fact]
	public void Use_system_defaults_when_no_access_token()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		// GetValue returns null for AccessToken and IConfiguration
		A.CallTo(() => context.GetValue("AccessToken", A<IAccessToken>.That.IsNull())).Returns((IAccessToken)null!);
		A.CallTo(() => context.GetValue("IConfiguration", A<IConfiguration>.That.IsNull())).Returns((IConfiguration)null!);

		var request = new TestRequest();

		// Act
		var audit = new ActivityAudit<TestRequest, string>(context, request);

		// Assert
		audit.Login.ShouldBe("System");
		audit.UserId.ShouldBe("System");
		audit.UserName.ShouldBe("System");
		audit.ApplicationName.ShouldBe("Unknown");
	}

	[Fact]
	public void Throw_when_context_is_null()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ActivityAudit<TestRequest, string>(null!, new TestRequest()));
	}

	[Fact]
	public void Throw_when_request_is_null()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ActivityAudit<TestRequest, string>(context, null!));
	}

	[Fact]
	public void Have_unique_id()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var audit1 = new ActivityAudit<TestRequest, string>(context, new TestRequest());
		var audit2 = new ActivityAudit<TestRequest, string>(context, new TestRequest());

		// Assert
		audit1.Id.ShouldNotBe(Guid.Empty);
		audit1.Id.ShouldNotBe(audit2.Id);
	}

	[Fact]
	public void Return_id_as_message_id()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());

		// Assert
		audit.MessageId.ShouldBe(audit.Id.ToString());
	}

	[Fact]
	public void Return_event_kind()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());

		// Assert
		audit.Kind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void Return_user_id_as_aggregate_id()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());

		// Assert
		audit.AggregateId.ShouldBe(audit.UserId);
	}

	[Fact]
	public void Return_timestamp_as_occurred_at()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());

		// Assert
		audit.OccurredAt.ShouldBe(audit.Timestamp);
	}

	[Fact]
	public void Return_self_as_body()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());

		// Assert
		audit.Body.ShouldBeSameAs(audit);
	}

	[Fact]
	public void Have_features()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());

		// Assert
		audit.Features.ShouldNotBeNull();
	}

	[Fact]
	public async Task DecorateAsync_captures_success_response()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());

		// Act
		var response = await audit.DecorateAsync(() => Task.FromResult("success"));

		// Assert
		response.ShouldBe("success");
		audit.Response.ShouldBe("success");
		audit.StatusCode.ShouldBe(200);
		audit.Exception.ShouldBeNull();
	}

	[Fact]
	public async Task DecorateAsync_captures_exception()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());
		var expectedException = new InvalidOperationException("test error");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await audit.DecorateAsync(() => throw expectedException));

		audit.Exception.ShouldBe(expectedException);
		audit.StatusCode.ShouldBe(500);
	}

	[Fact]
	public async Task DecorateAsync_captures_api_exception()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());
		var apiException = new ApiException(404, "Not found", null);

		// Act & Assert
		await Should.ThrowAsync<ApiException>(async () =>
			await audit.DecorateAsync(() => throw apiException));

		audit.Exception.ShouldBe(apiException);
		audit.StatusCode.ShouldBe(404);
	}

	[Fact]
	public async Task DecorateAsync_throws_for_null_activity()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await audit.DecorateAsync(null!));
	}

	[Fact]
	public async Task DecorateAsync_sets_timestamp()
	{
		// Arrange
		var audit = new ActivityAudit<TestRequest, string>(CreateContext(), new TestRequest());
		var before = DateTimeOffset.UtcNow;

		// Act
		await audit.DecorateAsync(() => Task.FromResult("result"));

		// Assert
		audit.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
	}

	/// <summary>
	/// Creates a fake IActivityContext that mocks the underlying GetValue calls
	/// rather than the extension methods (which FakeItEasy cannot intercept).
	/// </summary>
	private static IActivityContext CreateContext()
	{
		var config = A.Fake<IConfiguration>();
		A.CallTo(() => config["ApplicationName"]).Returns("TestApp");

		var context = A.Fake<IActivityContext>();
		A.CallTo(() => context.GetValue("AccessToken", A<IAccessToken>.That.IsNull())).Returns((IAccessToken)null!);
		A.CallTo(() => context.GetValue("IConfiguration", A<IConfiguration>.That.IsNull())).Returns(config);
		return context;
	}

	private sealed class TestRequest;
}
