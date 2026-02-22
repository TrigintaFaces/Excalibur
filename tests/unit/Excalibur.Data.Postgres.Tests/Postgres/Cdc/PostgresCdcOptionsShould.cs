// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Cdc;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresCdcOptionsShould
{
	[Fact]
	public void HaveDefaultConnectionString()
	{
		var options = new PostgresCdcOptions();

		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultPublicationName()
	{
		var options = new PostgresCdcOptions();

		options.PublicationName.ShouldBe("excalibur_cdc_publication");
	}

	[Fact]
	public void HaveDefaultReplicationSlotName()
	{
		var options = new PostgresCdcOptions();

		options.ReplicationSlotName.ShouldBe("excalibur_cdc_slot");
	}

	[Fact]
	public void HaveEmptyTableNamesByDefault()
	{
		var options = new PostgresCdcOptions();

		options.TableNames.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultProcessorId()
	{
		var options = new PostgresCdcOptions();

		options.ProcessorId.ShouldBe(Environment.MachineName);
	}

	[Fact]
	public void HaveDefaultPollingInterval()
	{
		var options = new PostgresCdcOptions();

		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultBatchSize()
	{
		var options = new PostgresCdcOptions();

		options.BatchSize.ShouldBe(1000);
	}

	[Fact]
	public void HaveDefaultTimeout()
	{
		var options = new PostgresCdcOptions();

		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveAutoCreateSlotEnabledByDefault()
	{
		var options = new PostgresCdcOptions();

		options.AutoCreateSlot.ShouldBeTrue();
	}

	[Fact]
	public void HaveUseBinaryProtocolDisabledByDefault()
	{
		var options = new PostgresCdcOptions();

		options.UseBinaryProtocol.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullRecoveryOptionsByDefault()
	{
		var options = new PostgresCdcOptions();

		options.RecoveryOptions.ShouldBeNull();
	}

	[Fact]
	public void ValidateSuccessfullyWithRequiredValues()
	{
		var options = new PostgresCdcOptions
		{
			ConnectionString = "Host=localhost;Database=test"
		};

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmpty()
	{
		var options = new PostgresCdcOptions();

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenPublicationNameIsEmpty()
	{
		var options = new PostgresCdcOptions
		{
			ConnectionString = "Host=localhost",
			PublicationName = ""
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenReplicationSlotNameIsEmpty()
	{
		var options = new PostgresCdcOptions
		{
			ConnectionString = "Host=localhost",
			ReplicationSlotName = ""
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenBatchSizeIsZero()
	{
		var options = new PostgresCdcOptions
		{
			ConnectionString = "Host=localhost",
			BatchSize = 0
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ValidateRecoveryOptionsWhenPresent()
	{
		var options = new PostgresCdcOptions
		{
			ConnectionString = "Host=localhost",
			RecoveryOptions = new PostgresCdcRecoveryOptions()
		};

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ValidateAcceptsSchemaQualifiedAndUnqualifiedTableNames()
	{
		var options = new PostgresCdcOptions
		{
			ConnectionString = "Host=localhost;Database=test",
			TableNames = ["public.orders", "orders"]
		};

		Should.NotThrow(() => options.Validate());
	}
}
