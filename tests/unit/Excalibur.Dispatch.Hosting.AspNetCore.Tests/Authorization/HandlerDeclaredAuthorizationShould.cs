// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using FakeItEasy;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// A requirement declared on a HANDLER is enforced, and a requirement that cannot be found is refused rather
/// than assumed absent.
/// </summary>
/// <remarks>
/// <para>
/// <b>What these arms deliberately do NOT do.</b> They never write <c>HandlerType</c> into the message
/// context. The arms elsewhere in this project do, and that is why the defect survived them: seeding that
/// item constructs the one state in which the old code was correct, so the suite exercised a configuration
/// the product was never in. Nothing in the framework sets that item on the ordinary dispatch path — handler
/// selection happens downstream of the middleware pipeline — so a handler's own <c>[Authorize]</c> was never
/// consulted, and any caller could execute a protected handler as long as the MESSAGE carried no attributes.
/// </para>
/// <para>
/// The handler registry here is built by the framework's own <c>AddDispatchHandlers()</c> from an ordinary
/// handler registration, which is how a consuming application composes one. Nothing about the association
/// between a message and its handler is arranged by hand.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "Authorization")]
public sealed class HandlerDeclaredAuthorizationShould
{
	// ---------- the bypass ----------

	/// <summary>
	/// SAFETY. The message declares nothing; its handler requires the Admin role. A caller without that role
	/// must not reach the handler. RED against the shipped middleware, which read a context item nothing sets
	/// and therefore found no handler requirement at all.
	/// </summary>
	[Fact]
	public async Task RefuseACallerWhoDoesNotSatisfyARequirementDeclaredOnTheHandler()
	{
		var (middleware, container) = Compose<AdminOnlyHandler>(roles: ["User"]);

		using (container)
		{
			var reachedTheHandler = false;

			var result = await middleware.InvokeAsync(
				new PlainMessage(),
				UnseededContext(),
				(_, _, _) =>
				{
					reachedTheHandler = true;
					return ValueTask.FromResult(A.Fake<IMessageResult>());
				},
				TestContext.Current.CancellationToken);

			reachedTheHandler.ShouldBeFalse(
				"the handler declares a required role and this caller is not in it");
			result.Succeeded.ShouldBeFalse();
		}
	}

	/// <summary>
	/// LIVENESS. Without this, the arm above is satisfied by a middleware that refuses everything — the
	/// cheapest way to look secure and the most expensive way to be wrong. Same composition, a caller who IS
	/// in the required role.
	/// </summary>
	[Fact]
	public async Task StillAdmitACallerWhoSatisfiesTheHandlersRequirement()
	{
		var (middleware, container) = Compose<AdminOnlyHandler>(roles: ["Admin"]);

		using (container)
		{
			var reachedTheHandler = false;

			_ = await middleware.InvokeAsync(
				new PlainMessage(),
				UnseededContext(),
				(_, _, _) =>
				{
					reachedTheHandler = true;
					return ValueTask.FromResult(A.Fake<IMessageResult>());
				},
				TestContext.Current.CancellationToken);

			reachedTheHandler.ShouldBeTrue("a caller in the required role must still be able to run the handler");
		}
	}

	// ---------- the third state ----------

	/// <summary>
	/// SAFETY. When nothing can say which handlers will run, the middleware does not know whether
	/// authorization applies — and it must not conclude that it does not. An absence of metadata is not an
	/// absence of a requirement.
	/// </summary>
	[Fact]
	public async Task RefuseWhenTheHandlersCannotBeDeterminedAtAll()
	{
		var (middleware, container) = Compose<AdminOnlyHandler>(roles: ["Admin"]);

		using (container)
		{
			var reachedTheHandler = false;

			// A message type no handler was registered for: the registry has nothing to say about it.
			var result = await middleware.InvokeAsync(
				new UnregisteredAction(),
				UnseededContext(),
				(_, _, _) =>
				{
					reachedTheHandler = true;
					return ValueTask.FromResult(A.Fake<IMessageResult>());
				},
				TestContext.Current.CancellationToken);

			reachedTheHandler.ShouldBeFalse(
				"nothing could say what would handle this message, so whether authorization applies is unknown");
			result.Succeeded.ShouldBeFalse();
		}
	}

	/// <summary>
	/// LIVENESS for the refusal. The third state must fire on UNKNOWN only, never on a message whose handlers
	/// are known and simply declare nothing. Without this arm the refusal above is indistinguishable from a
	/// middleware that has stopped letting anything through.
	/// </summary>
	[Fact]
	public async Task StillPassThroughAMessageWhoseKnownHandlerDeclaresNothing()
	{
		var (middleware, container) = Compose<UndecoratedHandler>(roles: []);

		using (container)
		{
			var reachedTheHandler = false;

			_ = await middleware.InvokeAsync(
				new PlainMessage(),
				UnseededContext(),
				(_, _, _) =>
				{
					reachedTheHandler = true;
					return ValueTask.FromResult(A.Fake<IMessageResult>());
				},
				TestContext.Current.CancellationToken);

			reachedTheHandler.ShouldBeTrue(
				"the handler is known and declares no requirement, so there is nothing to enforce");
		}
	}

	/// <summary>
	/// Composes the middleware the way an application does: an ordinary handler registration, the framework's
	/// own <c>AddDispatchHandlers()</c> to build the registry from it, and the host's real authorization
	/// services.
	/// </summary>
	private static (AspNetCoreAuthorizationMiddleware Middleware, ServiceProvider Container) Compose<THandler>(
		string[] roles)
		where THandler : class, IEventHandler<PlainMessage>
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuthorization();
		_ = services.AddScoped<IEventHandler<PlainMessage>, THandler>();
		_ = services.AddDispatchHandlers();

		var container = services.BuildServiceProvider();

		var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1") };
		claims.AddRange(roles.Select(static role => new Claim(ClaimTypes.Role, role)));

		var accessor = A.Fake<IHttpContextAccessor>();
		A.CallTo(() => accessor.HttpContext).Returns(new DefaultHttpContext
		{
			User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
		});

		var middleware = new AspNetCoreAuthorizationMiddleware(
			accessor,
			container.GetRequiredService<IAuthorizationService>(),
			container.GetRequiredService<IAuthorizationPolicyProvider>(),
			container.GetRequiredService<IHandlerRegistry>(),
			Microsoft.Extensions.Options.Options.Create(new AspNetCoreAuthorizationOptions()),
			NullLogger<AspNetCoreAuthorizationMiddleware>.Instance);

		return (middleware, container);
	}

	/// <summary>
	/// A message context with an EMPTY item bag — nothing tells the middleware what the handler is. This is
	/// the state the ordinary dispatch path actually produces, and the state the previous arms never used.
	/// </summary>
	private static IMessageContext UnseededContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => context.CorrelationId).Returns("corr-1");

		return context;
	}

	private sealed class PlainMessage : IDispatchEvent;

	private sealed class UnregisteredAction : IDispatchAction;


	[Authorize(Roles = "Admin")]
	private sealed class AdminOnlyHandler : IEventHandler<PlainMessage>
	{
		public Task HandleAsync(PlainMessage eventMessage, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class UndecoratedHandler : IEventHandler<PlainMessage>
	{
		public Task HandleAsync(PlainMessage eventMessage, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}
}
