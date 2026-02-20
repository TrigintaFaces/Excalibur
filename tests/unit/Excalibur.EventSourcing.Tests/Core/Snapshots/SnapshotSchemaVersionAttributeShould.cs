// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Snapshots.Versioning;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SnapshotSchemaVersionAttributeShould
{
	[Fact]
	public void StoreVersion()
	{
		var attr = new SnapshotSchemaVersionAttribute(5);
		attr.Version.ShouldBe(5);
	}

	[Fact]
	public void ThrowWhenVersionIsZero()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new SnapshotSchemaVersionAttribute(0));
	}

	[Fact]
	public void ThrowWhenVersionIsNegative()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new SnapshotSchemaVersionAttribute(-1));
	}

	[Fact]
	public void AcceptVersionOne()
	{
		var attr = new SnapshotSchemaVersionAttribute(1);
		attr.Version.ShouldBe(1);
	}

	[Fact]
	public void BeApplicableToClasses()
	{
		var usage = typeof(SnapshotSchemaVersionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		usage.ValidOn.ShouldBe(AttributeTargets.Class | AttributeTargets.Struct);
		usage.AllowMultiple.ShouldBeFalse();
		usage.Inherited.ShouldBeFalse();
	}
}
