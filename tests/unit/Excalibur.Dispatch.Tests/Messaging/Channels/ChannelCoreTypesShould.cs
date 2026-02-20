// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelCoreTypesShould
{
	[Fact]
	public void ChannelMode_HaveExpectedValues()
	{
		// Assert
		ChannelMode.Unbounded.ShouldBe((ChannelMode)0);
		ChannelMode.Bounded.ShouldBe((ChannelMode)1);
	}

	[Fact]
	public void ChannelMode_HaveTwoValues()
	{
		// Act
		var values = Enum.GetValues<ChannelMode>();

		// Assert
		values.Length.ShouldBe(2);
	}

	[Fact]
	public void ChannelMessagePumpStatus_HaveExpectedValues()
	{
		// Assert
		ChannelMessagePumpStatus.NotStarted.ShouldBe((ChannelMessagePumpStatus)0);
		ChannelMessagePumpStatus.Starting.ShouldBe((ChannelMessagePumpStatus)1);
		ChannelMessagePumpStatus.Running.ShouldBe((ChannelMessagePumpStatus)2);
		ChannelMessagePumpStatus.Stopping.ShouldBe((ChannelMessagePumpStatus)3);
		ChannelMessagePumpStatus.Stopped.ShouldBe((ChannelMessagePumpStatus)4);
		ChannelMessagePumpStatus.Faulted.ShouldBe((ChannelMessagePumpStatus)5);
	}

	[Fact]
	public void ChannelMessagePumpStatus_HaveSixValues()
	{
		// Act
		var values = Enum.GetValues<ChannelMessagePumpStatus>();

		// Assert
		values.Length.ShouldBe(6);
	}

	[Fact]
	public void ChannelMessagePumpStatus_DefaultToNotStarted()
	{
		// Arrange
		ChannelMessagePumpStatus status = default;

		// Assert
		status.ShouldBe(ChannelMessagePumpStatus.NotStarted);
	}

	[Fact]
	public void Batch_Constructor_SetItemsAndCount()
	{
		// Arrange
		var items = new List<string> { "a", "b", "c" };

		// Act
		var batch = new Batch<string>(items);

		// Assert
		batch.Items.ShouldBe(items);
		batch.Count.ShouldBe(3);
		batch.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void Batch_Constructor_ThrowOnNullItems()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new Batch<string>(null!));
	}

	[Fact]
	public void Batch_Equality_SameReference_AreEqual()
	{
		// Arrange
		var items = new List<int> { 1, 2, 3 };
		var batch1 = new Batch<int>(items);

		// Act — equality is reference-based for Items
		var batch2 = batch1;

		// Assert
		batch1.ShouldBe(batch2);
		(batch1 == batch2).ShouldBeTrue();
	}

	[Fact]
	public void Batch_Equality_DifferentItems_AreNotEqual()
	{
		// Arrange
		var batch1 = new Batch<int>(new List<int> { 1, 2 });
		var batch2 = new Batch<int>(new List<int> { 1, 2 });

		// Assert — different list references
		(batch1 != batch2).ShouldBeTrue();
	}

	[Fact]
	public void Batch_Equals_WithObjectParam_ReturnFalseForDifferentType()
	{
		// Arrange
		var batch = new Batch<int>(new List<int> { 1 });

		// Act & Assert
		batch.Equals("not a batch").ShouldBeFalse();
	}

	[Fact]
	public void BatchReadResult_Constructor_SetProperties()
	{
		// Arrange
		var items = new List<string> { "x", "y" };

		// Act
		var result = new BatchReadResult<string>(items, hasItems: true);

		// Assert
		result.Items.ShouldBe(items);
		result.HasItems.ShouldBeTrue();
		result.Count.ShouldBe(2);
	}

	[Fact]
	public void BatchReadResult_EmptyResult_HasItemsFalse()
	{
		// Act
		var result = new BatchReadResult<int>([], hasItems: false);

		// Assert
		result.HasItems.ShouldBeFalse();
		result.Count.ShouldBe(0);
	}

	[Fact]
	public void BatchReadResult_Equality_SameReference_AreEqual()
	{
		// Arrange
		var items = new List<int> { 1, 2 };
		var result1 = new BatchReadResult<int>(items, true);
		var result2 = result1;

		// Assert
		result1.ShouldBe(result2);
		(result1 == result2).ShouldBeTrue();
	}

	[Fact]
	public void BatchReadResult_Equality_DifferentItems_AreNotEqual()
	{
		// Arrange
		var result1 = new BatchReadResult<int>(new List<int> { 1 }, true);
		var result2 = new BatchReadResult<int>(new List<int> { 1 }, true);

		// Assert
		(result1 != result2).ShouldBeTrue();
	}

	[Fact]
	public void BatchReadResult_Equals_WithObjectParam_ReturnFalseForDifferentType()
	{
		// Arrange
		var result = new BatchReadResult<int>([], false);

		// Act & Assert
		result.Equals(42).ShouldBeFalse();
	}

	[Fact]
	public void MemoryMessageEventArgs_SetProperties()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		using var cts = new CancellationTokenSource();

		// Act
		var args = new MemoryMessageEventArgs(envelope, cts.Token);

		// Assert
		args.Envelope.ShouldBe(envelope);
		args.CancellationToken.ShouldBe(cts.Token);
	}

	[Fact]
	public void MemoryMessageEventArgs_InheritFromEventArgs()
	{
		// Arrange
		var args = new MemoryMessageEventArgs(new MessageEnvelope(), CancellationToken.None);

		// Assert
		args.ShouldBeAssignableTo<EventArgs>();
	}
}
