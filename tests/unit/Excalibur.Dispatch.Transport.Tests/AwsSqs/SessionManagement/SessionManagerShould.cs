// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

using AwsISessionStore = Excalibur.Dispatch.Transport.Aws.ISessionStore;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SessionManagerShould
{
	private readonly AwsISessionStore _fakeStore = A.Fake<AwsISessionStore>();
	private readonly SessionManager _sut;

	public SessionManagerShould()
	{
		_sut = new SessionManager(_fakeStore, NullLogger<SessionManager>.Instance);
	}

	[Fact]
	public void ThrowWhenSessionStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SessionManager(null!, NullLogger<SessionManager>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SessionManager(A.Fake<AwsISessionStore>(), null!));
	}

	[Fact]
	public async Task CreateSessionAsync()
	{
		// Arrange
		var expected = new SessionData { Id = "session-1" };
		A.CallTo(() => _fakeStore.CreateAsync("session-1", TimeSpan.FromMinutes(5), A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _sut.CreateSessionAsync("session-1", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task GetSessionAsync()
	{
		// Arrange
		var expected = new SessionData { Id = "session-1" };
		A.CallTo(() => _fakeStore.TryGetAsync("session-1", A<CancellationToken>._))
			.Returns(expected);

		// Act
		var result = await _sut.GetSessionAsync("session-1", CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task ReturnNullForNonExistentSession()
	{
		// Arrange
		A.CallTo(() => _fakeStore.TryGetAsync("missing", A<CancellationToken>._))
			.Returns((SessionData?)null);

		// Act
		var result = await _sut.GetSessionAsync("missing", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task UpdateSessionAsync()
	{
		// Arrange
		var session = new SessionData { Id = "session-1", MessageCount = 5 };
		var updated = new SessionData { Id = "session-1", MessageCount = 10 };
		A.CallTo(() => _fakeStore.UpdateAsync(session, A<CancellationToken>._))
			.Returns(updated);

		// Act
		var result = await _sut.UpdateSessionAsync(session, CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(updated);
	}

	[Fact]
	public async Task DeleteSessionAsync()
	{
		// Act
		await _sut.DeleteSessionAsync("session-1", CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeStore.DeleteAsync("session-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SessionExistsAsync()
	{
		// Arrange
		A.CallTo(() => _fakeStore.ExistsAsync("session-1", A<CancellationToken>._))
			.Returns(true);

		// Act
		var result = await _sut.SessionExistsAsync("session-1", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task SessionNotExistsAsync()
	{
		// Arrange
		A.CallTo(() => _fakeStore.ExistsAsync("missing", A<CancellationToken>._))
			.Returns(false);

		// Act
		var result = await _sut.SessionExistsAsync("missing", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task AcquireLockViLockCoordinator()
	{
		// Act — test the ISessionLockCoordinator explicit interface implementation
		ISessionLockCoordinator coordinator = _sut;
		var token = await coordinator.AcquireLockAsync("session-1", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Assert
		token.ShouldNotBeNull();
		token.SessionId.ShouldBe("session-1");
		token.Token.ShouldNotBeNullOrWhiteSpace();
		token.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task TryAcquireLockViaLockCoordinator()
	{
		ISessionLockCoordinator coordinator = _sut;
		var token = await coordinator.TryAcquireLockAsync("session-2", TimeSpan.FromMinutes(1), CancellationToken.None);

		token.ShouldNotBeNull();
		token!.SessionId.ShouldBe("session-2");
	}

	[Fact]
	public async Task ExtendLockViaLockCoordinator()
	{
		ISessionLockCoordinator coordinator = _sut;
		var lockToken = new SessionLockToken { SessionId = "s1", Token = "t1" };

		var result = await coordinator.ExtendLockAsync(lockToken, TimeSpan.FromMinutes(5), CancellationToken.None);

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ReleaseLockViaLockCoordinator()
	{
		ISessionLockCoordinator coordinator = _sut;
		var lockToken = new SessionLockToken { SessionId = "s1", Token = "t1" };

		var result = await coordinator.ReleaseLockAsync(lockToken, CancellationToken.None);

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task IsLockedReturnsFalse()
	{
		ISessionLockCoordinator coordinator = _sut;

		var result = await coordinator.IsLockedAsync("session-1", CancellationToken.None);

		result.ShouldBeFalse();
	}
}
