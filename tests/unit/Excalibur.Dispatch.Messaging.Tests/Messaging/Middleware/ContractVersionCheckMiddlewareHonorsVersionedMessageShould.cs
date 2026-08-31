// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Versioning;
using Excalibur.Dispatch.Options.Middleware;

using Tests.Shared.TestFakes;

using MessageResult = Excalibur.Dispatch.MessageResult;

using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// The framework declares <see cref="IVersionedMessage" />, so a message that implements it states its
/// version through that contract. The middleware must read the interface rather than probe for a property
/// whose name merely happens to match.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
public sealed class ContractVersionCheckMiddlewareHonorsVersionedMessageShould
{
	private readonly IContractVersionService _versionService = A.Fake<IContractVersionService>();

	private ContractVersionCheckMiddleware CreateMiddleware() =>
		new(
			MsOptions.Create(new ContractVersionCheckOptions { Enabled = true }),
			_versionService,
			NullLoggerFactory.Instance.CreateLogger<ContractVersionCheckMiddleware>());

	/// <summary>
	/// An explicit interface implementation is invisible to a public-property probe, so this case is the
	/// one a name probe structurally cannot serve.
	/// </summary>
	[Fact]
	public async Task ReadTheVersionFromAnExplicitInterfaceImplementation()
	{
		var observed = await CaptureCheckedVersion(new ExplicitlyVersionedMessage()).ConfigureAwait(true);

		observed.ShouldBe("7");
	}

	[Fact]
	public async Task ReadTheVersionFromAnImplicitInterfaceImplementation()
	{
		var observed = await CaptureCheckedVersion(new VersionedMessage()).ConfigureAwait(true);

		observed.ShouldBe("4");
	}

	/// <summary>
	/// The interface carries the message's own statement of its version, so it outranks static attribute
	/// metadata declared on the type.
	/// </summary>
	[Fact]
	public async Task PreferTheInterfaceOverAContractVersionAttribute()
	{
		var observed = await CaptureCheckedVersion(new AttributedVersionedMessage()).ConfigureAwait(true);

		observed.ShouldBe("9");
	}

	/// <summary>
	/// Liveness arm: dropping the probe for our own contract must not disable it for foreign message types
	/// that carry a version property without implementing the interface.
	/// </summary>
	[Fact]
	public async Task StillProbeAForeignTypeThatDoesNotImplementTheInterface()
	{
		var observed = await CaptureCheckedVersion(new ForeignMessageWithVersionProperty()).ConfigureAwait(true);

		observed.ShouldBe("2");
	}

	private async Task<string?> CaptureCheckedVersion(IDispatchMessage message)
	{
		string? observed = null;

		_ = A.CallTo(() => _versionService.CheckCompatibilityAsync(
				A<string>._, A<string>._, A<string[]?>._, A<CancellationToken>._))
			.Invokes((string _, string version, string[]? _, CancellationToken _) => observed = version)
			.Returns(Task.FromResult(VersionCompatibilityResult.Compatible()));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		_ = await CreateMiddleware()
			.InvokeAsync(message, new FakeMessageContext { MessageId = "version-probe" }, next, CancellationToken.None)
			.ConfigureAwait(true);

		return observed;
	}

	private sealed class ExplicitlyVersionedMessage : IDispatchMessage, IVersionedMessage
	{
		int IVersionedMessage.Version => 7;

		string IVersionedMessage.MessageType => "ExplicitlyVersioned";

		public Guid Id { get; } = Guid.NewGuid();

		public string MessageId { get; } = Guid.NewGuid().ToString();

		public string MessageType => "ExplicitlyVersioned";

		public MessageKinds Kind => MessageKinds.Event;

		public ReadOnlyMemory<byte> Payload => ReadOnlyMemory<byte>.Empty;

		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();

		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class VersionedMessage : IDispatchMessage, IVersionedMessage
	{
		public int Version => 4;

		public Guid Id { get; } = Guid.NewGuid();

		public string MessageId { get; } = Guid.NewGuid().ToString();

		public string MessageType => "Versioned";

		public MessageKinds Kind => MessageKinds.Event;

		public ReadOnlyMemory<byte> Payload => ReadOnlyMemory<byte>.Empty;

		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();

		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	[ContractVersion("1")]
	private sealed class AttributedVersionedMessage : IDispatchMessage, IVersionedMessage
	{
		public int Version => 9;

		public Guid Id { get; } = Guid.NewGuid();

		public string MessageId { get; } = Guid.NewGuid().ToString();

		public string MessageType => "AttributedVersioned";

		public MessageKinds Kind => MessageKinds.Event;

		public ReadOnlyMemory<byte> Payload => ReadOnlyMemory<byte>.Empty;

		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();

		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class ForeignMessageWithVersionProperty : IDispatchMessage
	{
		public int Version => 2;

		public Guid Id { get; } = Guid.NewGuid();

		public string MessageId { get; } = Guid.NewGuid().ToString();

		public string MessageType => "Foreign";

		public MessageKinds Kind => MessageKinds.Event;

		public ReadOnlyMemory<byte> Payload => ReadOnlyMemory<byte>.Empty;

		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();

		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}
}
