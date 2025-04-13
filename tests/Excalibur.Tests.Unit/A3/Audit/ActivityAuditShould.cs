using Excalibur.A3;
using Excalibur.A3.Audit;
using Excalibur.A3.Authentication;
using Excalibur.Core;
using Excalibur.Core.Exceptions;
using Excalibur.Domain;
using Excalibur.Tests.Mothers.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.A3.Audit;

public class ActivityAuditShould : SharedApplicationContextTestBase
{
	[Fact]
	public void ThrowArgumentNullExceptionForNullContext()
	{
		// Arrange
		var request = new TestRequest();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
		{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			IActivityContext nullContext = null;
			// ReSharper disable once ExpressionIsAlwaysNull
			_ = new ActivityAudit<TestRequest, string>(nullContext!, request);
#pragma warning restore CS8625
		});
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullRequest()
	{
		// Arrange
		var context = CreateMockActivityContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
		{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			TestRequest nullRequest = null;
			// ReSharper disable once ExpressionIsAlwaysNull
			_ = new ActivityAudit<TestRequest, string>(context, nullRequest!);
#pragma warning restore CS8625
		});
	}

	[Fact]
	public void InitializePropertiesCorrectlyOnConstruction()
	{
		// Arrange
		var context = CreateMockActivityContext();
		var request = new TestRequest();

		// Configure ApplicationContext
		ApplicationContextMother.Initialize(new Dictionary<string, string?>
		{
			{ "ApplicationName", "TestApp" }, { "ApplicationSystemName", "testapp" }
		});

		// Act
		var audit = new ActivityAudit<TestRequest, string>(context, request);

		// Assert
		audit.ActivityName.ShouldBe(nameof(TestRequest));
		audit.ApplicationName.ShouldBe("TestApp");
		audit.ClientAddress.ShouldBe("127.0.0.1");
		audit.CorrelationId.ShouldBe(Guid.Parse("11111111-1111-1111-1111-111111111111"));
		audit.Login.ShouldBe("testuser");
		audit.Request.ShouldBe(request);
		audit.Response.ShouldBeNull();
		audit.StatusCode.ShouldBe(0);
		audit.TenantId.ShouldBe("tenant1");
		audit.Timestamp.ShouldBeInRange(DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow);
		audit.UserId.ShouldBe("user1");
		audit.UserName.ShouldBe("Test User");
	}

	[Fact]
	public async Task DecorateAsyncShouldExecuteActivitySuccessfully()
	{
		// Arrange
		var context = CreateMockActivityContext();
		var request = new TestRequest();

		// Configure ApplicationContext
		ApplicationContextMother.SetValue("ApplicationName", "TestApp");

		var audit = new ActivityAudit<TestRequest, string>(context, request);

		// Act
		var result = await audit.DecorateAsync(() => Task.FromResult("Success")).ConfigureAwait(true);

		// Assert
		result.ShouldBe("Success");
		audit.Response.ShouldBe("Success");
		audit.StatusCode.ShouldBe(200);
		audit.Exception.ShouldBeNull();
	}

	[Fact]
	public async Task DecorateAsyncShouldCaptureExceptionAndSetStatusCode()
	{
		// Arrange
		var context = CreateMockActivityContext();
		var request = new TestRequest();

		// Configure ApplicationContext
		ApplicationContextMother.SetValue("ApplicationName", "TestApp");

		var audit = new ActivityAudit<TestRequest, string>(context, request);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			_ = await audit.DecorateAsync(() => throw new InvalidOperationException("Test Exception")).ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Additional Assertions
		_ = audit.Exception.ShouldBeOfType<InvalidOperationException>();
		audit.StatusCode.ShouldBe(500);
	}

	[Fact]
	public async Task DecorateAsyncShouldCaptureApiExceptionAndSetCorrectStatusCode()
	{
		// Arrange
		var context = CreateMockActivityContext();
		var request = new TestRequest();
		var audit = new ActivityAudit<TestRequest, string>(context, request);

		// Create an ApiException with explicit status code
		var apiException = new ApiException(statusCode: 404, message: "Not Found", innerException: null);

		// Act & Assert
		_ = await Should.ThrowAsync<ApiException>(async () =>
		{
			_ = await audit.DecorateAsync(() => throw apiException).ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Additional Assertions
		_ = audit.Exception.ShouldBeOfType<ApiException>();
		audit.StatusCode.ShouldBe(404);
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

	private sealed class TestRequest
	{
		// Minimal request implementation
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
