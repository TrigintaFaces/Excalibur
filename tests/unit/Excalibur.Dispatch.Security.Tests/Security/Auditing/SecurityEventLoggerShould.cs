// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Unit tests for <see cref="SecurityEventLogger"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Auditing")]
public sealed class SecurityEventLoggerShould : IAsyncDisposable
{
	private readonly ILogger<SecurityEventLogger> _logger;
	private readonly ISecurityEventStore _eventStore;
	private readonly SecurityEventLogger _sut;

	public SecurityEventLoggerShould()
	{
		_logger = A.Fake<ILogger<SecurityEventLogger>>();
		_eventStore = A.Fake<ISecurityEventStore>();
		_sut = new SecurityEventLogger(_logger, _eventStore);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.StopAsync(CancellationToken.None);
		_sut.Dispose();
	}

	[Fact]
	public void ImplementISecurityEventLogger()
	{
		// Assert
		_sut.ShouldBeAssignableTo<ISecurityEventLogger>();
	}

	[Fact]
	public void ImplementIHostedService()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IHostedService>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(SecurityEventLogger).IsPublic.ShouldBeTrue();
		typeof(SecurityEventLogger).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SecurityEventLogger(null!, _eventStore));
	}

	[Fact]
	public void ThrowWhenEventStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SecurityEventLogger(_logger, null!));
	}

	[Fact]
	public async Task StartSuccessfully()
	{
		// Act & Assert
		await Should.NotThrowAsync(async () =>
			await _sut.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task StopSuccessfully()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act & Assert
		await Should.NotThrowAsync(async () =>
			await _sut.StopAsync(CancellationToken.None));
	}

	[Fact]
	public async Task LogSecurityEventWithoutContext()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act & Assert
		await Should.NotThrowAsync(async () =>
			await _sut.LogSecurityEventAsync(
				SecurityEventType.AuthenticationSuccess,
				"Test authentication success",
				SecuritySeverity.Low, CancellationToken.None));
	}

	[Fact]
	public async Task LogSecurityEventWithAllSeverityLevels()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act & Assert - All severity levels
		await _sut.LogSecurityEventAsync(SecurityEventType.AuthenticationSuccess, "Low", SecuritySeverity.Low, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.AuthorizationSuccess, "Medium", SecuritySeverity.Medium, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.AuthorizationFailure, "High", SecuritySeverity.High, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.InjectionAttempt, "Critical", SecuritySeverity.Critical, CancellationToken.None);
	}

	[Fact]
	public async Task LogSecurityEventWithAllEventTypes()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act & Assert - Multiple event types
		await _sut.LogSecurityEventAsync(SecurityEventType.AuthenticationSuccess, "Auth success", SecuritySeverity.Low, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.AuthenticationFailure, "Auth failure", SecuritySeverity.Medium, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.AuthorizationSuccess, "Authz success", SecuritySeverity.Low, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.AuthorizationFailure, "Authz failure", SecuritySeverity.High, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.ValidationFailure, "Validation", SecuritySeverity.Medium, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.InjectionAttempt, "Injection", SecuritySeverity.Critical, CancellationToken.None);
		await _sut.LogSecurityEventAsync(SecurityEventType.RateLimitExceeded, "Rate limit", SecuritySeverity.Medium, CancellationToken.None);
	}

	[Fact]
	public async Task DisposeWithoutException()
	{
		// Act & Assert
		await Should.NotThrowAsync(async () =>
		{
			await _sut.StartAsync(CancellationToken.None);
			await _sut.StopAsync(CancellationToken.None);
			_sut.Dispose();
		});
	}

	[Fact]
	public async Task HandleMultipleStopCallsGracefully()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act - Stop twice, second call should still work
		await _sut.StopAsync(CancellationToken.None);

		// Assert - Second stop should not throw
		await Should.NotThrowAsync(async () =>
			await _sut.StopAsync(CancellationToken.None));
	}
}
