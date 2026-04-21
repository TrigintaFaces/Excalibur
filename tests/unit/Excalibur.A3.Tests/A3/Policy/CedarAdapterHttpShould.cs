// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Policy.Cedar;

namespace Excalibur.Tests.A3.Policy;

/// <summary>
/// Tests for Cedar HTTP adapter: happy path (Local + AVP modes), timeout,
/// connection failure, input mapping, and response parsing (Sprint 727 T.1 yfelyg).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class CedarAdapterHttpShould : IDisposable
{
	private static readonly AuthorizationSubject s_subject =
		new("user-42", "tenant-1", new Dictionary<string, string> { ["role"] = "admin" });

	private static readonly AuthorizationAction s_action =
		new("Read", new Dictionary<string, string> { ["scope"] = "full" });

	private static readonly AuthorizationResource s_resource =
		new("Order", "order-123", new Dictionary<string, string> { ["region"] = "us-east" });

	private ServiceProvider? _provider;

	public void Dispose()
	{
		_provider?.Dispose();
	}

	private IAuthorizationEvaluator BuildEvaluator(
		OpaAdapterHttpShould.MockHttpHandler handler,
		Action<CedarOptions>? configureOverride = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();

		var builder = services.AddExcaliburA3()
			.UseCedarPolicy(configureOverride ?? (opts =>
			{
				opts.Endpoint = "http://cedar-test:8180";
				opts.Mode = CedarMode.Local;
			}));

		services.ConfigureHttpClientDefaults(b =>
			b.ConfigurePrimaryHttpMessageHandler(() => handler));

		_provider = services.BuildServiceProvider();
		return _provider.GetRequiredService<IAuthorizationEvaluator>();
	}

	// ──────────────────────────────────────────────
	// HTTP Happy Path -- Local Mode
	// ──────────────────────────────────────────────

	[Fact]
	public async Task ReturnPermitWhenLocalCedarReturnsDecisionAllow()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "Allow"}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
		decision.Reason.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnDenyWhenLocalCedarReturnsDecisionDeny()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "Deny"}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldNotBeNull();
	}

	// ──────────────────────────────────────────────
	// HTTP Happy Path -- AVP Mode
	// ──────────────────────────────────────────────

	[Fact]
	public async Task ReturnPermitWhenAvpReturnsDecisionAllow()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "ALLOW"}""");

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://avp-test:443";
			opts.Mode = CedarMode.AwsVerifiedPermissions;
			opts.PolicyStoreId = "ps-abc123";
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
	}

	[Fact]
	public async Task ReturnDenyWhenAvpReturnsDecisionDeny()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "DENY"}""");

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://avp-test:443";
			opts.Mode = CedarMode.AwsVerifiedPermissions;
			opts.PolicyStoreId = "ps-abc123";
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
	}

	// ──────────────────────────────────────────────
	// Case-insensitive decision matching (Local)
	// ──────────────────────────────────────────────

	[Theory]
	[InlineData("allow")]
	[InlineData("ALLOW")]
	[InlineData("Allow")]
	public async Task AcceptCaseInsensitiveAllowDecisionInLocalMode(string decision)
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, $$"""{"decision": "{{decision}}"}""");

		var evaluator = BuildEvaluator(handler);
		var result = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		result.Effect.ShouldBe(AuthorizationEffect.Permit);
	}

	// ──────────────────────────────────────────────
	// HTTP Error Handling (fail-closed)
	// ──────────────────────────────────────────────

	[Theory]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.BadRequest)]
	[InlineData(HttpStatusCode.ServiceUnavailable)]
	public async Task ReturnDenyOnHttpErrorWhenFailClosed(HttpStatusCode statusCode)
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(statusCode, "{}");

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://cedar-test:8180";
			opts.FailClosed = true;
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldContain($"HTTP {(int)statusCode}");
	}

	[Fact]
	public async Task ReturnPermitOnHttpErrorWhenFailOpen()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.InternalServerError, "{}");

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://cedar-test:8180";
			opts.FailClosed = false;
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
	}

	// ──────────────────────────────────────────────
	// Timeout Handling
	// ──────────────────────────────────────────────

	[Fact]
	public async Task ReturnDenyOnTimeoutWhenFailClosed()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithTimeout();

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://cedar-test:8180";
			opts.TimeoutMs = 200;
			opts.FailClosed = true;
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldContain("timed out");
	}

	[Fact]
	public async Task ReturnPermitOnTimeoutWhenFailOpen()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithTimeout();

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://cedar-test:8180";
			opts.TimeoutMs = 200;
			opts.FailClosed = false;
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
		decision.Reason.ShouldContain("timed out");
	}

	// ──────────────────────────────────────────────
	// Connection Failure
	// ──────────────────────────────────────────────

	[Fact]
	public async Task ReturnDenyOnConnectionFailureWhenFailClosed()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithConnectionFailure();

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://cedar-test:8180";
			opts.FailClosed = true;
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldContain("connection failed");
	}

	[Fact]
	public async Task ReturnPermitOnConnectionFailureWhenFailOpen()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithConnectionFailure();

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://cedar-test:8180";
			opts.FailClosed = false;
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
		decision.Reason.ShouldContain("connection failed");
	}

	// ──────────────────────────────────────────────
	// Input Mapping -- Local Mode
	// ──────────────────────────────────────────────

	[Fact]
	public async Task SendCorrectCedarLocalInputJsonFormat()
	{
		byte[]? capturedBody = null;
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithCapture(
			HttpStatusCode.OK,
			"""{"decision": "Allow"}""",
			body => capturedBody = body);

		var evaluator = BuildEvaluator(handler);
		await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		capturedBody.ShouldNotBeNull();
		using var doc = JsonDocument.Parse(capturedBody);
		var root = doc.RootElement;

		// Cedar local format: principal, action, resource, context
		var principal = root.GetProperty("principal");
		principal.GetProperty("type").GetString().ShouldBe("User");
		principal.GetProperty("id").GetString().ShouldBe("user-42");

		var action = root.GetProperty("action");
		action.GetProperty("type").GetString().ShouldBe("Action");
		action.GetProperty("id").GetString().ShouldBe("Read");

		var resource = root.GetProperty("resource");
		resource.GetProperty("type").GetString().ShouldBe("Order");
		resource.GetProperty("id").GetString().ShouldBe("order-123");

		// Context should contain tenantId plus merged attributes
		var context = root.GetProperty("context");
		context.GetProperty("tenantId").GetString().ShouldBe("tenant-1");
		context.GetProperty("role").GetString().ShouldBe("admin");
		context.GetProperty("scope").GetString().ShouldBe("full");
		context.GetProperty("region").GetString().ShouldBe("us-east");
	}

	// ──────────────────────────────────────────────
	// Input Mapping -- AVP Mode
	// ──────────────────────────────────────────────

	[Fact]
	public async Task SendCorrectAvpInputJsonFormat()
	{
		byte[]? capturedBody = null;
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithCapture(
			HttpStatusCode.OK,
			"""{"decision": "ALLOW"}""",
			body => capturedBody = body);

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://avp-test:443";
			opts.Mode = CedarMode.AwsVerifiedPermissions;
			opts.PolicyStoreId = "ps-store-99";
		});

		await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		capturedBody.ShouldNotBeNull();
		using var doc = JsonDocument.Parse(capturedBody);
		var root = doc.RootElement;

		// AVP format includes policyStoreId and uses entityType/entityId
		root.GetProperty("policyStoreId").GetString().ShouldBe("ps-store-99");

		var principal = root.GetProperty("principal");
		principal.GetProperty("entityType").GetString().ShouldBe("User");
		principal.GetProperty("entityId").GetString().ShouldBe("user-42");

		var action = root.GetProperty("action");
		action.GetProperty("actionType").GetString().ShouldBe("Action");
		action.GetProperty("actionId").GetString().ShouldBe("Read");

		var resource = root.GetProperty("resource");
		resource.GetProperty("entityType").GetString().ShouldBe("Order");
		resource.GetProperty("entityId").GetString().ShouldBe("order-123");
	}

	// ──────────────────────────────────────────────
	// Response Parsing Edge Cases
	// ──────────────────────────────────────────────

	[Fact]
	public async Task ReturnDenyOnMissingDecisionProperty()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"result": true}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldContain("missing");
	}

	[Fact]
	public async Task ReturnDenyOnMalformedJsonResponse()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, "not-json");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldContain("not valid JSON");
	}

	[Fact]
	public async Task ReturnDenyOnUnknownDecisionValue()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "Maybe"}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
	}

	// ──────────────────────────────────────────────
	// Null Argument Guards
	// ──────────────────────────────────────────────

	[Fact]
	public async Task ThrowOnNullSubject()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "Allow"}""");

		var evaluator = BuildEvaluator(handler);

		await Should.ThrowAsync<ArgumentNullException>(
			() => evaluator.EvaluateAsync(null!, s_action, s_resource, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullAction()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "Allow"}""");

		var evaluator = BuildEvaluator(handler);

		await Should.ThrowAsync<ArgumentNullException>(
			() => evaluator.EvaluateAsync(s_subject, null!, s_resource, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullResource()
	{
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "Allow"}""");

		var evaluator = BuildEvaluator(handler);

		await Should.ThrowAsync<ArgumentNullException>(
			() => evaluator.EvaluateAsync(s_subject, s_action, null!, CancellationToken.None));
	}

	// ──────────────────────────────────────────────
	// Input Mapping: no attributes / no tenantId (Local)
	// ──────────────────────────────────────────────

	[Fact]
	public async Task OmitTenantIdFromContextWhenNull()
	{
		var subjectNoTenant = new AuthorizationSubject("user-1", null, null);
		var actionNoAttrs = new AuthorizationAction("Write", null);
		var resourceNoAttrs = new AuthorizationResource("Doc", "d-1", null);

		byte[]? capturedBody = null;
		using var handler = OpaAdapterHttpShould.MockHttpHandler.WithCapture(
			HttpStatusCode.OK,
			"""{"decision": "Allow"}""",
			body => capturedBody = body);

		var evaluator = BuildEvaluator(handler);
		await evaluator.EvaluateAsync(
			subjectNoTenant, actionNoAttrs, resourceNoAttrs, CancellationToken.None);

		capturedBody.ShouldNotBeNull();
		using var doc = JsonDocument.Parse(capturedBody);
		var context = doc.RootElement.GetProperty("context");

		context.TryGetProperty("tenantId", out _).ShouldBeFalse();
	}
}
