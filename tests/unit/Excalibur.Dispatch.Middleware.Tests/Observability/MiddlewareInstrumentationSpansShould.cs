// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Telemetry;

// Aliased, not wildcard-imported: Excalibur.Dispatch.Validation also declares a ValidationOptions,
// distinct from Excalibur.Dispatch.Options.Middleware.ValidationOptions used below.
using NoOpValidatorResolver = Excalibur.Dispatch.Validation.NoOpValidatorResolver;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability;

/// <summary>
/// Engage-test (bead 4yidqt) for the previously-uninstrumented middleware: invoking each must start its
/// named OpenTelemetry activity on its dedicated <see cref="ActivitySource"/>.
/// </summary>
/// <remarks>
/// <b>Non-vacuity (ADR-336):</b> on the pre-fix path these four middleware had no <c>ActivitySource</c> /
/// <c>StartActivity</c>, so no activity was emitted — the listener captures nothing → RED. Post-fix each
/// starts <c>&lt;Name&gt;.Invoke</c> on <c>Excalibur.Dispatch.&lt;Name&gt;</c> → GREEN. The activity starts at the
/// top of <c>InvokeAsync</c> (before the body), so a faked-dependency invocation captures it even if the body
/// short-circuits; any downstream throw is irrelevant to the span having started.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Feature", "Observability")]
public sealed class MiddlewareInstrumentationSpansShould : IDisposable
{
	private readonly List<Activity> _captured = [];
	private readonly ActivityListener _listener;

	public MiddlewareInstrumentationSpansShould()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.StartsWith("Excalibur.Dispatch.", StringComparison.Ordinal),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = activity => _captured.Add(activity),
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		foreach (var activity in _captured)
		{
			activity.Dispose();
		}
	}

	[Fact]
	public Task AuthenticationMiddleware_StartsItsInvokeActivity() =>
		AssertStartsActivityAsync(
			new AuthenticationMiddleware(
				Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions()),
				A.Fake<IAuthenticationService>(),
				A.Fake<ITelemetrySanitizer>(),
				NullLogger<AuthenticationMiddleware>.Instance),
			DispatchTelemetryConstants.ActivitySources.AuthenticationMiddleware,
			"AuthenticationMiddleware.Invoke");

	[Fact]
	public Task AuthorizationMiddleware_StartsItsInvokeActivity() =>
		AssertStartsActivityAsync(
			new AuthorizationMiddleware(
				Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()),
				A.Fake<IAuthorizationService>(),
				A.Fake<ITelemetrySanitizer>(),
				NullLogger<AuthorizationMiddleware>.Instance),
			DispatchTelemetryConstants.ActivitySources.AuthorizationMiddleware,
			"AuthorizationMiddleware.Invoke");

	[Fact]
	public Task ValidationMiddleware_StartsItsInvokeActivity() =>
		AssertStartsActivityAsync(
			new ValidationMiddleware(
				Microsoft.Extensions.Options.Options.Create(new ValidationOptions()),
				A.Fake<IMessageValidationService>(),
				new NoOpValidatorResolver(),
				NullLogger<ValidationMiddleware>.Instance),
			DispatchTelemetryConstants.ActivitySources.ValidationMiddleware,
			"ValidationMiddleware.Invoke");

	private async Task AssertStartsActivityAsync(IDispatchMiddleware middleware, string sourceName, string operationName)
	{
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult<IMessageResult>(MessageResult.Success());

		try
		{
			_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None).ConfigureAwait(false);
		}
		catch
		{
			// The activity starts at the top of InvokeAsync (before the body); a downstream throw on faked
			// dependencies does not undo the captured span — that's exactly what this engage-test asserts.
		}

		_captured.ShouldContain(
			a => a.Source.Name == sourceName && a.OperationName == operationName,
			$"4yidqt — invoking {middleware.GetType().Name} must start activity '{operationName}' on source '{sourceName}' (RED on the pre-fix uninstrumented path).");
	}
}
