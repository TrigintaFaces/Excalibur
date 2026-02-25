// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Migration;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventMigrationResultShould
{
	[Fact]
	public void ExposeAllProperties()
	{
		var errors = new List<string> { "error1" };
		var result = new EventMigrationResult(100, 5, 3, false, errors);

		result.EventsMigrated.ShouldBe(100);
		result.EventsSkipped.ShouldBe(5);
		result.StreamsMigrated.ShouldBe(3);
		result.IsDryRun.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
	}

	[Fact]
	public void SupportDryRunFlag()
	{
		var result = new EventMigrationResult(0, 0, 0, true, []);
		result.IsDryRun.ShouldBeTrue();
	}

	[Fact]
	public void SupportEmptyErrors()
	{
		var result = new EventMigrationResult(50, 0, 1, false, []);
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var errors = Array.Empty<string>().ToList().AsReadOnly();
		var a = new EventMigrationResult(10, 2, 1, false, errors);
		var b = new EventMigrationResult(10, 2, 1, false, errors);
		a.ShouldBe(b);
	}
}
