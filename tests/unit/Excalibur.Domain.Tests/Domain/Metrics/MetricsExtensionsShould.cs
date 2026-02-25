// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Metrics;

namespace Excalibur.Tests.Domain.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricsExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MetricsExtensionsShould
{
	#region IncrementCounter Tests

	[Fact]
	public void IncrementCounter_ThrowsArgumentNullException_WhenMetricsIsNull()
	{
		// Arrange
		IMetrics metrics = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			metrics.IncrementCounter("test_counter"));
	}

	[Fact]
	public void IncrementCounter_CallsRecordCounterWithDefaultValue()
	{
		// Arrange
		var metrics = A.Fake<IMetrics>();

		// Act
		metrics.IncrementCounter("test_counter");

		// Assert
		A.CallTo(() => metrics.RecordCounter("test_counter", 1, A<KeyValuePair<string, object>[]>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void IncrementCounter_CallsRecordCounterWithSpecifiedValue()
	{
		// Arrange
		var metrics = A.Fake<IMetrics>();

		// Act
		metrics.IncrementCounter("test_counter", 5);

		// Assert
		A.CallTo(() => metrics.RecordCounter("test_counter", 5, A<KeyValuePair<string, object>[]>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void IncrementCounter_PassesTags_WhenProvided()
	{
		// Arrange
		var metrics = A.Fake<IMetrics>();
		var tags = new[]
		{
			new KeyValuePair<string, object>("env", "test"),
			new KeyValuePair<string, object>("service", "myapp")
		};

		// Act
		metrics.IncrementCounter("test_counter", 1, tags);

		// Assert
		A.CallTo(() => metrics.RecordCounter("test_counter", 1, A<KeyValuePair<string, object>[]>.That.Matches(t =>
				t.Length == 2 &&
				t[0].Key == "env" &&
				t[1].Key == "service")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void IncrementCounter_PassesEmptyTags_WhenNoTagsProvided()
	{
		// Arrange
		var metrics = A.Fake<IMetrics>();

		// Act
		metrics.IncrementCounter("test_counter", 10);

		// Assert
		A.CallTo(() => metrics.RecordCounter("test_counter", 10, A<KeyValuePair<string, object>[]>.That.IsEmpty()))
			.MustHaveHappenedOnceExactly();
	}

	#endregion IncrementCounter Tests
}
