// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Cdc;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreCdcOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "CDC")]
public sealed class FirestoreCdcOptionsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		var options = new FirestoreCdcOptions();

		options.CollectionPath.ShouldBe(string.Empty);
		options.ProcessorName.ShouldBe("cdc-processor");
		options.MaxBatchSize.ShouldBe(100);
		options.StartPosition.ShouldBeNull();
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(30));
		options.UseCollectionGroup.ShouldBeFalse();
		options.ChannelCapacity.ShouldBe(1000);
	}

	[Fact]
	public void ValidateAcceptsSimpleCollectionPath()
	{
		var options = new FirestoreCdcOptions
		{
			CollectionPath = "orders"
		};

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ValidateAcceptsNestedCollectionPath()
	{
		var options = new FirestoreCdcOptions
		{
			CollectionPath = "organizations/org1/members"
		};

		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenCollectionPathMissing()
	{
		var options = new FirestoreCdcOptions();

		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionPath");
	}

	[Fact]
	public void ThrowWhenProcessorNameMissing()
	{
		var options = new FirestoreCdcOptions
		{
			CollectionPath = "orders",
			ProcessorName = ""
		};

		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProcessorName");
	}
}
