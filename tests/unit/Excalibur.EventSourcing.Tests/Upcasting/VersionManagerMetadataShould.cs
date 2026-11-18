// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing.Snapshots;
using Excalibur.EventSourcing.Upcasting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Excalibur.EventSourcing.Tests.Upcasting;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Concurrency")]
public sealed class VersionManagerMetadataShould
{
    [Fact]
    public void Preserve_event_registration_metadata_and_upgrader_identity()
    {
        var manager = new EventVersionManager(NullLogger<EventVersionManager>.Instance);
        var upgrader = new MutableEventUpgrader();
        manager.RegisterUpgrader(upgrader);
        upgrader.From = 8;
        upgrader.To = 9;
        manager.UpgradeEvent("T", "input", 1, 2).ShouldBe("input-upgraded");
        manager.GetUpgradersForEventType("T").Single().ShouldBeSameAs(upgrader);
        upgrader.FromReads.ShouldBe(1);
        upgrader.ToReads.ShouldBe(1);
        upgrader.TypeReads.ShouldBe(1);
    }

    [Fact]
    public void Preserve_snapshot_registration_metadata_and_upgrader_identity()
    {
        var manager = new SnapshotVersionManager([], NullLogger<SnapshotVersionManager>.Instance);
        var upgrader = new MutableSnapshotUpgrader();
        manager.RegisterUpgrader(upgrader);
        upgrader.From = 8;
        upgrader.To = 9;
        byte[] input = [1];
        manager.UpgradeSnapshot("T", input, 1, 2).ShouldBe(new byte[] { 1, 2 });
        manager.GetUpgradersForAggregateType("T").Single().ShouldBeSameAs(upgrader);
        upgrader.FromReads.ShouldBe(1);
        upgrader.ToReads.ShouldBe(1);
        upgrader.TypeReads.ShouldBe(1);
    }

    private sealed class MutableEventUpgrader : IEventUpgrader
    {
        public int From { get; set; } = 1;
        public int To { get; set; } = 2;
        public int FromReads { get; private set; }
        public int ToReads { get; private set; }
        public int TypeReads { get; private set; }
        public string EventType { get { TypeReads++; return "T"; } }
        public int FromVersion { get { FromReads++; return From; } }
        public int ToVersion { get { ToReads++; return To; } }
        public bool CanUpgrade(string eventType, int fromVersion) => eventType == "T" && fromVersion == From;
        public object Upgrade(object oldEvent) => oldEvent + "-upgraded";
    }

    private sealed class MutableSnapshotUpgrader : ISnapshotUpgrader
    {
        public int From { get; set; } = 1;
        public int To { get; set; } = 2;
        public int FromReads { get; private set; }
        public int ToReads { get; private set; }
        public int TypeReads { get; private set; }
        public string AggregateType { get { TypeReads++; return "T"; } }
        public int FromVersion { get { FromReads++; return From; } }
        public int ToVersion { get { ToReads++; return To; } }
        public bool CanUpgrade(string aggregateType, int fromVersion) => aggregateType == "T" && fromVersion == From;
        public byte[] Upgrade(byte[] oldSnapshotData) => [..oldSnapshotData, 2];
    }
}
