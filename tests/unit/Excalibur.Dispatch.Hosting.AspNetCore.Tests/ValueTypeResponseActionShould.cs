// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// author≠impl lock for the value-type response relaxation on the Dispatch minimal-API action
/// extensions. The <c>DispatchPostAction&lt;TAction, TResponse&gt;</c> / <c>DispatchGetAction&lt;TAction,
/// TResponse&gt;</c> overloads (and their siblings) previously constrained <c>TResponse : class</c>,
/// which made a value-type response (<see cref="Guid"/>, <see cref="int"/>) inexpressible — the exact
/// "command returns the created id" scenario the handler interface documents. The relaxation drops that
/// constraint on all POST/GET/PUT/DELETE response overloads.
/// </summary>
/// <remarks>
/// <para>
/// <b>NON-VACUITY / RED (compile-time):</b> the constraint is a generic type constraint, so the binding
/// proof is a COMPILE proof, not a runtime one. Under the pre-relax signature
/// <c>where TResponse : class</c>, the calls <c>app.DispatchPostAction&lt;NewIdAction, Guid&gt;(…)</c> and
/// <c>app.DispatchPostAction&lt;CounterAction, int&gt;(…)</c> below fail with <c>CS0452</c>
/// ("the type 'Guid' must be a reference type"), so this file does not compile against the old surface.
/// GREEN = it compiles against the relaxed surface AND round-trips the value over HTTP.
/// Verified: committed baseline carried <c>where TResponse : class</c> on the 12 relaxed sites
/// (POST/GET/PUT/DELETE response overloads across <c>EndpointRouteBuilderExtensions</c>,
/// <c>ControllerBaseExtensions</c>, <c>RouteMessageHandlerFactory</c>).
/// </para>
/// <para>
/// <b>LIVENESS</b> (the arm that matters): a value-type response is actually produced and serialized —
/// drive the endpoint, assert HTTP 200 and body == the known value. Two distinct value types (Guid, int)
/// prove it is the CONSTRAINT DROP, not a Guid-special-case. <b>Regression</b>: a reference-type response
/// still returns 200 — the relaxation did not break <c>class</c> responses.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.")]
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Action/response types must be public for ASP.NET minimal API [AsParameters] binding and handler discovery.")]
public sealed class ValueTypeResponseActionShould : UnitTestBase
{
	private const string GuidPostRoute = "/vt/guid-post";
	private const string GuidGetRoute = "/vt/guid-get";
	private const string IntPostRoute = "/vt/int-post";
	private const string DtoPostRoute = "/vt/dto-post";

	private static Uri Rel(string route) => new(route, UriKind.Relative);

	private static readonly Guid KnownId = Guid.Parse("11111111-2222-3333-4444-555555555555");
	private const int KnownCount = 4242;
	private const string KnownPayload = "created";

	[Fact]
	public async Task PostAction_WithValueTypeGuidResponse_Returns200AndTheGuid()
	{
		// Arrange: DispatchPostAction<NewIdAction, Guid> — value-type TResponse (Guid). RED under
		// `where TResponse : class` (CS0452); this line only compiles because the constraint was relaxed.
		await using var host = await CreateHostAsync().ConfigureAwait(false);
		using var client = host.GetTestClient();

		// Act: drive the endpoint (POST, empty body — action is parameterless).
		using var response = await client.PostAsync(Rel(GuidPostRoute), content: null).ConfigureAwait(false);

		// Assert LIVENESS: the value-type response was produced and serialized.
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(false);
		body.ShouldBe(KnownId, "the Guid handler result must round-trip through the value-type response overload");
	}

	[Fact]
	public async Task GetAction_WithValueTypeGuidResponse_Returns200AndTheGuid()
	{
		// Arrange: DispatchGetAction<NewIdAction, Guid> — GET variant of the same value-type relaxation.
		await using var host = await CreateHostAsync().ConfigureAwait(false);
		using var client = host.GetTestClient();

		// Act
		using var response = await client.GetAsync(Rel(GuidGetRoute)).ConfigureAwait(false);

		// Assert LIVENESS
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Guid>().ConfigureAwait(false);
		body.ShouldBe(KnownId, "the GET value-type response overload must also serialize the Guid");
	}

	[Fact]
	public async Task PostAction_WithSecondValueType_Int_Returns200AndTheInt()
	{
		// Arrange: a SECOND value type (int) proves the relaxation is the constraint drop, not Guid-specific.
		await using var host = await CreateHostAsync().ConfigureAwait(false);
		using var client = host.GetTestClient();

		// Act
		using var response = await client.PostAsync(Rel(IntPostRoute), content: null).ConfigureAwait(false);

		// Assert LIVENESS
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<int>().ConfigureAwait(false);
		body.ShouldBe(KnownCount, "a struct response other than Guid must also work — proves the constraint drop, not a Guid special case");
	}

	[Fact]
	public async Task PostAction_WithReferenceTypeResponse_StillReturns200()
	{
		// Arrange REGRESSION: dropping `where TResponse : class` must NOT break a reference-type response.
		await using var host = await CreateHostAsync().ConfigureAwait(false);
		using var client = host.GetTestClient();

		// Act
		using var response = await client.PostAsync(Rel(DtoPostRoute), content: null).ConfigureAwait(false);

		// Assert: class responses are unaffected by the relaxation.
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PayloadDto>().ConfigureAwait(false);
		body.ShouldNotBeNull();
		body!.Value.ShouldBe(KnownPayload, "a reference-type response must still round-trip after the constraint relaxation");
	}

	private static async Task<WebApplication> CreateHostAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();

		// AddDispatch(assembly) auto-discovers the IActionHandler<,> implementations declared below.
		_ = builder.Services.AddDispatch(typeof(ValueTypeResponseActionShould).Assembly);

		var app = builder.Build();

		// Ensure an authenticated principal for the dispatch pipeline (mirrors sibling endpoint tests).
		app.Use(static async (httpContext, next) =>
		{
			httpContext.User = new ClaimsPrincipal(
				new ClaimsIdentity(
					[new Claim(ClaimTypes.NameIdentifier, "value-type-lock")],
					authenticationType: "TestAuth"));
			await next().ConfigureAwait(false);
		});

		// The load-bearing lines: these generic instantiations are CS0452 under `where TResponse : class`.
		_ = app.DispatchPostAction<NewIdAction, Guid>(GuidPostRoute);
		_ = app.DispatchGetAction<NewIdAction, Guid>(GuidGetRoute);
		_ = app.DispatchPostAction<CounterAction, int>(IntPostRoute);
		_ = app.DispatchPostAction<DtoAction, PayloadDto>(DtoPostRoute);

		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	#region Fixtures

	/// <summary>Action whose response is a value type (<see cref="Guid"/>).</summary>
	public sealed record NewIdAction : IDispatchAction<Guid>;

	/// <summary>Action whose response is a second, distinct value type (<see cref="int"/>).</summary>
	public sealed record CounterAction : IDispatchAction<int>;

	/// <summary>Action whose response is a reference type — the regression control.</summary>
	public sealed record DtoAction : IDispatchAction<PayloadDto>;

	/// <summary>Reference-type response payload for the regression arm.</summary>
	public sealed record PayloadDto(string Value);

	private sealed class NewIdActionHandler : IActionHandler<NewIdAction, Guid>
	{
		public Task<Guid> HandleAsync(NewIdAction action, CancellationToken cancellationToken)
		{
			_ = action;
			_ = cancellationToken;
			return Task.FromResult(KnownId);
		}
	}

	private sealed class CounterActionHandler : IActionHandler<CounterAction, int>
	{
		public Task<int> HandleAsync(CounterAction action, CancellationToken cancellationToken)
		{
			_ = action;
			_ = cancellationToken;
			return Task.FromResult(KnownCount);
		}
	}

	private sealed class DtoActionHandler : IActionHandler<DtoAction, PayloadDto>
	{
		public Task<PayloadDto> HandleAsync(DtoAction action, CancellationToken cancellationToken)
		{
			_ = action;
			_ = cancellationToken;
			return Task.FromResult(new PayloadDto(KnownPayload));
		}
	}

	#endregion Fixtures
}
