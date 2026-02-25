// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PersistenceOptionsMonitorShould : IDisposable
{
	private readonly IOptionsMonitor<ProviderConfiguration> _innerMonitor;
	private readonly PersistenceOptionsMonitor<ProviderConfiguration> _monitor;

	private static ProviderConfiguration CreateConfig(string name = "default") =>
		new()
		{
			Name = name,
			Type = PersistenceProviderType.InMemory,
			ConnectionString = "Mode=InMemory"
		};

	public PersistenceOptionsMonitorShould()
	{
		_innerMonitor = A.Fake<IOptionsMonitor<ProviderConfiguration>>();
		var config = A.Fake<IPersistenceConfiguration>();
		A.CallTo(() => _innerMonitor.CurrentValue).Returns(CreateConfig());
		A.CallTo(() => _innerMonitor.Get(A<string?>._)).Returns(CreateConfig());
		_monitor = new PersistenceOptionsMonitor<ProviderConfiguration>(config, _innerMonitor);
	}

	[Fact]
	public void CurrentValue_DelegatesToInnerMonitor()
	{
		var expected = CreateConfig("test");
		A.CallTo(() => _innerMonitor.CurrentValue).Returns(expected);

		_monitor.CurrentValue.ShouldBeSameAs(expected);
	}

	[Fact]
	public void Get_DelegatesToInnerMonitor()
	{
		var expected = CreateConfig("named");
		A.CallTo(() => _innerMonitor.Get("named")).Returns(expected);

		var result = _monitor.Get("named");
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public void GetProviderOptions_ThrowsForNullName()
	{
		Should.Throw<ArgumentException>(() => _monitor.GetProviderOptions(null!));
	}

	[Fact]
	public void GetProviderOptions_DelegatesToGet()
	{
		var expected = CreateConfig("provider1");
		A.CallTo(() => _innerMonitor.Get("provider1")).Returns(expected);

		var result = _monitor.GetProviderOptions("provider1");
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public void OnChange_ThrowsForNullListener()
	{
		Should.Throw<ArgumentNullException>(
			() => _monitor.OnChange(null!));
	}

	[Fact]
	public void OnChange_ReturnsDisposable()
	{
		var subscription = A.Fake<IDisposable>();
		A.CallTo(() => _innerMonitor.OnChange(A<Action<ProviderConfiguration, string?>>._))
			.Returns(subscription);

		var result = _monitor.OnChange((_, _) => { });
		result.ShouldNotBeNull();
		result.Dispose(); // Should not throw
	}

	[Fact]
	public void OnProviderChange_ThrowsForNullProviderName()
	{
		Should.Throw<ArgumentException>(
			() => _monitor.OnProviderChange(null!, (_, _) => { }));
	}

	[Fact]
	public void OnProviderChange_ThrowsForNullListener()
	{
		Should.Throw<ArgumentNullException>(
			() => _monitor.OnProviderChange("test", null!));
	}

	[Fact]
	public void OnProviderChange_ReturnsDisposable()
	{
		var result = _monitor.OnProviderChange("test", (_, _) => { });
		result.ShouldNotBeNull();
		result.Dispose(); // Should not throw
	}

	[Fact]
	public void GetLastChangeTime_ThrowsForNullProviderName()
	{
		Should.Throw<ArgumentException>(
			() => _monitor.GetLastChangeTime(null!));
	}

	[Fact]
	public void GetLastChangeTime_ReturnsNullWhenNeverChanged()
	{
		var result = _monitor.GetLastChangeTime("unknown");
		result.ShouldBeNull();
	}

	[Fact]
	public void GetLastChangeTime_ReturnsTimeAfterProviderChange()
	{
		_monitor.OnProviderChange("test-provider", (_, _) => { });

		var result = _monitor.GetLastChangeTime("test-provider");
		result.ShouldNotBeNull();
	}

	[Fact]
	public void ForceReload_NotifiesProviderListeners()
	{
		var notified = false;
		_monitor.OnProviderChange("reload-test", (_, _) => notified = true);

		_monitor.ForceReload("reload-test");

		notified.ShouldBeTrue();
	}

	[Fact]
	public void ForceReload_WithNull_ReloadsAllProviders()
	{
		var notified1 = false;
		var notified2 = false;
		_monitor.OnProviderChange("p1", (_, _) => notified1 = true);
		_monitor.OnProviderChange("p2", (_, _) => notified2 = true);

		_monitor.ForceReload();

		notified1.ShouldBeTrue();
		notified2.ShouldBeTrue();
	}

	[Fact]
	public void ValidateOptions_ReturnsErrorForNull()
	{
		var errors = _monitor.ValidateOptions(null!).ToList();
		errors.ShouldContain("Options cannot be null");
	}

	[Fact]
	public void ValidateOptions_ReturnsEmptyForValid()
	{
		var options = CreateConfig("valid");
		options.MaxPoolSize = 10;
		options.ConnectionTimeout = 30;
		options.CommandTimeout = 30;

		var errors = _monitor.ValidateOptions(options).ToList();
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void GetChangeToken_ReturnsNonNull()
	{
		var token = PersistenceOptionsMonitor<ProviderConfiguration>.GetChangeToken();
		token.ShouldNotBeNull();
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		Should.NotThrow(() => _monitor.Dispose());
	}

	public void Dispose() => _monitor.Dispose();
}
