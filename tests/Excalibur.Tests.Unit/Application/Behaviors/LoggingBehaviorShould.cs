using Excalibur.Application.Behaviors;
using Excalibur.Tests.Stubs.Application;

using FakeItEasy;

using MediatR;

using Microsoft.Extensions.Logging;

using Shouldly;

namespace Excalibur.Tests.Unit.Application.Behaviors;

public class LoggingBehaviorShould
{
	[Fact]
	public async Task IncludeCorrelationIdInScopeIfRequestImplementsIAmCorrelatable()
	{
		// Arrange
		var logger = A.Fake<ILogger<LoggingBehavior<CorrelatableRequest, string>>>();
		var behavior = new LoggingBehavior<CorrelatableRequest, string>(logger);
		var correlationId = Guid.NewGuid();
		var request = new CorrelatableRequest { CorrelationId = correlationId };
		var next = A.Fake<RequestHandlerDelegate<string>>();

		_ = A.CallTo(() => next()).Returns("Success");

		// Setup expectations for BeginScope with correlation ID
		_ = A.CallTo(() => logger.BeginScope(A<Dictionary<string, object>>
				.That.Matches(d => d.ContainsKey("CorrelationId") &&
								   d["CorrelationId"].Equals(correlationId))))
			.Returns(A.Fake<IDisposable>());

		// Act
		_ = await behavior.Handle(request, next, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => logger.BeginScope(A<Dictionary<string, object>>
				.That.Matches(d => d.ContainsKey("CorrelationId") &&
								   d["CorrelationId"].Equals(correlationId))))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTenantIdInScopeIfRequestImplementsIAmMultiTenant()
	{
		// Arrange
		var logger = A.Fake<ILogger<LoggingBehavior<MultiTenantRequest, string>>>();
		var behavior = new LoggingBehavior<MultiTenantRequest, string>(logger);
		const string TenantId = "test-tenant-123";
		var request = new MultiTenantRequest { TenantId = TenantId };
		var next = A.Fake<RequestHandlerDelegate<string>>();

		_ = A.CallTo(() => next()).Returns("Success");

		// Setup expectations for BeginScope with tenant ID
		_ = A.CallTo(() => logger.BeginScope(A<Dictionary<string, object>>
				.That.Matches(d => d.ContainsKey("TenantId") &&
								   d["TenantId"].Equals(TenantId))))
			.Returns(A.Fake<IDisposable>());

		// Act
		_ = await behavior.Handle(request, next, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => logger.BeginScope(A<Dictionary<string, object>>
				.That.Matches(d => d.ContainsKey("TenantId") &&
								   d["TenantId"].Equals(TenantId))))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeBothCorrelationIdAndTenantIdWhenRequestImplementsBothInterfaces()
	{
		// Arrange
		var logger = A.Fake<ILogger<LoggingBehavior<CorrelatableAndMultiTenantRequest, string>>>();
		var behavior = new LoggingBehavior<CorrelatableAndMultiTenantRequest, string>(logger);
		var correlationId = Guid.NewGuid();
		const string TenantId = "test-tenant-456";
		var request = new CorrelatableAndMultiTenantRequest { CorrelationId = correlationId, TenantId = TenantId };
		var next = A.Fake<RequestHandlerDelegate<string>>();

		_ = A.CallTo(() => next()).Returns("Success");

		// Setup expectation with correct dictionary type
		_ = A.CallTo(() => logger.BeginScope(A<Dictionary<string, object>>.That.Matches(d =>
				d.ContainsKey("CorrelationId") && d["CorrelationId"].Equals(correlationId) &&
				d.ContainsKey("TenantId") && d["TenantId"].Equals(TenantId))))
			.Returns(A.Fake<IDisposable>());

		// Act
		_ = await behavior.Handle(request, next, CancellationToken.None).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => logger.BeginScope(A<Dictionary<string, object>>.That.Matches(d =>
				d.ContainsKey("CorrelationId") && d["CorrelationId"].Equals(correlationId) &&
				d.ContainsKey("TenantId") && d["TenantId"].Equals(TenantId))))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenRequestIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger<LoggingBehavior<TestRequest, string>>>();
		var behavior = new LoggingBehavior<TestRequest, string>(logger);
		TestRequest request = null!;
		var next = A.Fake<RequestHandlerDelegate<string>>();

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("request");
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenNextIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger<LoggingBehavior<TestRequest, string>>>();
		var behavior = new LoggingBehavior<TestRequest, string>(logger);
		var request = new TestRequest("Test");
		RequestHandlerDelegate<string> next = null!;

		// Act & Assert
		var exception = await Should.ThrowAsync<ArgumentNullException>(() =>
			behavior.Handle(request, next, CancellationToken.None)).ConfigureAwait(true);

		exception.ParamName.ShouldBe("next");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenLoggerIsNull()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new LoggingBehavior<TestRequest, string>(null!));

		exception.ParamName.ShouldBe("logger");
	}
}
