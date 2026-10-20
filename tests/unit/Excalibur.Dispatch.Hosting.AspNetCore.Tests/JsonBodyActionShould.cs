// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Pattern", "Integration")]
[SuppressMessage("Trimming", "IL2026")]
[SuppressMessage("AOT", "IL3050")]
[SuppressMessage("Design", "CA1034", Justification = "Public DTOs and handlers are discovered by ASP.NET and Dispatch.")]
public sealed class JsonBodyActionShould : UnitTestBase
{
	[Theory]
	[InlineData("/body", false)]
	[InlineData("/mapped", true)]
	public async Task BindJsonAndExecuteExactlyOnce(string route, bool mapped)
	{
		await using var app = await CreateHostAsync().ConfigureAwait(false);
		using var client = app.GetTestClient();
		using var response = await client.PostAsJsonAsync(new Uri(route + "?Product=query&Quantity=99", UriKind.Relative), new Request("widget", 7)).ConfigureAwait(false);
		response.StatusCode.ShouldBe(mapped ? HttpStatusCode.Created : HttpStatusCode.OK);
		(await response.Content.ReadFromJsonAsync<int>().ConfigureAwait(false)).ShouldBe(7);
		var effects = app.Services.GetRequiredService<Effects>();
		effects.Calls.ShouldBe(1);
		effects.Product.ShouldBe("widget");
		effects.Factories.ShouldBe(mapped ? 1 : 0);
		effects.Callbacks.ShouldBe(mapped ? 1 : 0);
	}

	[Theory]
	[InlineData("/body", false)]
	[InlineData("/body", true)]
	[InlineData("/mapped", false)]
	[InlineData("/mapped", true)]
	public async Task RefuseMissingOrMalformedJsonBeforeAnyEffect(string route, bool malformed)
	{
		await using var app = await CreateHostAsync().ConfigureAwait(false);
		using var client = app.GetTestClient();
		using var content = malformed ? new StringContent("{broken", Encoding.UTF8, "application/json") : null;
		using var response = await client.PostAsync(new Uri(route, UriKind.Relative), content).ConfigureAwait(false);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var effects = app.Services.GetRequiredService<Effects>();
		effects.Calls.ShouldBe(0);
		effects.Factories.ShouldBe(0);
		effects.Callbacks.ShouldBe(0);
	}

	[Fact]
	public async Task PreserveLegacyQueryBoundPost()
	{
		await using var app = await CreateHostAsync().ConfigureAwait(false);
		using var client = app.GetTestClient();
		using var response = await client.PostAsync(new Uri("/query?Product=legacy&Quantity=9", UriKind.Relative), null).ConfigureAwait(false);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await response.Content.ReadFromJsonAsync<int>().ConfigureAwait(false)).ShouldBe(9);
		app.Services.GetRequiredService<Effects>().Product.ShouldBe("legacy");
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task PreserveNullableQueryResponseWithValidation(bool validation, bool found)
	{
		await using var app = await CreateHostAsync(validation).ConfigureAwait(false);
		using var client = app.GetTestClient();
		using var response = await client.GetAsync(new Uri("/nullable?Found=" + found, UriKind.Relative)).ConfigureAwait(false);
		response.StatusCode.ShouldBe(found ? HttpStatusCode.OK : HttpStatusCode.NotFound);
		app.Services.GetRequiredService<Effects>().Calls.ShouldBe(1);
	}

	[Theory]
	[InlineData("/body")]
	[InlineData("/mapped")]
	public async Task RefuseExplicitJsonNull(string route)
	{
		await using var app = await CreateHostAsync().ConfigureAwait(false);
		using var client = app.GetTestClient();
		using var content = new StringContent("null", Encoding.UTF8, "application/json");
		using var response = await client.PostAsync(new Uri(route, UriKind.Relative), content).ConfigureAwait(false);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var effects = app.Services.GetRequiredService<Effects>();
		effects.Calls.ShouldBe(0);
		effects.Factories.ShouldBe(0);
		effects.Callbacks.ShouldBe(0);
	}

	private static async Task<WebApplication> CreateHostAsync(bool validation = false)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddSingleton<Effects>();
		builder.Services.AddDispatch(dispatch =>
		{
			dispatch.AddHandlersFromAssembly(typeof(JsonBodyActionShould).Assembly);
			if (validation)
			{
				dispatch.UseValidation().WithDataAnnotationsValidation();
			}
		});
		var app = builder.Build();
		app.DispatchPostBodyAction<Action, int>("/body");
		app.DispatchPostBodyAction<Request, Action, int>("/mapped", (request, context) =>
		{
			context.RequestServices.GetRequiredService<Effects>().Factories++;
			return new Action(request.Product, request.Quantity);
		}, (context, result) =>
		{
			context.RequestServices.GetRequiredService<Effects>().Callbacks++;
			return Results.Json(result.ReturnValue, statusCode: StatusCodes.Status201Created);
		});
		app.DispatchPostAction<Action, int>("/query");
		app.DispatchGetAction<NullableQuery, Payload?>("/nullable", static (_, result) =>
			result.ReturnValue is null ? Results.NotFound() : Results.Ok(result.ReturnValue));
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	public sealed record Request(string Product, int Quantity);
	public sealed record Action(string Product, int Quantity) : IDispatchAction<int>;
	public sealed record NullableQuery(bool Found) : IDispatchAction<Payload?>;
	public sealed record Payload(string Value);
	public sealed class Effects
	{
		public int Calls { get; set; }
		public int Factories { get; set; }
		public int Callbacks { get; set; }
		public string? Product { get; set; }
	}
	public sealed class Handler(Effects effects) : IActionHandler<Action, int>
	{
		public Task<int> HandleAsync(Action action, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			effects.Calls++;
			effects.Product = action.Product;
			return Task.FromResult(action.Quantity);
		}
	}
	public sealed class NullableHandler(Effects effects) : IActionHandler<NullableQuery, Payload?>
	{
		public Task<Payload?> HandleAsync(NullableQuery action, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			effects.Calls++;
			return Task.FromResult(action.Found ? new Payload("found") : null);
		}
	}
}
