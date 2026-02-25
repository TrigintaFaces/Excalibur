// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataTaskRecordTypeAttribute"/>.
/// </summary>
[UnitTest]
public sealed class DataTaskRecordTypeAttributeShould : UnitTestBase
{
	[Fact]
	public void Store_RecordTypeName()
	{
		// Arrange & Act
		var attr = new DataTaskRecordTypeAttribute("OrderRecord");

		// Assert
		attr.RecordTypeName.ShouldBe("OrderRecord");
	}

	[Fact]
	public void Deconstruct_RecordTypeName()
	{
		// Arrange
		var attr = new DataTaskRecordTypeAttribute("InvoiceRecord");

		// Act
		attr.Deconstruct(out var name);

		// Assert
		name.ShouldBe("InvoiceRecord");
	}

	[Fact]
	public void Throw_WhenRecordTypeName_IsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new DataTaskRecordTypeAttribute(null!));
	}

	[Fact]
	public void Throw_WhenRecordTypeName_IsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new DataTaskRecordTypeAttribute(string.Empty));
	}

	[Fact]
	public void HaveCorrectAttributeUsage()
	{
		// Arrange
		var usage = typeof(DataTaskRecordTypeAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Class);
		usage.Inherited.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRecordTypeName_ViaInit()
	{
		// Arrange & Act
		var attr = new DataTaskRecordTypeAttribute("Original") { RecordTypeName = "Updated" };

		// Assert
		attr.RecordTypeName.ShouldBe("Updated");
	}
}
