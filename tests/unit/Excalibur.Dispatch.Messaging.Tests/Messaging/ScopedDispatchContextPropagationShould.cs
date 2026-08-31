// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Resolving a handler into a dependency-injection scope the caller's context does not already target is
/// a change of scope, not a change of caller: the tenant, user, correlation and causation the caller
/// established still apply to the handler that runs. Substituting a fresh context for the duration of
/// that resolution discarded all of them.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class ScopedDispatchContextPropagationShould
{
	[Fact]
	public async Task CarryTheCallersTenantIntoAHandlerResolvedInAFreshScope()
	{
		// Arrange -- the caller's context is bound to the ROOT provider, which is not a scope, so the
		// scoped handler below must be resolved from a freshly created scope. That is precisely the
		// branch that used to substitute a bare context.
		ScopedContextCapturingHandler.Reset();
		using var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var message = new ScopedContextQuery();
		var context = new MessageContext(message, provider)
		{
			CorrelationId = "corr-from-caller",
		};
		var identity = context.GetOrCreateIdentityFeature();
		identity.TenantId = "tenant-from-caller";
		identity.UserId = "user-from-caller";

		// Act
		var result = await dispatcher
			.DispatchAsync<ScopedContextQuery, string>(message, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert -- liveness: the handler ran, resolved its scoped dependency from a real scope, and saw
		// the caller's tenant, user and correlation. Without this arm the safety assertions below would
		// also hold for a dispatch that never reached a handler at all.
		result.Succeeded.ShouldBeTrue($"Error: {result.ErrorMessage}; Problem: {result.ProblemDetails?.Detail}");
		result.ReturnValue.ShouldBe("scoped-ok");
		ScopedContextCapturingHandler.Invocations.ShouldBe(1);
		ScopedContextCapturingHandler.CapturedContext.ShouldNotBeNull();
		ScopedContextCapturingHandler.CapturedTenantId.ShouldBe("tenant-from-caller");
		ScopedContextCapturingHandler.CapturedUserId.ShouldBe("user-from-caller");
		ScopedContextCapturingHandler.CapturedCorrelationId.ShouldBe("corr-from-caller");

		// Assert -- safety: the handler was NOT handed a context with no tenant, which downstream is
		// indistinguishable from a genuinely untenanted operation.
		ScopedContextCapturingHandler.CapturedTenantId.ShouldNotBeNull();

		// Assert -- the caller's context is left as it was found: the scope rebinding is undone, so the
		// caller does not inherit a provider that is about to be disposed.
		context.RequestServices.ShouldBeSameAs(provider);
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddScoped<ScopedDependency>();
		_ = services.AddScoped<IActionHandler<ScopedContextQuery, string>, ScopedContextCapturingHandler>();
		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchHandlers();

		var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		return provider;
	}

	private sealed class ScopedDependency
	{
		public string Marker => "scoped-ok";
	}

	private sealed class ScopedContextQuery : IDispatchAction<string>
	{
		public object Body => this;

		public Guid Id { get; } = Guid.NewGuid();

		public string MessageId => Id.ToString();

		public string MessageType => GetType().FullName ?? GetType().Name;

		public MessageKinds Kind => MessageKinds.Action;

		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
	}

	/// <summary>
	/// A writable <see cref="IMessageContext"/> property is the activator's context-injection seam, so
	/// this handler observes exactly the context the bus decided to hand it.
	/// </summary>
	private sealed class ScopedContextCapturingHandler(ScopedDependency dependency)
		: IActionHandler<ScopedContextQuery, string>
	{
		private static int s_invocations;

		public static int Invocations => Volatile.Read(ref s_invocations);

		public static IMessageContext? CapturedContext { get; private set; }

		public static string? CapturedTenantId { get; private set; }

		public static string? CapturedUserId { get; private set; }

		public static string? CapturedCorrelationId { get; private set; }

		public IMessageContext? Context { get; set; }

		public static void Reset()
		{
			_ = Interlocked.Exchange(ref s_invocations, 0);
			CapturedContext = null;
			CapturedTenantId = null;
			CapturedUserId = null;
			CapturedCorrelationId = null;
		}

		public Task<string> HandleAsync(ScopedContextQuery action, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref s_invocations);
			CapturedContext = Context;
			CapturedTenantId = Context?.GetTenantId();
			CapturedUserId = Context?.GetUserId();
			CapturedCorrelationId = Context?.CorrelationId;
			return Task.FromResult(dependency.Marker);
		}
	}
}
