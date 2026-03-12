// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Observability.Sanitization;
using Excalibur.Dispatch.Options.Core;
using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sanitization;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SensitiveDataPostConfigureOptionsShould
{
	[Fact]
	public void SetIncludeSensitiveDataOnTracingOptionsWhenPiiEnabled()
	{
		// Arrange
		var sanitizerOptions = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sut = new SensitiveDataPostConfigureOptions(sanitizerOptions);
		var tracingOptions = new TracingOptions { IncludeSensitiveData = false };

		// Act
		sut.PostConfigure(null, tracingOptions);

		// Assert
		tracingOptions.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void NotChangeTracingOptionsWhenPiiDisabled()
	{
		// Arrange
		var sanitizerOptions = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions { IncludeRawPii = false });
		var sut = new SensitiveDataPostConfigureOptions(sanitizerOptions);
		var tracingOptions = new TracingOptions { IncludeSensitiveData = false };

		// Act
		sut.PostConfigure(null, tracingOptions);

		// Assert
		tracingOptions.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void SetIncludeSensitiveDataOnAuditLoggingOptionsWhenPiiEnabled()
	{
		// Arrange
		var sanitizerOptions = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sut = new SensitiveDataPostConfigureOptions(sanitizerOptions);
		var auditOptions = new AuditLoggingOptions { IncludeSensitiveData = false };

		// Act
		sut.PostConfigure(null, auditOptions);

		// Assert
		auditOptions.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void SetIncludeSensitiveDataOnObservabilityOptionsWhenPiiEnabled()
	{
		// Arrange
		var sanitizerOptions = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sut = new SensitiveDataPostConfigureOptions(sanitizerOptions);
		var obsOptions = new ObservabilityOptions { IncludeSensitiveData = false };

		// Act
		sut.PostConfigure(null, obsOptions);

		// Assert
		obsOptions.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullTracingOptions()
	{
		var sanitizerOptions = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sut = new SensitiveDataPostConfigureOptions(sanitizerOptions);

		Should.Throw<ArgumentNullException>(() =>
			sut.PostConfigure(null, (TracingOptions)null!));
	}

	[Fact]
	public void ThrowOnNullAuditLoggingOptions()
	{
		var sanitizerOptions = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sut = new SensitiveDataPostConfigureOptions(sanitizerOptions);

		Should.Throw<ArgumentNullException>(() =>
			sut.PostConfigure(null, (AuditLoggingOptions)null!));
	}

	[Fact]
	public void ThrowOnNullObservabilityOptions()
	{
		var sanitizerOptions = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sut = new SensitiveDataPostConfigureOptions(sanitizerOptions);

		Should.Throw<ArgumentNullException>(() =>
			sut.PostConfigure(null, (ObservabilityOptions)null!));
	}

	[Fact]
	public void HandleNullSanitizerOptionsGracefully()
	{
		// Arrange - null IOptions should default to false
		var sut = new SensitiveDataPostConfigureOptions(null!);
		var tracingOptions = new TracingOptions { IncludeSensitiveData = false };

		// Act - should not throw, should not change the option
		sut.PostConfigure(null, tracingOptions);

		// Assert
		tracingOptions.IncludeSensitiveData.ShouldBeFalse();
	}
}
