// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
///     Tests for the <see cref="AuthenticationMiddleware" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthenticationMiddlewareShould
{
	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new AuthenticationMiddleware(
				null!,
				A.Fake<IAuthenticationService>(),
				A.Fake<ITelemetrySanitizer>(),
				NullLogger<AuthenticationMiddleware>.Instance));

	[Fact]
	public void ThrowForNullAuthenticationService() =>
		Should.Throw<ArgumentNullException>(() =>
			new AuthenticationMiddleware(
				Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions()),
				null!,
				A.Fake<ITelemetrySanitizer>(),
				NullLogger<AuthenticationMiddleware>.Instance));

	[Fact]
	public void ThrowForNullSanitizer() =>
		Should.Throw<ArgumentNullException>(() =>
			new AuthenticationMiddleware(
				Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions()),
				A.Fake<IAuthenticationService>(),
				null!,
				NullLogger<AuthenticationMiddleware>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new AuthenticationMiddleware(
				Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions()),
				A.Fake<IAuthenticationService>(),
				A.Fake<ITelemetrySanitizer>(),
				null!));

	[Fact]
	public void CreateSuccessfully()
	{
		var sut = new AuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions()),
			A.Fake<IAuthenticationService>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<AuthenticationMiddleware>.Instance);

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void HaveAuthenticationStage()
	{
		var sut = new AuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions()),
			A.Fake<IAuthenticationService>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<AuthenticationMiddleware>.Instance);

		sut.Stage.ShouldBe(DispatchMiddlewareStage.Authentication);
	}

	[Fact]
	public void ApplyToActionMessages()
	{
		var sut = new AuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions()),
			A.Fake<IAuthenticationService>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<AuthenticationMiddleware>.Instance);

		sut.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void ImplementIDispatchMiddleware()
	{
		var sut = new AuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions()),
			A.Fake<IAuthenticationService>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<AuthenticationMiddleware>.Instance);

		sut.ShouldBeAssignableTo<IDispatchMiddleware>();
	}
}
