// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="MessageBusOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageBusOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new TestMessageBusOptions();

		// Assert
		options.EnableRetries.ShouldBeFalse();
		options.TargetUri.ShouldBeNull();
		options.EnableTelemetry.ShouldBeTrue();
	}

	[Fact]
	public void NotExposeRetryTuningSettings()
	{
		// Whether a bus retries is a per-bus decision; how it retries is configured once for the
		// pipeline. Re-adding a tuning knob here would give retry two configuration sites and a
		// precedence question between them, and — because nothing on this type is read when the
		// pipeline is built — a consumer who set one would see no error and conclude it applied.
		var settable = typeof(MessageBusOptions)
			.GetProperties()
			.Select(static p => p.Name)
			.ToArray();

		settable.ShouldNotContain("MaxRetryAttempts");
		settable.ShouldNotContain("RetryStrategy");
		settable.ShouldNotContain("RetryDelay");
		settable.ShouldNotContain("JitterFactor");

		// Liveness: the switch that IS honoured must still be here, or this test passes against a
		// type with no retry surface at all.
		settable.ShouldContain("EnableRetries");
	}

	[Fact]
	public void Name_CanBeSet()
	{
		// Act
		var options = new TestMessageBusOptions { Name = "my-bus" };

		// Assert
		options.Name.ShouldBe("my-bus");
	}

	[Fact]
	public void EnableRetries_CanBeSet()
	{
		// Act
		var options = new TestMessageBusOptions { EnableRetries = true };

		// Assert
		options.EnableRetries.ShouldBeTrue();
	}

	[Fact]
	public void TargetUri_CanBeSet()
	{
		// Arrange
		var uri = new Uri("https://remote-bus.example.com");

		// Act
		var options = new TestMessageBusOptions { TargetUri = uri };

		// Assert
		options.TargetUri.ShouldBe(uri);
	}

	[Fact]
	public void EnableTelemetry_CanBeDisabled()
	{
		// Act
		var options = new TestMessageBusOptions { EnableTelemetry = false };

		// Assert
		options.EnableTelemetry.ShouldBeFalse();
	}

	/// <summary>
	/// Concrete test implementation since MessageBusOptions is abstract.
	/// </summary>
	private sealed class TestMessageBusOptions : MessageBusOptions;
}
