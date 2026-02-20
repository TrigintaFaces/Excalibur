// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.Postgres.Cdc;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresStalePositionExceptionShould
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		var ex = new PostgresStalePositionException();

		ex.Message.ShouldContain("stale WAL position");
		ex.InnerException.ShouldBeNull();
		ex.EventArgs.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessage()
	{
		var ex = new PostgresStalePositionException("test message");

		ex.Message.ShouldBe("test message");
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new PostgresStalePositionException("test", inner);

		ex.Message.ShouldBe("test");
		ex.InnerException.ShouldBeSameAs(inner);
	}

	[Fact]
	public void CreateWithEventArgs()
	{
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "proc-1",
			ReasonCode = "WAL_POSITION_STALE",
			DetectedAt = DateTimeOffset.UtcNow
		};

		var ex = new PostgresStalePositionException(eventArgs);

		ex.EventArgs.ShouldBeSameAs(eventArgs);
		ex.ProcessorId.ShouldBe("proc-1");
		ex.ReasonCode.ShouldBe("WAL_POSITION_STALE");
		ex.Message.ShouldContain("proc-1");
		ex.Message.ShouldContain("WAL_POSITION_STALE");
	}

	[Fact]
	public void CreateWithMessageAndEventArgs()
	{
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "proc-2",
			ReasonCode = "REPLICATION_SLOT_INVALID",
			CaptureInstance = "my_slot"
		};

		var ex = new PostgresStalePositionException("custom message", eventArgs);

		ex.Message.ShouldBe("custom message");
		ex.EventArgs.ShouldBeSameAs(eventArgs);
		ex.ReplicationSlotName.ShouldBe("my_slot");
	}

	[Fact]
	public void ReturnNullPropertiesWhenNoEventArgs()
	{
		var ex = new PostgresStalePositionException();

		ex.ProcessorId.ShouldBeNull();
		ex.ReasonCode.ShouldBeNull();
		ex.StalePosition.ShouldBeNull();
		ex.ReplicationSlotName.ShouldBeNull();
	}

	[Fact]
	public void ExtractStalePositionFromEventArgs()
	{
		var lsnValue = 12345UL;
		var staleBytes = BitConverter.GetBytes(lsnValue);

		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "proc-3",
			StalePosition = staleBytes,
			ReasonCode = "WAL_POSITION_STALE"
		};

		var ex = new PostgresStalePositionException(eventArgs);

		ex.StalePosition.ShouldNotBeNull();
		ex.StalePosition!.Value.LsnValue.ShouldBe(lsnValue);
	}

	[Fact]
	public void ReturnNullStalePositionForShortBytes()
	{
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "proc-4",
			StalePosition = new byte[] { 1, 2, 3 }, // Less than 8 bytes
			ReasonCode = "WAL_POSITION_STALE"
		};

		var ex = new PostgresStalePositionException(eventArgs);

		ex.StalePosition.ShouldBeNull();
	}

	[Fact]
	public void ExtractReplicationSlotNameFromAdditionalContext()
	{
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "proc-5",
			ReasonCode = "REPLICATION_SLOT_INVALID",
			AdditionalContext = new Dictionary<string, object>
			{
				["ReplicationSlotName"] = "excalibur_cdc_slot"
			}
		};

		var ex = new PostgresStalePositionException(eventArgs);

		ex.ReplicationSlotName.ShouldBe("excalibur_cdc_slot");
	}

	[Fact]
	public void FallBackToCaptureInstanceForReplicationSlotName()
	{
		var eventArgs = new CdcPositionResetEventArgs
		{
			ProcessorId = "proc-6",
			ReasonCode = "REPLICATION_SLOT_INVALID",
			CaptureInstance = "fallback_slot"
		};

		var ex = new PostgresStalePositionException(eventArgs);

		ex.ReplicationSlotName.ShouldBe("fallback_slot");
	}

	[Fact]
	public void FormatMessageWithNullEventArgs()
	{
		var ex = new PostgresStalePositionException((CdcPositionResetEventArgs)null!);

		ex.Message.ShouldContain("stale WAL position");
	}
}
