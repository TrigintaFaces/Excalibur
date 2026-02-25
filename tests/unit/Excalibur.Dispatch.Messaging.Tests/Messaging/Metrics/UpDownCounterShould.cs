// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Metrics;

using IntUpDownCounter = Excalibur.Dispatch.Metrics.UpDownCounter<int>;
using LongUpDownCounter = Excalibur.Dispatch.Metrics.UpDownCounter<long>;
using DoubleUpDownCounter = Excalibur.Dispatch.Metrics.UpDownCounter<double>;
using FloatUpDownCounter = Excalibur.Dispatch.Metrics.UpDownCounter<float>;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="UpDownCounter{T}"/>.
/// </summary>
/// <remarks>
/// Tests the up-down counter wrapper for .NET Metrics API.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class UpDownCounterShould : IDisposable
{
	private readonly Meter _meter;

	public UpDownCounterShould()
	{
		_meter = new Meter("Excalibur.Dispatch.Tests.UpDownCounter", "1.0.0");
	}

	public void Dispose()
	{
		_meter.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithRequiredParameters_CreatesInstance()
	{
		// Arrange & Act
		var counter = new IntUpDownCounter(_meter, "test_counter");

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithAllParameters_CreatesInstance()
	{
		// Arrange & Act
		var counter = new IntUpDownCounter(_meter, "test_counter", "items", "Test counter description");

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullUnit_CreatesInstance()
	{
		// Arrange & Act
		var counter = new IntUpDownCounter(_meter, "test_counter", null, "Description");

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullDescription_CreatesInstance()
	{
		// Arrange & Act
		var counter = new IntUpDownCounter(_meter, "test_counter", "items", null);

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	#endregion

	#region Add Tests - Int

	[Fact]
	public void Add_WithPositiveInt_Succeeds()
	{
		// Arrange
		var counter = new IntUpDownCounter(_meter, "int_counter");

		// Act - Should not throw
		counter.Add(5);

		// Assert - If we got here, it worked
		true.ShouldBeTrue();
	}

	[Fact]
	public void Add_WithNegativeInt_Succeeds()
	{
		// Arrange
		var counter = new IntUpDownCounter(_meter, "int_counter_neg");

		// Act - Should not throw
		counter.Add(-3);

		// Assert - If we got here, it worked
		true.ShouldBeTrue();
	}

	[Fact]
	public void Add_WithZeroInt_Succeeds()
	{
		// Arrange
		var counter = new IntUpDownCounter(_meter, "int_counter_zero");

		// Act - Should not throw
		counter.Add(0);

		// Assert - If we got here, it worked
		true.ShouldBeTrue();
	}

	#endregion

	#region Add Tests - Long

	[Fact]
	public void Add_WithPositiveLong_Succeeds()
	{
		// Arrange
		var counter = new LongUpDownCounter(_meter, "long_counter");

		// Act - Should not throw
		counter.Add(1000000L);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Add_WithNegativeLong_Succeeds()
	{
		// Arrange
		var counter = new LongUpDownCounter(_meter, "long_counter_neg");

		// Act - Should not throw
		counter.Add(-500000L);

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Add Tests - Double

	[Fact]
	public void Add_WithPositiveDouble_Succeeds()
	{
		// Arrange
		var counter = new DoubleUpDownCounter(_meter, "double_counter");

		// Act - Should not throw
		counter.Add(3.14);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Add_WithNegativeDouble_Succeeds()
	{
		// Arrange
		var counter = new DoubleUpDownCounter(_meter, "double_counter_neg");

		// Act - Should not throw
		counter.Add(-2.71);

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Add Tests - With Tags

	[Fact]
	public void Add_WithTags_Succeeds()
	{
		// Arrange
		var counter = new IntUpDownCounter(_meter, "tagged_counter");
		var tags = new TagList { { "region", "us-east" } };

		// Act - Should not throw
		counter.Add(1, tags);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Add_WithMultipleTags_Succeeds()
	{
		// Arrange
		var counter = new IntUpDownCounter(_meter, "multi_tagged_counter");
		var tags = new TagList
		{
			{ "region", "us-east" },
			{ "service", "messaging" },
			{ "endpoint", "queue1" }
		};

		// Act - Should not throw
		counter.Add(5, tags);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Add_WithDefaultTags_Succeeds()
	{
		// Arrange
		var counter = new IntUpDownCounter(_meter, "default_tagged_counter");

		// Act - Should not throw
		counter.Add(1, default);

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Multiple Adds Tests

	[Fact]
	public void Add_MultipleTimes_Succeeds()
	{
		// Arrange
		var counter = new IntUpDownCounter(_meter, "multi_add_counter");

		// Act - Should not throw
		counter.Add(5);
		counter.Add(-2);
		counter.Add(10);
		counter.Add(-3);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Add_RapidSuccession_Succeeds()
	{
		// Arrange
		var counter = new IntUpDownCounter(_meter, "rapid_counter");

		// Act - Should not throw
		for (var i = 0; i < 100; i++)
		{
			counter.Add(i % 2 == 0 ? 1 : -1);
		}

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Type Constraint Tests

	[Fact]
	public void SupportsIntType()
	{
		// Arrange & Act
		var counter = new IntUpDownCounter(_meter, "int_type");
		counter.Add(1);

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	[Fact]
	public void SupportsLongType()
	{
		// Arrange & Act
		var counter = new LongUpDownCounter(_meter, "long_type");
		counter.Add(1L);

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	[Fact]
	public void SupportsDoubleType()
	{
		// Arrange & Act
		var counter = new DoubleUpDownCounter(_meter, "double_type");
		counter.Add(1.0);

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	[Fact]
	public void SupportsFloatType()
	{
		// Arrange & Act
		var counter = new FloatUpDownCounter(_meter, "float_type");
		counter.Add(1.0f);

		// Assert
		_ = counter.ShouldNotBeNull();
	}

	#endregion
}
