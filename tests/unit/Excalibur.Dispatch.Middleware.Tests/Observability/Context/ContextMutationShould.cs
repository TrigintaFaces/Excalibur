// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ContextMutationShould
{
	[Fact]
	public void SetAndGetAllProperties()
	{
		// Arrange & Act
		var sut = new ContextMutation
		{
			Field = "CorrelationId",
			Type = MutationType.Modified,
			OldValue = "old-corr",
			NewValue = "new-corr",
			Stage = "Authorization",
		};

		// Assert
		sut.Field.ShouldBe("CorrelationId");
		sut.Type.ShouldBe(MutationType.Modified);
		sut.OldValue.ShouldBe("old-corr");
		sut.NewValue.ShouldBe("new-corr");
		sut.Stage.ShouldBe("Authorization");
	}

	[Fact]
	public void AllowNullValuesForOldAndNew()
	{
		var sut = new ContextMutation
		{
			Field = "TenantId",
			Type = MutationType.Added,
			OldValue = null,
			NewValue = "tenant-001",
			Stage = "Validation",
		};

		sut.OldValue.ShouldBeNull();
		sut.NewValue.ShouldBe("tenant-001");
	}

	[Fact]
	public void SupportRemovedMutationType()
	{
		var sut = new ContextMutation
		{
			Field = "TempData",
			Type = MutationType.Removed,
			OldValue = "some-data",
			NewValue = null,
			Stage = "Handler",
		};

		sut.Type.ShouldBe(MutationType.Removed);
		sut.NewValue.ShouldBeNull();
	}
}
