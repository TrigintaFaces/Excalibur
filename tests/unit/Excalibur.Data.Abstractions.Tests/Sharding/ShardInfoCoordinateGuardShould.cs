// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Sharding;

namespace Excalibur.Data.Abstractions.Tests.Sharding;

/// <summary>
/// Binds the shard-mapping guarantee: a shard that does not declare an isolation coordinate is refused,
/// rather than filled in from the deployment default.
/// </summary>
/// <remarks>
/// <para>
/// A tenant shard map exists to keep one tenant's data apart from another's. When a coordinate is read as
/// <c>shardInfo.X ?? defaultX</c>, two shards that both omit <c>X</c> resolve to the same physical
/// location — same node, same index, same schema — and the tenants mapped to them are commingled with no
/// error raised and nothing written to a log. The resolver's own purpose is defeated by its most
/// forgiving line.
/// </para>
/// <para>
/// Both arms are needed. Refusal alone is satisfied by a guard that rejects everything, which makes
/// sharding unusable; acceptance alone is satisfied by the fallback this replaces. Together they say the
/// guard fires on absence and only on absence.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ShardInfoCoordinateGuardShould
{
	private static ShardInfo Shard(string? schema) =>
		new("shard-a", "Host=db-a", SchemaName: schema);

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void RefuseACoordinateTheShardDoesNotDeclare(string? undeclared)
	{
		var shard = Shard(undeclared);

		var thrown = Should.Throw<InvalidOperationException>(
			() => shard.RequireCoordinate(shard.SchemaName, nameof(ShardInfo.SchemaName)),
			"a shard that does not declare its schema must be refused; filling it from the deployment "
			+ "default puts every incompletely-mapped tenant in the same schema");

		// The message must name the offending shard AND the missing coordinate, or an operator cannot
		// find which entry to fix.
		thrown.Message.ShouldContain("shard-a");
		thrown.Message.ShouldContain(nameof(ShardInfo.SchemaName));
	}

	[Fact]
	public void ReturnACoordinateTheShardDeclares()
	{
		var shard = Shard("tenant_a");

		shard.RequireCoordinate(shard.SchemaName, nameof(ShardInfo.SchemaName)).ShouldBe(
			"tenant_a",
			"a fully specified shard must resolve; a guard that refused these too would make sharding "
			+ "unusable while satisfying every refusal assertion");
	}
}
