using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CompositeMessageUpgradeStrategyShould
{
	[Fact]
	public void ReturnTrue_WhenAnyStrategyCanUpgrade()
	{
		var strategies = new IMessageUpgradeStrategy[]
		{
			new TestUpgradeStrategy(canUpgrade: false, result: "first"),
			new TestUpgradeStrategy(canUpgrade: true, result: "second")
		};
		var sut = new CompositeMessageUpgradeStrategy(strategies);

		var canUpgrade = sut.CanUpgrade(typeof(string), "v1");

		canUpgrade.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_WhenNoStrategyCanUpgrade()
	{
		var strategies = new IMessageUpgradeStrategy[]
		{
			new TestUpgradeStrategy(canUpgrade: false, result: "first"),
			new TestUpgradeStrategy(canUpgrade: false, result: "second")
		};
		var sut = new CompositeMessageUpgradeStrategy(strategies);

		var canUpgrade = sut.CanUpgrade(typeof(string), "v1");

		canUpgrade.ShouldBeFalse();
	}

	[Fact]
	public void UseFirstMatchingStrategy_WhenUpgrading()
	{
		var first = new TestUpgradeStrategy(canUpgrade: true, result: "first");
		var second = new TestUpgradeStrategy(canUpgrade: true, result: "second");
		var sut = new CompositeMessageUpgradeStrategy([first, second]);

		var upgraded = sut.Upgrade("payload", typeof(string), "v1", "v2");

		upgraded.ShouldBe("first");
		first.UpgradeCallCount.ShouldBe(1);
		second.UpgradeCallCount.ShouldBe(0);
	}

	[Fact]
	public void ThrowNotSupported_WhenNoStrategyMatches()
	{
		var sut = new CompositeMessageUpgradeStrategy(
			[new TestUpgradeStrategy(canUpgrade: false, result: "unused")]);

		_ = Should.Throw<NotSupportedException>(
			() => sut.Upgrade("payload", typeof(string), "v1", "v2"));
	}

	private sealed class TestUpgradeStrategy(bool canUpgrade, string result) : IMessageUpgradeStrategy
	{
		public int UpgradeCallCount { get; private set; }

		public bool CanUpgrade(Type messageType, string version) => canUpgrade;

		public object Upgrade(string payload, Type messageType, string fromVersion, string toVersion)
		{
			UpgradeCallCount++;
			return result;
		}
	}
}
