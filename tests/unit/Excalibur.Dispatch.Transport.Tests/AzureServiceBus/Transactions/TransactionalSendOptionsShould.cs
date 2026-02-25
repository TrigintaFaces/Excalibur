// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Transactions;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TransactionalSendOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new TransactionalSendOptions();

		// Assert
		options.TransactionGroup.ShouldBeNull();
		options.PartitionKey.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new TransactionalSendOptions
		{
			TransactionGroup = "order-batch",
			PartitionKey = "partition-1",
		};

		// Assert
		options.TransactionGroup.ShouldBe("order-batch");
		options.PartitionKey.ShouldBe("partition-1");
	}
}
