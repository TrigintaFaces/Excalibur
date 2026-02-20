// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Observability.Sanitization;
using Excalibur.Dispatch.Options.Core;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Tests for <see cref="SensitiveDataPostConfigureOptions"/> verifying that
/// <see cref="TelemetrySanitizerOptions.IncludeRawPii"/> flows into the
/// <c>IncludeSensitiveData</c> property on TracingOptions, AuditLoggingOptions,
/// and ObservabilityOptions (C.9).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class SensitiveDataPostConfigureOptionsShould
{
	private static SensitiveDataPostConfigureOptions CreatePostConfigure(bool includeRawPii)
	{
		var sanitizerOptions = MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = includeRawPii });
		return new SensitiveDataPostConfigureOptions(sanitizerOptions);
	}

	#region TracingOptions

	[Fact]
	public void SetIncludeSensitiveDataOnTracingOptionsWhenIncludeRawPiiTrue()
	{
		// Arrange
		var postConfigure = CreatePostConfigure(includeRawPii: true);
		var options = new TracingOptions { IncludeSensitiveData = false };

		// Act
		((IPostConfigureOptions<TracingOptions>)postConfigure).PostConfigure(null, options);

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void NotChangeTracingOptionsWhenIncludeRawPiiFalse()
	{
		// Arrange
		var postConfigure = CreatePostConfigure(includeRawPii: false);
		var options = new TracingOptions { IncludeSensitiveData = false };

		// Act
		((IPostConfigureOptions<TracingOptions>)postConfigure).PostConfigure(null, options);

		// Assert
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void PreserveExplicitTracingOptionsWhenIncludeRawPiiFalse()
	{
		// Arrange — consumer explicitly set IncludeSensitiveData=true
		var postConfigure = CreatePostConfigure(includeRawPii: false);
		var options = new TracingOptions { IncludeSensitiveData = true };

		// Act
		((IPostConfigureOptions<TracingOptions>)postConfigure).PostConfigure(null, options);

		// Assert — not overridden by IncludeRawPii=false
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullTracingOptions()
	{
		var postConfigure = CreatePostConfigure(includeRawPii: true);

		Should.Throw<ArgumentNullException>(() =>
			((IPostConfigureOptions<TracingOptions>)postConfigure).PostConfigure(null, null!));
	}

	#endregion

	#region AuditLoggingOptions

	[Fact]
	public void SetIncludeSensitiveDataOnAuditLoggingOptionsWhenIncludeRawPiiTrue()
	{
		// Arrange
		var postConfigure = CreatePostConfigure(includeRawPii: true);
		var options = new AuditLoggingOptions { IncludeSensitiveData = false };

		// Act
		((IPostConfigureOptions<AuditLoggingOptions>)postConfigure).PostConfigure(null, options);

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void NotChangeAuditLoggingOptionsWhenIncludeRawPiiFalse()
	{
		// Arrange
		var postConfigure = CreatePostConfigure(includeRawPii: false);
		var options = new AuditLoggingOptions { IncludeSensitiveData = false };

		// Act
		((IPostConfigureOptions<AuditLoggingOptions>)postConfigure).PostConfigure(null, options);

		// Assert
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void PreserveExplicitAuditLoggingOptionsWhenIncludeRawPiiFalse()
	{
		// Arrange — consumer explicitly set IncludeSensitiveData=true
		var postConfigure = CreatePostConfigure(includeRawPii: false);
		var options = new AuditLoggingOptions { IncludeSensitiveData = true };

		// Act
		((IPostConfigureOptions<AuditLoggingOptions>)postConfigure).PostConfigure(null, options);

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullAuditLoggingOptions()
	{
		var postConfigure = CreatePostConfigure(includeRawPii: true);

		Should.Throw<ArgumentNullException>(() =>
			((IPostConfigureOptions<AuditLoggingOptions>)postConfigure).PostConfigure(null, null!));
	}

	#endregion

	#region ObservabilityOptions

	[Fact]
	public void SetIncludeSensitiveDataOnObservabilityOptionsWhenIncludeRawPiiTrue()
	{
		// Arrange
		var postConfigure = CreatePostConfigure(includeRawPii: true);
		var options = new ObservabilityOptions { IncludeSensitiveData = false };

		// Act
		((IPostConfigureOptions<ObservabilityOptions>)postConfigure).PostConfigure(null, options);

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void NotChangeObservabilityOptionsWhenIncludeRawPiiFalse()
	{
		// Arrange
		var postConfigure = CreatePostConfigure(includeRawPii: false);
		var options = new ObservabilityOptions { IncludeSensitiveData = false };

		// Act
		((IPostConfigureOptions<ObservabilityOptions>)postConfigure).PostConfigure(null, options);

		// Assert
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void PreserveExplicitObservabilityOptionsWhenIncludeRawPiiFalse()
	{
		// Arrange — consumer explicitly set IncludeSensitiveData=true
		var postConfigure = CreatePostConfigure(includeRawPii: false);
		var options = new ObservabilityOptions { IncludeSensitiveData = true };

		// Act
		((IPostConfigureOptions<ObservabilityOptions>)postConfigure).PostConfigure(null, options);

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullObservabilityOptions()
	{
		var postConfigure = CreatePostConfigure(includeRawPii: true);

		Should.Throw<ArgumentNullException>(() =>
			((IPostConfigureOptions<ObservabilityOptions>)postConfigure).PostConfigure(null, null!));
	}

	#endregion
}
