// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Extensions;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Extensions;

/// <summary>
/// Unit tests for <see cref="TraceContextExtensions"/>.
/// </summary>
/// <remarks>
/// Tests the trace context extension methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Extensions")]
[Trait("Priority", "0")]
public sealed class TraceContextExtensionsShould
{
	#region GetTraceParent Tests

	[Fact]
	public void GetTraceParent_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		IMessageContext context = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.GetTraceParent());
	}

	[Fact]
	public void GetTraceParent_WithContextTraceParent_ReturnsContextValue()
	{
		// Arrange
		var traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.TraceParent).Returns(traceParent);

		// Act
		var result = context.GetTraceParent();

		// Assert
		result.ShouldBe(traceParent);
	}

	[Fact]
	public void GetTraceParent_WithNullContextTraceParentAndNoActivity_ReturnsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.TraceParent).Returns(null);

		// Ensure no current activity
		Activity.Current = null;

		// Act
		var result = context.GetTraceParent();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetTraceParent_WithNullContextTraceParentAndCurrentActivity_ReturnsActivityId()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.TraceParent).Returns(null);

		using var activity = new Activity("TestOperation");
		_ = activity.Start();

		try
		{
			// Act
			var result = context.GetTraceParent();

			// Assert
			_ = result.ShouldNotBeNull();
			result.ShouldBe(activity.Id);
		}
		finally
		{
			activity.Stop();
		}
	}

	[Fact]
	public void GetTraceParent_PrioritizesContextValueOverActivity()
	{
		// Arrange
		var contextTraceParent = "context-trace-parent";
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.TraceParent).Returns(contextTraceParent);

		using var activity = new Activity("TestOperation");
		_ = activity.Start();

		try
		{
			// Act
			var result = context.GetTraceParent();

			// Assert - Context value takes precedence
			result.ShouldBe(contextTraceParent);
			result.ShouldNotBe(activity.Id);
		}
		finally
		{
			activity.Stop();
		}
	}

	#endregion

	#region Activity Context Tests

	[Fact]
	public void GetTraceParent_WithW3CActivityFormat_ReturnsValidTraceId()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.TraceParent).Returns(null);

		using var activity = new Activity("TestOperation");
		_ = activity.SetIdFormat(ActivityIdFormat.W3C);
		_ = activity.Start();

		try
		{
			// Act
			var result = context.GetTraceParent();

			// Assert
			_ = result.ShouldNotBeNull();
			// W3C format: 00-{traceId}-{spanId}-{flags}
			result.ShouldStartWith("00-");
		}
		finally
		{
			activity.Stop();
		}
	}

	#endregion
}
