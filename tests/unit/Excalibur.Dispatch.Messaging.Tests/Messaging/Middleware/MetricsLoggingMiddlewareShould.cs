// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
///     Tests for the <see cref="MetricsLoggingMiddleware" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MetricsLoggingMiddlewareShould
{
	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new MetricsLoggingMiddleware(
				null!,
				A.Fake<IMessageMetrics>(),
				A.Fake<ITelemetrySanitizer>(),
				NullLogger<MetricsLoggingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullMessageMetrics() =>
		Should.Throw<ArgumentNullException>(() =>
			new MetricsLoggingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
				null!,
				A.Fake<ITelemetrySanitizer>(),
				NullLogger<MetricsLoggingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullSanitizer() =>
		Should.Throw<ArgumentNullException>(() =>
			new MetricsLoggingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
				A.Fake<IMessageMetrics>(),
				null!,
				NullLogger<MetricsLoggingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new MetricsLoggingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
				A.Fake<IMessageMetrics>(),
				A.Fake<ITelemetrySanitizer>(),
				null!));

	[Fact]
	public void CreateSuccessfully()
	{
		var sut = new MetricsLoggingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void HaveMetricsStage()
	{
		var sut = new MetricsLoggingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		sut.Stage.ShouldBe(DispatchMiddlewareStage.End);
	}

	[Fact]
	public void ApplyToAllMessages()
	{
		var sut = new MetricsLoggingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		sut.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
	}

	[Fact]
	public void ImplementIDispatchMiddleware()
	{
		var sut = new MetricsLoggingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new MetricsLoggingOptions()),
			A.Fake<IMessageMetrics>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<MetricsLoggingMiddleware>.Instance);

		sut.ShouldBeAssignableTo<IDispatchMiddleware>();
	}
}
