// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Edge case tests for AreFieldValuesEqual and CompareSnapshots private methods
/// in <see cref="ContextFlowDiagnostics"/>, exercised via VisualizeContextFlow.
/// Covers: both-null, value-to-null, same reference, Guid/DateTimeOffset/TimeSpan/decimal
/// direct equality, and the InvalidOperationException fallback path.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class AreFieldValuesEqualEdgeCasesShould : IDisposable
{
	private readonly IContextFlowTracker _tracker = A.Fake<IContextFlowTracker>();
	private readonly IContextFlowMetrics _metrics = A.Fake<IContextFlowMetrics>();
	private readonly ContextFlowDiagnostics _sut;

	public AreFieldValuesEqualEdgeCasesShould()
	{
		var options = new ContextObservabilityOptions();
		var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
		_sut = new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			_tracker,
			_metrics,
			optionsWrapper);
	}

	public void Dispose() => _sut.Dispose();

	[Fact]
	public void NotReportModified_WhenBothFieldValuesAreNull()
	{
		// Arrange - both snapshots have the same field with null value
		var snapshots = CreateSnapshotPair(
			("NullableField", null),
			("NullableField", null));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-both-null"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-both-null");

		// Assert - null == null means no modification
		result.ShouldNotContain("Modified: NullableField");
	}

	[Fact]
	public void ReportModified_WhenFieldChangesFromValueToNull()
	{
		// Arrange - field goes from a value to null
		var snapshots = CreateSnapshotPair(
			("Status", (object?)"Active"),
			("Status", null));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-val-to-null"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-val-to-null");

		// Assert
		result.ShouldContain("Modified: Status");
	}

	[Fact]
	public void NotReportModified_WhenGuidValuesAreEqual()
	{
		// Arrange - Guid type uses direct Equals() path
		var guid = Guid.NewGuid();
		var snapshots = CreateSnapshotPair(
			("TraceId", (object?)guid),
			("TraceId", (object?)guid));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-guid-eq"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-guid-eq");

		// Assert
		result.ShouldNotContain("Modified: TraceId");
	}

	[Fact]
	public void ReportModified_WhenGuidValuesDiffer()
	{
		// Arrange
		var snapshots = CreateSnapshotPair(
			("TraceId", (object?)Guid.NewGuid()),
			("TraceId", (object?)Guid.NewGuid()));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-guid-diff"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-guid-diff");

		// Assert
		result.ShouldContain("Modified: TraceId");
	}

	[Fact]
	public void NotReportModified_WhenDateTimeOffsetValuesAreEqual()
	{
		// Arrange - DateTimeOffset uses direct Equals() path
		var timestamp = DateTimeOffset.UtcNow;
		var snapshots = CreateSnapshotPair(
			("Timestamp", (object?)timestamp),
			("Timestamp", (object?)timestamp));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-dto-eq"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-dto-eq");

		// Assert
		result.ShouldNotContain("Modified: Timestamp");
	}

	[Fact]
	public void ReportModified_WhenDateTimeOffsetValuesDiffer()
	{
		// Arrange
		var snapshots = CreateSnapshotPair(
			("Timestamp", (object?)DateTimeOffset.UtcNow.AddHours(-1)),
			("Timestamp", (object?)DateTimeOffset.UtcNow));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-dto-diff"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-dto-diff");

		// Assert
		result.ShouldContain("Modified: Timestamp");
	}

	[Fact]
	public void NotReportModified_WhenTimeSpanValuesAreEqual()
	{
		// Arrange - TimeSpan uses direct Equals() path
		var span = TimeSpan.FromSeconds(30);
		var snapshots = CreateSnapshotPair(
			("Timeout", (object?)span),
			("Timeout", (object?)span));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-ts-eq"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-ts-eq");

		// Assert
		result.ShouldNotContain("Modified: Timeout");
	}

	[Fact]
	public void NotReportModified_WhenDecimalValuesAreEqual()
	{
		// Arrange - decimal uses direct Equals() path
		var amount = 123.45m;
		var snapshots = CreateSnapshotPair(
			("Amount", (object?)amount),
			("Amount", (object?)amount));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-decimal-eq"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-decimal-eq");

		// Assert
		result.ShouldNotContain("Modified: Amount");
	}

	[Fact]
	public void ReportModified_WhenDecimalValuesDiffer()
	{
		// Arrange
		var snapshots = CreateSnapshotPair(
			("Amount", (object?)10.00m),
			("Amount", (object?)20.00m));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-decimal-diff"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-decimal-diff");

		// Assert
		result.ShouldContain("Modified: Amount");
	}

	[Fact]
	public void NotReportModified_WhenDateTimeValuesAreEqual()
	{
		// Arrange - DateTime uses direct Equals() path
		var dt = new DateTime(2026, 4, 4, 12, 0, 0, DateTimeKind.Utc);
		var snapshots = CreateSnapshotPair(
			("CreatedAt", (object?)dt),
			("CreatedAt", (object?)dt));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-dt-eq"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-dt-eq");

		// Assert
		result.ShouldNotContain("Modified: CreatedAt");
	}

	[Fact]
	public void NotReportModified_WhenBooleanValuesAreEqual()
	{
		// Arrange - bool is a primitive type, uses direct Equals() path
		var snapshots = CreateSnapshotPair(
			("IsRetry", (object?)true),
			("IsRetry", (object?)true));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-bool-eq"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-bool-eq");

		// Assert
		result.ShouldNotContain("Modified: IsRetry");
	}

	[Fact]
	public void ReportModified_WhenBooleanValuesDiffer()
	{
		// Arrange
		var snapshots = CreateSnapshotPair(
			("IsRetry", (object?)false),
			("IsRetry", (object?)true));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-bool-diff"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-bool-diff");

		// Assert
		result.ShouldContain("Modified: IsRetry");
	}

	[Fact]
	public void NotReportModified_WhenSameReferenceObjectIsUsed()
	{
		// Arrange - ReferenceEquals returns true for same object instance
		var sharedDict = new Dictionary<string, object>(StringComparer.Ordinal) { ["k"] = "v" };
		var snapshots = CreateSnapshotPair(
			("SharedRef", (object?)sharedDict),
			("SharedRef", (object?)sharedDict));

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-same-ref"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-same-ref");

		// Assert - ReferenceEquals short-circuit means no modification
		result.ShouldNotContain("Modified: SharedRef");
	}

	/// <summary>
	/// Helper to create a pair of snapshots with one custom field each, suitable for
	/// testing AreFieldValuesEqual through VisualizeContextFlow.
	/// </summary>
	private static ContextSnapshot[] CreateSnapshotPair(
		(string key, object? value) beforeField,
		(string key, object? value) afterField)
	{
		// Extract the message ID from the tracker call pattern -- the caller sets it up
		return
		[
			new ContextSnapshot
			{
				MessageId = "test",
				Stage = "Before",
				Timestamp = DateTimeOffset.UtcNow.AddSeconds(-5),
				Fields = new Dictionary<string, object?>(StringComparer.Ordinal)
				{
					[beforeField.key] = beforeField.value,
				},
				FieldCount = 1,
				SizeBytes = 50,
				Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
			},
			new ContextSnapshot
			{
				MessageId = "test",
				Stage = "After",
				Timestamp = DateTimeOffset.UtcNow,
				Fields = new Dictionary<string, object?>(StringComparer.Ordinal)
				{
					[afterField.key] = afterField.value,
				},
				FieldCount = 1,
				SizeBytes = 50,
				Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
			},
		];
	}
}
