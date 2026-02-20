// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Tests.Serverless;

/// <summary>
/// Unit tests for <see cref="TraceContext" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TraceContextShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var context = new TraceContext();

		// Assert
		context.TraceId.ShouldBe(string.Empty);
		context.SpanId.ShouldBe(string.Empty);
		context.ParentSpanId.ShouldBeNull();
		context.TraceFlags.ShouldBeNull();
		context.TraceState.ShouldBeNull();
		context.TraceParent.ShouldBeNull();
	}

	[Fact]
	public void TraceId_CanBeSetViaInitializer()
	{
		// Arrange
		const string traceId = "1234567890abcdef1234567890abcdef";

		// Act
		var context = new TraceContext { TraceId = traceId };

		// Assert
		context.TraceId.ShouldBe(traceId);
	}

	[Fact]
	public void SpanId_CanBeSetViaInitializer()
	{
		// Arrange
		const string spanId = "1234567890abcdef";

		// Act
		var context = new TraceContext { SpanId = spanId };

		// Assert
		context.SpanId.ShouldBe(spanId);
	}

	[Fact]
	public void TraceFlags_CanBeSetViaInitializer()
	{
		// Arrange
		const string traceFlags = "01";

		// Act
		var context = new TraceContext { TraceFlags = traceFlags };

		// Assert
		context.TraceFlags.ShouldBe(traceFlags);
	}

	[Fact]
	public void TraceState_CanBeSetViaInitializer()
	{
		// Arrange
		const string traceState = "vendorkey=value";

		// Act
		var context = new TraceContext { TraceState = traceState };

		// Assert
		context.TraceState.ShouldBe(traceState);
	}

	[Fact]
	public void ParentSpanId_CanBeSetViaInitializer()
	{
		// Arrange
		const string parentSpanId = "abcdef1234567890";

		// Act
		var context = new TraceContext { ParentSpanId = parentSpanId };

		// Assert
		context.ParentSpanId.ShouldBe(parentSpanId);
	}

	[Fact]
	public void TraceParent_CanBeSetViaInitializer()
	{
		// Arrange
		const string traceParent = "00-1234567890abcdef1234567890abcdef-1234567890abcdef-01";

		// Act
		var context = new TraceContext { TraceParent = traceParent };

		// Assert
		context.TraceParent.ShouldBe(traceParent);
	}

	[Fact]
	public void WithExpression_CreatesNewInstanceWithModifiedProperty()
	{
		// Arrange
		var original = new TraceContext { TraceId = "original-trace-id", SpanId = "original-span-id" };

		// Act
		var modified = original with { TraceId = "modified-trace-id" };

		// Assert
		modified.TraceId.ShouldBe("modified-trace-id");
		modified.SpanId.ShouldBe("original-span-id");
		original.TraceId.ShouldBe("original-trace-id");
	}
}
