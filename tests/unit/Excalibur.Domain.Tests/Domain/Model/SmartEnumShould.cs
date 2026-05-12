#pragma warning disable IL2026 // RequiresUnreferencedCode — test-only, not AOT published

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Data.Tests.Domain.Model;

/// <summary>
/// Tests for <see cref="SmartEnum{T}"/> DDD building block.
/// Covers: GetAll, FromId, TryFromId, FromName, TryFromName,
/// Equals/GetHashCode, ToString, and constructor guards.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class SmartEnumShould
{
	// ========================================
	// Test fixture
	// ========================================

	private sealed class TestStatus : SmartEnum<TestStatus>
	{
		public static readonly TestStatus Active = new(1, nameof(Active));
		public static readonly TestStatus Inactive = new(2, nameof(Inactive));
		public static readonly TestStatus Pending = new(3, nameof(Pending));

		private TestStatus(int id, string name) : base(id, name) { }
	}

	// ========================================
	// GetAll
	// ========================================

	[Fact]
	public void ReturnAllDefinedValues_FromGetAll()
	{
		// Act
		var all = TestStatus.GetAll();

		// Assert
		all.Count.ShouldBe(3);
		all.ShouldContain(TestStatus.Active);
		all.ShouldContain(TestStatus.Inactive);
		all.ShouldContain(TestStatus.Pending);
	}

	// ========================================
	// FromId — happy path
	// ========================================

	[Theory]
	[InlineData(1, "Active")]
	[InlineData(2, "Inactive")]
	[InlineData(3, "Pending")]
	public void ReturnCorrectValue_FromId(int id, string expectedName)
	{
		// Act
		var result = TestStatus.FromId(id);

		// Assert
		result.Id.ShouldBe(id);
		result.Name.ShouldBe(expectedName);
	}

	// ========================================
	// FromId — failure path
	// ========================================

	[Fact]
	public void ThrowInvalidOperationException_FromId_WhenIdNotFound()
	{
		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => TestStatus.FromId(999));
		ex.Message.ShouldContain("999");
		ex.Message.ShouldContain("Valid IDs:");
	}

	// ========================================
	// TryFromId
	// ========================================

	[Fact]
	public void ReturnTrue_TryFromId_WhenIdExists()
	{
		// Act
		var found = TestStatus.TryFromId(1, out var result);

		// Assert
		found.ShouldBeTrue();
		result.ShouldBe(TestStatus.Active);
	}

	[Fact]
	public void ReturnFalse_TryFromId_WhenIdNotFound()
	{
		// Act
		var found = TestStatus.TryFromId(999, out var result);

		// Assert
		found.ShouldBeFalse();
		result.ShouldBeNull();
	}

	// ========================================
	// FromName — happy path + case-insensitive
	// ========================================

	[Theory]
	[InlineData("Active", 1)]
	[InlineData("active", 1)]
	[InlineData("ACTIVE", 1)]
	[InlineData("Inactive", 2)]
	[InlineData("inactive", 2)]
	public void ReturnCorrectValue_FromName_CaseInsensitive(string name, int expectedId)
	{
		// Act
		var result = TestStatus.FromName(name);

		// Assert
		result.Id.ShouldBe(expectedId);
	}

	// ========================================
	// FromName — failure paths
	// ========================================

	[Fact]
	public void ThrowInvalidOperationException_FromName_WhenNameNotFound()
	{
		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => TestStatus.FromName("NonExistent"));
		ex.Message.ShouldContain("NonExistent");
		ex.Message.ShouldContain("Valid names:");
	}

	[Fact]
	public void ThrowArgumentNullException_FromName_WhenNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => TestStatus.FromName(null!));
	}

	// ========================================
	// TryFromName
	// ========================================

	[Fact]
	public void ReturnTrue_TryFromName_WhenNameExists()
	{
		// Act
		var found = TestStatus.TryFromName("Pending", out var result);

		// Assert
		found.ShouldBeTrue();
		result.ShouldBe(TestStatus.Pending);
	}

	[Fact]
	public void ReturnTrue_TryFromName_CaseInsensitive()
	{
		// Act
		var found = TestStatus.TryFromName("pending", out var result);

		// Assert
		found.ShouldBeTrue();
		result.ShouldBe(TestStatus.Pending);
	}

	[Fact]
	public void ReturnFalse_TryFromName_WhenNameNotFound()
	{
		// Act
		var found = TestStatus.TryFromName("Unknown", out var result);

		// Assert
		found.ShouldBeFalse();
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnFalse_TryFromName_WhenNull()
	{
		// Act
		var found = TestStatus.TryFromName(null!, out var result);

		// Assert
		found.ShouldBeFalse();
		result.ShouldBeNull();
	}

	// ========================================
	// Equals / GetHashCode
	// ========================================

	[Fact]
	public void BeEqual_WhenSameInstance()
	{
		// Assert
		TestStatus.Active.Equals(TestStatus.Active).ShouldBeTrue();
	}

	[Fact]
	public void BeEqual_WhenSameId()
	{
		// FromId returns the same singleton instance
		var a = TestStatus.FromId(1);
		var b = TestStatus.FromId(1);

		// Assert
		a.Equals(b).ShouldBeTrue();
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	[Fact]
	public void NotBeEqual_WhenDifferentId()
	{
		// Assert
		TestStatus.Active.Equals(TestStatus.Inactive).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqual_WhenComparedToNull()
	{
		// Assert
		TestStatus.Active.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqual_WhenComparedToDifferentType()
	{
		// Assert
		TestStatus.Active.Equals("Active").ShouldBeFalse();
	}

	// ========================================
	// ToString
	// ========================================

	[Fact]
	public void ReturnName_FromToString()
	{
		// Assert
		TestStatus.Active.ToString().ShouldBe("Active");
		TestStatus.Inactive.ToString().ShouldBe("Inactive");
		TestStatus.Pending.ToString().ShouldBe("Pending");
	}

	// ========================================
	// Properties
	// ========================================

	[Fact]
	public void ExposeIdAndName()
	{
		// Assert
		TestStatus.Active.Id.ShouldBe(1);
		TestStatus.Active.Name.ShouldBe("Active");
	}
}
