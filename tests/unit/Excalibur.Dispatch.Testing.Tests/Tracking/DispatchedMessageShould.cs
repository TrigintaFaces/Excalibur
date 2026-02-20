// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing.Tracking;

namespace Excalibur.Dispatch.Testing.Tests.Tracking;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchedMessageShould
{
	[Fact]
	public void StoreAllPropertiesFromConstructor()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var timestamp = DateTimeOffset.UtcNow;
		var result = A.Fake<IMessageResult>();

		// Act
		var dispatched = new DispatchedMessage(message, context, timestamp, result);

		// Assert
		dispatched.Message.ShouldBeSameAs(message);
		dispatched.Context.ShouldBeSameAs(context);
		dispatched.Timestamp.ShouldBe(timestamp);
		dispatched.Result.ShouldBeSameAs(result);
	}

	[Fact]
	public void AllowNullResult()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var dispatched = new DispatchedMessage(message, context, timestamp, null);

		// Assert
		dispatched.Result.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var timestamp = DateTimeOffset.UtcNow;
		var result = A.Fake<IMessageResult>();

		var d1 = new DispatchedMessage(message, context, timestamp, result);
		var d2 = new DispatchedMessage(message, context, timestamp, result);

		// Act & Assert
		d1.ShouldBe(d2);
		d1.GetHashCode().ShouldBe(d2.GetHashCode());
	}

	[Fact]
	public void SupportRecordWithDeconstruction()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var timestamp = DateTimeOffset.UtcNow;
		var result = A.Fake<IMessageResult>();

		var dispatched = new DispatchedMessage(message, context, timestamp, result);

		// Act - use with expression
		var newResult = A.Fake<IMessageResult>();
		var modified = dispatched with { Result = newResult };

		// Assert
		modified.Result.ShouldBeSameAs(newResult);
		modified.Message.ShouldBeSameAs(message);
		modified.Context.ShouldBeSameAs(context);
		modified.Timestamp.ShouldBe(timestamp);
	}
}
