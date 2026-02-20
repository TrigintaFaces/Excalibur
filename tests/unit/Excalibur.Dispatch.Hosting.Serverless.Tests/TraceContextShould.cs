// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="TraceContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TraceContextShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreEmptyStrings()
	{
		// Act
		var tc = new TraceContext();

		// Assert
		tc.TraceId.ShouldBe(string.Empty);
		tc.SpanId.ShouldBe(string.Empty);
		tc.ParentSpanId.ShouldBeNull();
		tc.TraceFlags.ShouldBeNull();
		tc.TraceState.ShouldBeNull();
		tc.TraceParent.ShouldBeNull();
	}

	[Fact]
	public void InitSyntax_SetsProperties()
	{
		// Act
		var tc = new TraceContext
		{
			TraceId = "trace-123",
			SpanId = "span-456",
			ParentSpanId = "parent-789",
			TraceFlags = "01",
			TraceState = "key=value",
			TraceParent = "00-trace-123-span-456-01",
		};

		// Assert
		tc.TraceId.ShouldBe("trace-123");
		tc.SpanId.ShouldBe("span-456");
		tc.ParentSpanId.ShouldBe("parent-789");
		tc.TraceFlags.ShouldBe("01");
		tc.TraceState.ShouldBe("key=value");
		tc.TraceParent.ShouldBe("00-trace-123-span-456-01");
	}

	[Fact]
	public void RecordEquality_WorksCorrectly()
	{
		// Arrange
		var tc1 = new TraceContext { TraceId = "abc", SpanId = "def" };
		var tc2 = new TraceContext { TraceId = "abc", SpanId = "def" };
		var tc3 = new TraceContext { TraceId = "xyz", SpanId = "def" };

		// Assert
		tc1.ShouldBe(tc2);
		tc1.ShouldNotBe(tc3);
	}
}
