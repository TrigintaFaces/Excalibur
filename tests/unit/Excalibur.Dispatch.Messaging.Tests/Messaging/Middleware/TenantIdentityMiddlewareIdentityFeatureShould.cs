// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestFakes;

using MessageResult = Excalibur.Dispatch.MessageResult;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// A tenant this middleware resolves has to land where the read side looks for it. The single accessor
/// every reader goes through -- <c>IMessageContext.GetTenantId()</c>, and through it the outbox write
/// path -- reads the identity feature, not the context's <c>Items</c>. A tenant recorded only in
/// <c>Items</c> is therefore resolved and then invisible.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
public sealed class TenantIdentityMiddlewareIdentityFeatureShould
{
	[Fact]
	public async Task StampTheResolvedTenantWhereTheReadSideLooksForIt()
	{
		// Arrange -- a configured default tenant that is deliberately NOT the one on the message, so an
		// implementation that stamps the default rather than the resolved value is distinguishable.
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = true,
			TenantIdHeader = "X-Tenant-ID",
			DefaultTenantId = "fallback-tenant",
			ValidateTenantAccess = false,
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "tenant-feature-1" };
		context.SetItem("X-Tenant-ID", "tenant-abc");

		string? tenantSeenDownstream = null;
		var result = await middleware.InvokeAsync(
			message,
			context,
			(_, ctx, _) =>
			{
				tenantSeenDownstream = ctx.GetTenantId();
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert -- liveness: the rest of the pipeline, and every reader downstream of it, sees the
		// tenant this middleware resolved.
		result.IsSuccess.ShouldBeTrue();
		tenantSeenDownstream.ShouldBe("tenant-abc");
		context.GetTenantId().ShouldBe("tenant-abc");
		context.GetIdentityFeature().ShouldNotBeNull().TenantId.ShouldBe("tenant-abc");

		// Assert -- safety: it is the resolved tenant, not the configured fallback.
		tenantSeenDownstream.ShouldNotBe("fallback-tenant");

		// Assert -- the Items entries the header-propagation surface depends on are still written.
		context.GetItem<string>("TenantId").ShouldBe("tenant-abc");
		context.GetItem<string>("X-Tenant-ID").ShouldBe("tenant-abc");
	}

	[Fact]
	public async Task NotInventATenantWhenTenantIdentityIsDisabled()
	{
		// Arrange -- disabled middleware. It must pass the message through untouched rather than stamp
		// a default, which would turn a genuinely untenanted operation into an owned one.
		var middleware = CreateMiddleware(new TenantIdentityOptions
		{
			Enabled = false,
			TenantIdHeader = "X-Tenant-ID",
			DefaultTenantId = "fallback-tenant",
		});
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "tenant-feature-2" };

		var reachedNext = false;
		var result = await middleware.InvokeAsync(
			message,
			context,
			(_, _, _) =>
			{
				reachedNext = true;
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert -- liveness: the pipeline still runs. Without this arm, a middleware that threw on
		// every message would satisfy the safety assertion below.
		reachedNext.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();

		// Assert -- safety: no tenant was fabricated.
		context.GetTenantId().ShouldBeNull();
	}

	private static TenantIdentityMiddleware CreateMiddleware(TenantIdentityOptions options) =>
		new(
			MsOptions.Create(options),
			NullTelemetrySanitizer.Instance,
			NullLoggerFactory.Instance.CreateLogger<TenantIdentityMiddleware>());
}
