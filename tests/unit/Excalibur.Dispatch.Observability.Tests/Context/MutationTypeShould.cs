// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="MutationType"/> internal enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class MutationTypeShould
{
	[Fact]
	public void HaveCorrectIntegerValues()
	{
		// Assert
		((int)MutationType.Added).ShouldBe(0);
		((int)MutationType.Removed).ShouldBe(1);
		((int)MutationType.Modified).ShouldBe(2);
	}

	[Fact]
	public void HaveThreeValues()
	{
		// Assert
		Enum.GetValues<MutationType>().ShouldBe([
			MutationType.Added,
			MutationType.Removed,
			MutationType.Modified,
		]);
	}

	[Fact]
	public void ParseFromString()
	{
		// Act & Assert
		Enum.Parse<MutationType>("Added").ShouldBe(MutationType.Added);
		Enum.Parse<MutationType>("Removed").ShouldBe(MutationType.Removed);
		Enum.Parse<MutationType>("Modified").ShouldBe(MutationType.Modified);
	}

	[Fact]
	public void ConvertToString()
	{
		// Act & Assert
		MutationType.Added.ToString().ShouldBe("Added");
		MutationType.Removed.ToString().ShouldBe("Removed");
		MutationType.Modified.ToString().ShouldBe("Modified");
	}

	[Fact]
	public void DefaultToAdded()
	{
		// Arrange
		MutationType defaultValue = default;

		// Assert
		defaultValue.ShouldBe(MutationType.Added);
	}

	[Fact]
	public void BeInternal()
	{
		// Assert - MutationType should be internal
		typeof(MutationType).IsNotPublic.ShouldBeTrue();
	}
}
