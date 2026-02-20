// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

// Explicit alias to disambiguate from Excalibur.Outbox.OutboxOptions
using DispatchOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxOptions;

namespace Excalibur.Outbox.Tests.Core;

/// <summary>
/// Unit tests for <see cref="OutboxExtensions"/>.
/// Verifies outbox extension method behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxExtensionsShould
{
	#region MessageBatchSize Tests

	[Fact]
	public void ReturnDefaultBatchSize()
	{
		// Arrange
		var options = DispatchOutboxOptions.Balanced();

		// Act
		var batchSize = options.MessageBatchSize();

		// Assert
		batchSize.ShouldBe(100);
	}

	[Fact]
	public void ReturnSameBatchSize_ForDifferentOptionsInstances()
	{
		// Arrange - different presets
		var options1 = DispatchOutboxOptions.HighThroughput();
		var options2 = DispatchOutboxOptions.HighReliability();

		// Act
		var batchSize1 = options1.MessageBatchSize();
		var batchSize2 = options2.MessageBatchSize();

		// Assert - currently returns constant default
		batchSize1.ShouldBe(100);
		batchSize2.ShouldBe(100);
	}

	#endregion
}
