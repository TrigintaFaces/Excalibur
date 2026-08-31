// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.A3;
using Excalibur.A3.Audit;
using Excalibur.A3.Authorization;

using Microsoft.AspNetCore.Authorization;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Binds the property that makes <see cref="A3AuthorizationMiddleware"/> safe to share: one instance,
/// many callers.
/// </summary>
/// <remarks>
/// <para>
/// A middleware instance is built once and lives for the process. <c>DispatchMiddlewareInvoker</c>
/// materialises the whole set with a single <c>GetServices&lt;IDispatchMiddleware&gt;()</c> against the
/// root provider in its constructor, and the invoker itself is a singleton, so the array is built once
/// no matter what lifetime a middleware's descriptor declares. Anything a middleware holds in a field
/// is therefore held for every message the process ever handles.
/// </para>
/// <para>
/// These tests drive one instance across two request scopes carrying two different callers. They fail
/// if the middleware ever goes back to holding the access token: the second caller would be authorized
/// as the first.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationMiddlewareRequestScopeShould : IDisposable
{
	private readonly IDispatchAuthorizationService _authorization = A.Fake<IDispatchAuthorizationService>();
	private readonly List<string> _authorizedLogins = [];
	private readonly ServiceProvider _serviceProvider;
	private readonly A3AuthorizationMiddleware _sut;
	private int _tokensIssued;

	public AuthorizationMiddlewareRequestScopeShould()
	{
		A.CallTo(() => _authorization.AuthorizeAsync(
				A<ClaimsPrincipal>.Ignored,
				A<string>.Ignored,
				A<IAuthorizationRequirement[]>.Ignored))
			.Invokes((ClaimsPrincipal principal, object _, IAuthorizationRequirement[] _) =>
				_authorizedLogins.Add(principal.Identity?.Name ?? "<none>"))
			.Returns(Excalibur.Dispatch.AuthorizationResult.Success());

		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// A distinct caller per scope: the identity a correctly-scoped host would supply per request.
		_ = services.AddScoped(_ =>
		{
			var login = $"caller-{Interlocked.Increment(ref _tokensIssued)}";
			var token = A.Fake<IAccessToken>();
			A.CallTo(() => token.Claims).Returns([new Claim(ClaimTypes.Name, login)]);
			A.CallTo(() => token.Login).Returns(login);
			A.CallTo(() => token.IsAuthenticated()).Returns(true);
			return token;
		});

		_serviceProvider = services.BuildServiceProvider();

		// Built once, from the root, exactly as the invoker builds it.
		_sut = new A3AuthorizationMiddleware(
			_authorization,
			new AttributeAuthorizationCache(),
			new ConditionExpressionEvaluator());
	}

	public void Dispose() => _serviceProvider.Dispose();

	[Fact]
	public async Task AuthorizeEachRequestAsItsOwnCaller()
	{
		// Arrange & Act - one middleware instance, two request scopes, two callers.
		await DispatchInNewScopeAsync();
		await DispatchInNewScopeAsync();

		// Assert - the second request must not be authorized as the first caller.
		_authorizedLogins.ShouldBe(["caller-1", "caller-2"]);
	}

	[Fact]
	public async Task DenyWhenNoRequestScopeCarriesTheCaller()
	{
		// Arrange - the root provider is not a request scope. Resolving the caller's token from it would
		// cache one identity for the lifetime of the container, so there is no identity to authorize.
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var reached = false;

		// Act
		var result = await _sut.InvokeAsync(
			new ScopeProbeMessage(),
			context,
			(_, _, _) =>
			{
				reached = true;
				return ValueTask.FromResult(A.Fake<IMessageResult>());
			},
			CancellationToken.None);

		// Assert - fails closed, and the handler never runs.
		result.Succeeded.ShouldBeFalse();
		reached.ShouldBeFalse();
		_authorizedLogins.ShouldBeEmpty();
	}

	/// <summary>
	/// <c>AddExcaliburA3</c> must leave exactly one <c>AuditMiddleware</c> descriptor, contributed by the
	/// composition that also registers the scoped <c>IActivityContext</c> it resolves. A second descriptor
	/// at another lifetime is de-duplicated by implementation type, so which one survives is decided by
	/// call order rather than by either registration.
	/// </summary>
	[Fact]
	public void RegisterAuditMiddlewareExactlyOnceAlongsideItsContext()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

		// Act
		_ = services.AddExcaliburA3();

		// Assert
		var auditDescriptors = services
			.Where(d => d.ServiceType == typeof(IDispatchMiddleware)
				&& d.ImplementationType == typeof(AuditMiddleware))
			.ToList();

		auditDescriptors.Count.ShouldBe(1);
		auditDescriptors[0].Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	private async Task DispatchInNewScopeAsync()
	{
		using var scope = _serviceProvider.CreateScope();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(scope.ServiceProvider);

		_ = await _sut.InvokeAsync(
			new ScopeProbeMessage(),
			context,
			(_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>()),
			CancellationToken.None);
	}

	private sealed class ScopeProbeMessage : IDispatchMessage, IRequireAuthorization
	{
		public Guid MessageId { get; } = Guid.NewGuid();

		public string ActivityName => "ScopeProbe";
	}
}
