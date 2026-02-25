// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.CosmosDb.Cdc;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbStalePositionException"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify exception constructors and properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "CDC")]
public sealed class CosmosDbStalePositionExceptionShould
{
	#region Constructor Tests

	[Fact]
	public void DefaultConstructor_HasDefaultMessage()
	{
		// Arrange & Act
		var exception = new CosmosDbStalePositionException();

		// Assert
		exception.Message.ShouldNotBeEmpty();
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Arrange
		var message = "Custom error message";

		// Act
		var exception = new CosmosDbStalePositionException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void MessageAndInnerExceptionConstructor_SetsBoth()
	{
		// Arrange
		var message = "Custom error message";
		var inner = new InvalidOperationException("Inner error");

		// Act
		var exception = new CosmosDbStalePositionException(message, inner);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void EventArgsConstructor_SetsEventArgs()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "processor-1",
			ReasonCode = CosmosDbStalePositionReasonCodes.ContinuationTokenExpired,
			DetectedAt = DateTimeOffset.UtcNow
		};

		// Act
		var exception = new CosmosDbStalePositionException(eventArgs);

		// Assert
		exception.EventArgs.ShouldBe(eventArgs);
	}

	[Fact]
	public void MessageAndEventArgsConstructor_SetsBoth()
	{
		// Arrange
		var message = "Custom message";
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "processor-1",
			ReasonCode = CosmosDbStalePositionReasonCodes.PartitionSplit
		};

		// Act
		var exception = new CosmosDbStalePositionException(message, eventArgs);

		// Assert
		exception.Message.ShouldBe(message);
		exception.EventArgs.ShouldBe(eventArgs);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void ProcessorId_ReturnsFromEventArgs()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "processor-1"
		};
		var exception = new CosmosDbStalePositionException(eventArgs);

		// Assert
		exception.ProcessorId.ShouldBe("processor-1");
	}

	[Fact]
	public void ProcessorId_ReturnsNull_WhenNoEventArgs()
	{
		// Arrange
		var exception = new CosmosDbStalePositionException();

		// Assert
		exception.ProcessorId.ShouldBeNull();
	}

	[Fact]
	public void ReasonCode_ReturnsFromEventArgs()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			ReasonCode = CosmosDbStalePositionReasonCodes.ETagMismatch
		};
		var exception = new CosmosDbStalePositionException(eventArgs);

		// Assert
		exception.ReasonCode.ShouldBe(CosmosDbStalePositionReasonCodes.ETagMismatch);
	}

	[Fact]
	public void StalePosition_ReturnsFromEventArgs()
	{
		// Arrange
		var position = new byte[] { 1, 2, 3, 4 };
		var eventArgs = new CdcPositionResetEventArgs
		{
			StalePosition = position
		};
		var exception = new CosmosDbStalePositionException(eventArgs);

		// Assert
		exception.StalePosition.ShouldBe(position);
	}

	[Fact]
	public void DatabaseName_ReturnsFromEventArgs()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			DatabaseName = "test-db"
		};
		var exception = new CosmosDbStalePositionException(eventArgs);

		// Assert
		exception.DatabaseName.ShouldBe("test-db");
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromException()
	{
		// Assert
		typeof(CosmosDbStalePositionException).BaseType.ShouldBe(typeof(Exception));
	}

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(CosmosDbStalePositionException).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region HttpStatusCode Tests

	[Fact]
	public void HttpStatusCode_ReturnsFromAdditionalContext()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			AdditionalContext = new Dictionary<string, object>
			{
				["HttpStatusCode"] = 410
			}
		};
		var exception = new CosmosDbStalePositionException(eventArgs);

		// Assert
		exception.HttpStatusCode.ShouldBe(410);
	}

	[Fact]
	public void HttpStatusCode_ReturnsNull_WhenNotInContext()
	{
		// Arrange
		var eventArgs = new CdcPositionResetEventArgs
		{
			AdditionalContext = new Dictionary<string, object>()
		};
		var exception = new CosmosDbStalePositionException(eventArgs);

		// Assert
		exception.HttpStatusCode.ShouldBeNull();
	}

	#endregion
}
