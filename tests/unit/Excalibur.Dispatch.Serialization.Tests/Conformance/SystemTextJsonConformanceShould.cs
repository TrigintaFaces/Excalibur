// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Serialization.Tests.Conformance;

/// <summary>
/// Sprint 623 A.1: Serializer conformance tests for <see cref="SystemTextJsonSerializer"/>.
/// </summary>
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Test project")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test project")]
public sealed class SystemTextJsonConformanceShould : SerializerConformanceTestsBase
{
	protected override ISerializer CreateSerializer() => new SystemTextJsonSerializer();

	protected override object CreateTestObject() => new JsonConformanceDto
	{
		Name = "conformance-test",
		Value = 42,
		IsActive = true,
		Tags = ["tag1", "tag2", "tag3"],
	};

	protected override object CreateEmptyTestObject() => new JsonConformanceDto();

	protected override Type TestObjectType => typeof(JsonConformanceDto);

	protected override void AssertObjectsEqual(object expected, object actual)
	{
		var e = (JsonConformanceDto)expected;
		var a = (JsonConformanceDto)actual;
		a.Name.ShouldBe(e.Name);
		a.Value.ShouldBe(e.Value);
		a.IsActive.ShouldBe(e.IsActive);
		a.Tags.ShouldBe(e.Tags);
	}

	protected override object CreateLargeTestObject()
	{
		// Build a large string payload > 1MB
		var largeString = new string('x', 500_000);
		return new JsonConformanceDto
		{
			Name = largeString,
			Value = int.MaxValue,
			IsActive = true,
			Tags = Enumerable.Range(0, 10_000).Select(i => $"tag-{i}-{largeString[..50]}").ToList(),
		};
	}
}
