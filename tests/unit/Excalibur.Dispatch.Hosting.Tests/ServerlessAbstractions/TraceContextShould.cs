// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Tests.ServerlessAbstractions;

/// <summary>
/// Unit tests for TraceContext.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TraceContextShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
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
	public void TraceId_CanBeSet()
	{
		// Arrange & Act
		var context = new TraceContext
		{
			TraceId = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
		};

		// Assert
		context.TraceId.ShouldBe("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
	}

	[Fact]
	public void SpanId_CanBeSet()
	{
		// Arrange & Act
		var context = new TraceContext
		{
			SpanId = "00f067aa0ba902b7"
		};

		// Assert
		context.SpanId.ShouldBe("00f067aa0ba902b7");
	}

	[Fact]
	public void ParentSpanId_CanBeSet()
	{
		// Arrange & Act
		var context = new TraceContext
		{
			ParentSpanId = "b7ad6b7169203331"
		};

		// Assert
		context.ParentSpanId.ShouldBe("b7ad6b7169203331");
	}

	[Fact]
	public void TraceParent_CanBeSet()
	{
		// Arrange
		var traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

		// Act
		var context = new TraceContext
		{
			TraceParent = traceParent
		};

		// Assert
		context.TraceParent.ShouldBe(traceParent);
	}
}
