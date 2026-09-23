// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Caching;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryCacheTagTracker"/> validating ICacheTagTracker contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryCacheTagTracker maintains a per-tag version stamp for tag-based cache invalidation in
/// scenarios where the underlying cache doesn't natively support tags.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>GetOrCreateStampAsync creates a stamp for a tag never seen before</description></item>
/// <item><description>GetOrCreateStampAsync is stable absent a bump</description></item>
/// <item><description>BumpStampAsync changes a tag's stamp without affecting other tags</description></item>
/// <item><description>NO SYNC methods - all Task-based</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "CACHE")]
public sealed class InMemoryCacheTagTrackerConformanceTests : CacheTagTrackerConformanceTestKit
{

	/// <summary>
	/// Exposes the kit's own wiring check to the runner. The check is an arm like any other, so a
	/// suite that omits THIS member disables it silently -- the one gap it cannot report itself.
	/// </summary>
	/// <returns>A completed task when every arm in the kit is wired.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
		ConformanceSuite_ShouldWireEveryArm();

	/// <inheritdoc />
	protected override ICacheTagTracker CreateTracker()
	{
		return new InMemoryCacheTagTracker();
	}

	#region GetOrCreateStampAsync Tests

	[Fact]
	public Task GetOrCreateStampAsync_NewTag_ShouldCreateStamp_Test() =>
		GetOrCreateStampAsync_NewTag_ShouldCreateStamp();

	[Fact]
	public Task GetOrCreateStampAsync_SameTagNoBump_ShouldReturnSameStamp_Test() =>
		GetOrCreateStampAsync_SameTagNoBump_ShouldReturnSameStamp();

	[Fact]
	public Task GetOrCreateStampAsync_DifferentTags_ShouldReturnDifferentStamps_Test() =>
		GetOrCreateStampAsync_DifferentTags_ShouldReturnDifferentStamps();

	#endregion GetOrCreateStampAsync Tests

	#region BumpStampAsync Tests

	[Fact]
	public Task BumpStampAsync_ShouldChangeStamp_Test() =>
		BumpStampAsync_ShouldChangeStamp();

	[Fact]
	public Task BumpStampAsync_NeverResolvedTag_ShouldBeSafeAndResolvable_Test() =>
		BumpStampAsync_NeverResolvedTag_ShouldBeSafeAndResolvable();

	[Fact]
	public Task BumpStampAsync_ShouldNotAffectOtherTags_Test() =>
		BumpStampAsync_ShouldNotAffectOtherTags();

	#endregion BumpStampAsync Tests

	#region Cross-Instance Sharing Tests

	// InMemoryCacheTagTracker is documented as single-process only (SupportsCrossInstanceSharing stays
	// false), so both arms below record a skip rather than a pass or a failure -- two of its instances
	// never share a backend by design.

	[Fact]
	public Task CrossInstanceBump_ShouldInvalidateEntriesOnAnotherInstance_Test() =>
		CrossInstanceBump_ShouldInvalidateEntriesOnAnotherInstance();

	[Fact]
	public Task CrossInstanceNoBump_ShouldStillHitOnAnotherInstance_Test() =>
		CrossInstanceNoBump_ShouldStillHitOnAnotherInstance();

	#endregion Cross-Instance Sharing Tests
}
