// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Policy.Opa;

namespace Excalibur.Tests.A3.Policy;

/// <summary>
/// Tests for OPA HTTP adapter: happy path, timeout, connection failure,
/// input mapping, and response parsing (Sprint 727 T.1 yfelyg).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class OpaAdapterHttpShould : IDisposable
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
		MockHttpHandler handler,
		Action<OpaOptions>? configureOverride = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();

		var builder = services.AddExcaliburA3()
			.UseOpaPolicy(configureOverride ?? (opts =>
			{
				opts.Endpoint = "http://opa-test:8181";
				opts.PolicyPath = "v1/data/authz/allow";
			}));

		// Replace the primary handler so HTTP calls go to our mock
		services.ConfigureHttpClientDefaults(b =>
			b.ConfigurePrimaryHttpMessageHandler(() => handler));

		_provider = services.BuildServiceProvider();
		return _provider.GetRequiredService<IAuthorizationEvaluator>();
	}

	// ──────────────────────────────────────────────
	// HTTP Happy Path
	// ──────────────────────────────────────────────

	[Fact]
	public async Task ReturnPermitWhenOpaReturnsResultTrue()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"result": true}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
		decision.Reason.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnDenyWhenOpaReturnsResultFalse()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"result": false}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnPermitWhenOpaReturnsNestedResultAllowTrue()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"result": {"allow": true}}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
	}

	[Fact]
	public async Task ReturnDenyWhenOpaReturnsNestedResultAllowFalse()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"result": {"allow": false}}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
	}

	// ──────────────────────────────────────────────
	// HTTP Error Handling (fail-closed)
	// ──────────────────────────────────────────────

	[Theory]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.BadRequest)]
	[InlineData(HttpStatusCode.Forbidden)]
	public async Task ReturnDenyOnHttpErrorStatusWhenFailClosed(HttpStatusCode statusCode)
	{
		using var handler = MockHttpHandler.WithJson(statusCode, "{}");

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://opa-test:8181";
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
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.InternalServerError, "{}");

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://opa-test:8181";
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
		using var handler = MockHttpHandler.WithTimeout();

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://opa-test:8181";
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
		using var handler = MockHttpHandler.WithTimeout();

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://opa-test:8181";
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
		using var handler = MockHttpHandler.WithConnectionFailure();

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://opa-test:8181";
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
		using var handler = MockHttpHandler.WithConnectionFailure();

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://opa-test:8181";
			opts.FailClosed = false;
		});

		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
		decision.Reason.ShouldContain("connection failed");
	}

	// ──────────────────────────────────────────────
	// Input Mapping
	// ──────────────────────────────────────────────

	[Fact]
	public async Task SendCorrectOpaInputJsonFormat()
	{
		byte[]? capturedBody = null;
		using var handler = MockHttpHandler.WithCapture(
			HttpStatusCode.OK,
			"""{"result": true}""",
			body => capturedBody = body);

		var evaluator = BuildEvaluator(handler);
		await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		capturedBody.ShouldNotBeNull();
		using var doc = JsonDocument.Parse(capturedBody);
		var root = doc.RootElement;

		// Verify OPA standard { "input": { "subject": ..., "action": ..., "resource": ... } }
		root.TryGetProperty("input", out var input).ShouldBeTrue();

		var subject = input.GetProperty("subject");
		subject.GetProperty("actorId").GetString().ShouldBe("user-42");
		subject.GetProperty("tenantId").GetString().ShouldBe("tenant-1");
		subject.GetProperty("attributes").GetProperty("role").GetString().ShouldBe("admin");

		var action = input.GetProperty("action");
		action.GetProperty("name").GetString().ShouldBe("Read");
		action.GetProperty("attributes").GetProperty("scope").GetString().ShouldBe("full");

		var resource = input.GetProperty("resource");
		resource.GetProperty("type").GetString().ShouldBe("Order");
		resource.GetProperty("id").GetString().ShouldBe("order-123");
		resource.GetProperty("attributes").GetProperty("region").GetString().ShouldBe("us-east");
	}

	[Fact]
	public async Task SendRequestToConfiguredPolicyPath()
	{
		Uri? capturedUri = null;
		using var handler = MockHttpHandler.WithUriCapture(
			HttpStatusCode.OK,
			"""{"result": true}""",
			uri => capturedUri = uri);

		var evaluator = BuildEvaluator(handler, opts =>
		{
			opts.Endpoint = "http://opa-test:8181";
			opts.PolicyPath = "v1/data/custom/policy";
		});

		await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		capturedUri.ShouldNotBeNull();
		capturedUri!.PathAndQuery.ShouldContain("v1/data/custom/policy");
	}

	// ──────────────────────────────────────────────
	// Response Parsing Edge Cases
	// ──────────────────────────────────────────────

	[Fact]
	public async Task ReturnDenyOnMissingResultProperty()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"decision": "allow"}""");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldContain("missing");
	}

	[Fact]
	public async Task ReturnDenyOnMalformedJsonResponse()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, "not-json");

		var evaluator = BuildEvaluator(handler);
		var decision = await evaluator.EvaluateAsync(
			s_subject, s_action, s_resource, CancellationToken.None);

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldContain("not valid JSON");
	}

	[Fact]
	public async Task HandleEmptyResponseBody()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, "");

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
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"result": true}""");

		var evaluator = BuildEvaluator(handler);

		await Should.ThrowAsync<ArgumentNullException>(
			() => evaluator.EvaluateAsync(null!, s_action, s_resource, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullAction()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"result": true}""");

		var evaluator = BuildEvaluator(handler);

		await Should.ThrowAsync<ArgumentNullException>(
			() => evaluator.EvaluateAsync(s_subject, null!, s_resource, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullResource()
	{
		using var handler = MockHttpHandler.WithJson(
			HttpStatusCode.OK, """{"result": true}""");

		var evaluator = BuildEvaluator(handler);

		await Should.ThrowAsync<ArgumentNullException>(
			() => evaluator.EvaluateAsync(s_subject, s_action, null!, CancellationToken.None));
	}

	// ──────────────────────────────────────────────
	// Input Mapping: no attributes / no tenantId
	// ──────────────────────────────────────────────

	[Fact]
	public async Task OmitTenantIdAndAttributesWhenNull()
	{
		var subjectNoTenant = new AuthorizationSubject("user-1", null, null);
		var actionNoAttrs = new AuthorizationAction("Write", null);
		var resourceNoAttrs = new AuthorizationResource("Doc", "d-1", null);

		byte[]? capturedBody = null;
		using var handler = MockHttpHandler.WithCapture(
			HttpStatusCode.OK,
			"""{"result": true}""",
			body => capturedBody = body);

		var evaluator = BuildEvaluator(handler);
		await evaluator.EvaluateAsync(
			subjectNoTenant, actionNoAttrs, resourceNoAttrs, CancellationToken.None);

		capturedBody.ShouldNotBeNull();
		using var doc = JsonDocument.Parse(capturedBody);
		var subject = doc.RootElement.GetProperty("input").GetProperty("subject");

		subject.TryGetProperty("tenantId", out _).ShouldBeFalse();
		subject.TryGetProperty("attributes", out _).ShouldBeFalse();

		var action = doc.RootElement.GetProperty("input").GetProperty("action");
		action.TryGetProperty("attributes", out _).ShouldBeFalse();

		var resource = doc.RootElement.GetProperty("input").GetProperty("resource");
		resource.TryGetProperty("attributes", out _).ShouldBeFalse();
	}

	// ──────────────────────────────────────────────
	// Shared mock HTTP handler
	// ──────────────────────────────────────────────

	internal sealed class MockHttpHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

		private MockHttpHandler(
			Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
		{
			_handler = handler;
		}

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
			=> _handler(request, cancellationToken);

		internal static MockHttpHandler WithJson(HttpStatusCode statusCode, string json)
		{
			return new MockHttpHandler((_, _) =>
			{
				var response = new HttpResponseMessage(statusCode)
				{
					Content = new StringContent(json, Encoding.UTF8, "application/json"),
				};
				return Task.FromResult(response);
			});
		}

		internal static MockHttpHandler WithTimeout()
		{
			return new MockHttpHandler((_, _) =>
				throw new TaskCanceledException(
					"The request was canceled due to the configured HttpClient.Timeout.",
					new TimeoutException()));
		}

		internal static MockHttpHandler WithConnectionFailure()
		{
			return new MockHttpHandler((_, _) =>
				throw new HttpRequestException("Connection refused"));
		}

		internal static MockHttpHandler WithCapture(
			HttpStatusCode statusCode,
			string json,
			Action<byte[]> captureBody)
		{
			return new MockHttpHandler(async (request, _) =>
			{
				if (request.Content is not null)
				{
					var body = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
					captureBody(body);
				}

				return new HttpResponseMessage(statusCode)
				{
					Content = new StringContent(json, Encoding.UTF8, "application/json"),
				};
			});
		}

		internal static MockHttpHandler WithUriCapture(
			HttpStatusCode statusCode,
			string json,
			Action<Uri?> captureUri)
		{
			return new MockHttpHandler((request, _) =>
			{
				captureUri(request.RequestUri);
				return Task.FromResult(new HttpResponseMessage(statusCode)
				{
					Content = new StringContent(json, Encoding.UTF8, "application/json"),
				});
			});
		}
	}
}
