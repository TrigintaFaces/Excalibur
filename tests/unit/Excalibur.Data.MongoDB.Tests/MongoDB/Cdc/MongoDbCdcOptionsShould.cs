// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbCdcOptionsShould
{
	[Fact]
	public void HaveDefaultConnectionString()
	{
		var options = new MongoDbCdcOptions();

		options.Connection.ConnectionString.ShouldBe("mongodb://localhost:27017");
	}

	[Fact]
	public void HaveNullDatabaseNameByDefault()
	{
		var options = new MongoDbCdcOptions();

		options.DatabaseName.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyCollectionNamesByDefault()
	{
		var options = new MongoDbCdcOptions();

		options.CollectionNames.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultProcessorId()
	{
		var options = new MongoDbCdcOptions();

		options.ProcessorId.ShouldBe("default");
	}

	[Fact]
	public void HaveDefaultBatchSize()
	{
		var options = new MongoDbCdcOptions();

		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultMaxAwaitTime()
	{
		var options = new MongoDbCdcOptions();

		options.ChangeStream.MaxAwaitTime.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveDefaultReconnectInterval()
	{
		var options = new MongoDbCdcOptions();

		options.ReconnectInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveFullDocumentEnabledByDefault()
	{
		var options = new MongoDbCdcOptions();

		options.ChangeStream.FullDocument.ShouldBeTrue();
	}

	[Fact]
	public void HaveFullDocumentBeforeChangeDisabledByDefault()
	{
		var options = new MongoDbCdcOptions();

		options.ChangeStream.FullDocumentBeforeChange.ShouldBeFalse();
	}

	[Fact]
	public void HaveEmptyOperationTypesByDefault()
	{
		var options = new MongoDbCdcOptions();

		options.ChangeStream.OperationTypes.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultServerSelectionTimeout()
	{
		var options = new MongoDbCdcOptions();

		options.Connection.ServerSelectionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultConnectTimeout()
	{
		var options = new MongoDbCdcOptions();

		options.Connection.ConnectTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveUseSslDisabledByDefault()
	{
		var options = new MongoDbCdcOptions();

		options.Connection.UseSsl.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultMaxPoolSize()
	{
		var options = new MongoDbCdcOptions();

		options.Connection.MaxPoolSize.ShouldBe(100);
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		var options = new MongoDbCdcOptions();

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmpty()
	{
		var options = new MongoDbCdcOptions();
		options.Connection.ConnectionString = "";

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenProcessorIdIsEmpty()
	{
		var options = new MongoDbCdcOptions { ProcessorId = "" };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenBatchSizeIsZero()
	{
		var options = new MongoDbCdcOptions { BatchSize = 0 };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenMaxAwaitTimeIsZero()
	{
		var options = new MongoDbCdcOptions();
		options.ChangeStream.MaxAwaitTime = TimeSpan.Zero;

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ValidateAcceptsCollectionNamesWithAndWithoutDatabasePrefix()
	{
		var options = new MongoDbCdcOptions
		{
			CollectionNames = ["orders", "sales.orders"]
		};

		Should.NotThrow(() => options.Validate());
	}
}
