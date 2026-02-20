// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

namespace Excalibur.Data.Tests.Core;

/// <summary>
/// Depth tests for <see cref="PersistenceOptionsMonitor{TOptions}"/>.
/// Covers change tracking, provider listeners, validation, and force reload.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PersistenceOptionsMonitorDepthShould : IDisposable
{
	private readonly IPersistenceConfiguration _fakeConfig;
	private readonly IOptionsMonitor<PersistenceOptionsMonitorTestOptions> _fakeMonitor;
	private readonly PersistenceOptionsMonitor<PersistenceOptionsMonitorTestOptions> _monitor;

	public PersistenceOptionsMonitorDepthShould()
	{
		_fakeConfig = A.Fake<IPersistenceConfiguration>();
		_fakeMonitor = A.Fake<IOptionsMonitor<PersistenceOptionsMonitorTestOptions>>();
		var defaultOptions = new PersistenceOptionsMonitorTestOptions();
		A.CallTo(() => _fakeMonitor.CurrentValue).Returns(defaultOptions);
		A.CallTo(() => _fakeMonitor.Get(A<string?>._)).Returns(defaultOptions);

		_monitor = new PersistenceOptionsMonitor<PersistenceOptionsMonitorTestOptions>(_fakeConfig, _fakeMonitor);
	}

	[Fact]
	public void ReturnCurrentValue()
	{
		// Act
		var value = _monitor.CurrentValue;

		// Assert
		value.ShouldNotBeNull();
	}

	[Fact]
	public void GetProviderOptions()
	{
		// Act
		var options = _monitor.GetProviderOptions("sqlserver");

		// Assert
		options.ShouldNotBeNull();
		A.CallTo(() => _fakeMonitor.Get("sqlserver")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowWhenProviderNameIsNullOrWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _monitor.GetProviderOptions(null!));
		Should.Throw<ArgumentException>(() => _monitor.GetProviderOptions(""));
		Should.Throw<ArgumentException>(() => _monitor.GetProviderOptions("   "));
	}

	[Fact]
	public void OnChangeReturnDisposableSubscription()
	{
		// Act
		var subscription = _monitor.OnChange((options, name) => { });

		// Assert
		subscription.ShouldNotBeNull();
		subscription.Dispose(); // should not throw
	}

	[Fact]
	public void ThrowWhenOnChangeListenerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _monitor.OnChange(null!));
	}

	[Fact]
	public void OnProviderChangeReturnDisposableSubscription()
	{
		// Act
		var subscription = _monitor.OnProviderChange("provider1", (options, name) => { });

		// Assert
		subscription.ShouldNotBeNull();
		subscription.Dispose(); // should not throw
	}

	[Fact]
	public void ThrowWhenOnProviderChangeProviderNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _monitor.OnProviderChange(null!, (_, _) => { }));
	}

	[Fact]
	public void ThrowWhenOnProviderChangeListenerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _monitor.OnProviderChange("provider1", null!));
	}

	[Fact]
	public void ForceReloadSpecificProvider()
	{
		// Arrange
		var callbackCalled = false;
		_ = _monitor.OnProviderChange("provider1", (_, _) => { callbackCalled = true; });

		// Act
		_monitor.ForceReload("provider1");

		// Assert
		callbackCalled.ShouldBeTrue();
	}

	[Fact]
	public void ForceReloadAllProviders()
	{
		// Arrange
		var callback1Called = false;
		var callback2Called = false;
		_ = _monitor.OnProviderChange("p1", (_, _) => { callback1Called = true; });
		_ = _monitor.OnProviderChange("p2", (_, _) => { callback2Called = true; });

		// Act
		_monitor.ForceReload();

		// Assert
		callback1Called.ShouldBeTrue();
		callback2Called.ShouldBeTrue();
	}

	[Fact]
	public void GetLastChangeTimeReturnNullForUnknownProvider()
	{
		// Act
		var time = _monitor.GetLastChangeTime("unknown");

		// Assert
		time.ShouldBeNull();
	}

	[Fact]
	public void GetLastChangeTimeReturnValueAfterSubscription()
	{
		// Arrange
		_ = _monitor.OnProviderChange("tracked", (_, _) => { });

		// Act
		var time = _monitor.GetLastChangeTime("tracked");

		// Assert
		time.ShouldNotBeNull();
		time.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
	}

	[Fact]
	public void ThrowWhenGetLastChangeTimeProviderNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _monitor.GetLastChangeTime(null!));
	}

	[Fact]
	public void ValidateOptionsReturnErrorWhenNull()
	{
		// Act
		var errors = _monitor.ValidateOptions(null!).ToList();

		// Assert
		errors.ShouldNotBeEmpty();
		errors.ShouldContain("Options cannot be null");
	}

	[Fact]
	public void ValidateOptionsReturnEmptyForValidOptions()
	{
		// Arrange
		var options = new PersistenceOptionsMonitorTestOptions();

		// Act
		var errors = _monitor.ValidateOptions(options).ToList();

		// Assert
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void GetStaticChangeToken()
	{
		// Act
		var token = PersistenceOptionsMonitor<PersistenceOptionsMonitorTestOptions>.GetChangeToken();

		// Assert
		token.ShouldNotBeNull();
		token.HasChanged.ShouldBeFalse();
	}

	[Fact]
	public void DisposeCleanupSubscriptions()
	{
		// Arrange
		_ = _monitor.OnChange((_, _) => { });
		_ = _monitor.OnProviderChange("p1", (_, _) => { });

		// Act & Assert - should not throw
		_monitor.Dispose();
	}

	[Fact]
	public void GetNamedOptions()
	{
		// Act
		var options = _monitor.Get("named");

		// Assert
		options.ShouldNotBeNull();
		A.CallTo(() => _fakeMonitor.Get("named")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ValidateOptionsReturnErrorWhenValidateThrows()
	{
		// Arrange
		var options = new PersistenceOptionsMonitorTestOptions { ShouldThrowOnValidate = true };

		// Act
		var errors = _monitor.ValidateOptions(options).ToList();

		// Assert
		errors.ShouldNotBeEmpty();
		errors.ShouldContain(e => e.Contains("Validation failed"));
	}

	public void Dispose()
	{
		_monitor.Dispose();
	}
}

/// <summary>
/// Test implementation of IPersistenceOptions for PersistenceOptionsMonitor tests.
/// Must be public for FakeItEasy to proxy IOptionsMonitor of this type.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible - test helper at file level
public sealed class PersistenceOptionsMonitorTestOptions : IPersistenceOptions
{
	public string ConnectionString { get; set; } = "Server=test;Database=test";
	public int ConnectionTimeout { get; set; } = 30;
	public int CommandTimeout { get; set; } = 30;
	public IDictionary<string, object> ProviderSpecificOptions { get; } = new Dictionary<string, object>();

	/// <summary>
	/// Gets or sets a value indicating whether <see cref="Validate"/> should throw for testing.
	/// </summary>
	public bool ShouldThrowOnValidate { get; set; }

	public void Validate()
	{
		if (ShouldThrowOnValidate)
		{
			throw new InvalidOperationException("Test validation error");
		}
	}
}
#pragma warning restore CA1034
