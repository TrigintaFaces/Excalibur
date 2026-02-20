// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Encryption;
using Excalibur.Dispatch.Messaging.Encryption;

using Tests.Shared.TestTypes.Infrastructure;

using IMessageSerializer = Excalibur.Dispatch.Abstractions.Serialization.IMessageSerializer;

namespace Excalibur.Dispatch.Tests.Functional.Messaging.Encryption;

/// <summary>
///     Functional tests verifying message bus encryption behavior.
/// </summary>
[Trait("Category", "Functional")]
public sealed class EncryptionFunctionalShould
{
	public required string MessageId { get; set; } = Guid.NewGuid().ToString();

	[Fact]
	public async Task EncryptAndDecryptPayload()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton<IMessageSerializer, TestSerializer>();
		_ = services.AddEncryptionProvider(new RecordingEncryptionProvider(0x5a));
		_ = services.AddSingleton(new RabbitMqOptions
		{
			HostName = "localhost",
			UserName = "guest",
			Password = "guest",
			VirtualHost = "/",
			Exchange = "ex",
			RoutingKey = "rk",
			EnableEncryption = true,
		});
		_ = services.AddMessageBus(
				"test",
				isRemote: false,
				static sp => new RecordingBus(
						sp.GetRequiredService<IMessageSerializer>(),
						sp.GetRequiredService<RabbitMqOptions>(),
						sp.GetRequiredService<IEncryptionProvider>()));

		var provider = services.BuildServiceProvider();
		await using var disposable = provider.ConfigureAwait(true);
		var bus = (RecordingBus)provider.GetRequiredKeyedService<IMessageBus>("test");
		var encryption = (RecordingEncryptionProvider)provider.GetRequiredService<IEncryptionProvider>();
		var serializer = provider.GetRequiredService<IMessageSerializer>();
		var message = new TestEvent { MessageId = Guid.NewGuid().ToString(), Text = "hello" };
		var context = new MessageContext(message, provider);

		// Act
		await bus.PublishAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		encryption.EncryptCalls.ShouldBe(1);
		var serialized = serializer.Serialize(message);
		var base64 = Convert.ToBase64String(serialized);
		bus.CapturedPayload.ShouldNotBe(base64);
		var decryptedBase64 = encryption.Decrypt(bus.CapturedPayload);
		decryptedBase64.ShouldBe(base64);
		Convert.FromBase64String(decryptedBase64).ShouldBe(serialized);
	}

	/// <summary>
	/// Test event for encryption tests.
	/// IDispatchEvent (and IDispatchMessage) is now a marker interface with no members.
	/// </summary>
	private sealed record TestEvent : IDispatchEvent
	{
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
		public string Text { get; init; } = string.Empty;
	}

	private sealed class RecordingEncryptionProvider(byte key) : IEncryptionProvider
	{
		private readonly XorEncryptionProvider _inner = new(key);

		public int EncryptCalls { get; private set; }

		public string Encrypt(string plaintext)
		{
			EncryptCalls++;
			return _inner.Encrypt(plaintext);
		}

		public string Decrypt(string ciphertext) => _inner.Decrypt(ciphertext);
	}

	private sealed class RecordingBus(
		IMessageSerializer serializer,
		RabbitMqOptions options,
		IEncryptionProvider provider) : IMessageBus
	{
		private readonly bool _useEncryption = options.EnableEncryption && provider is not null;

		public string CapturedPayload { get; private set; } = string.Empty;

		public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
		{
			Capture(serializer.Serialize(action));
			return Task.CompletedTask;
		}

		public Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
		{
			Capture(serializer.Serialize(evt));
			return Task.CompletedTask;
		}

		public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
		{
			Capture(serializer.Serialize(doc));
			return Task.CompletedTask;
		}

		private void Capture(byte[] bytes)
		{
			var text = Convert.ToBase64String(bytes);
			if (_useEncryption)
			{
				text = provider.Encrypt(text);
			}

			CapturedPayload = text;
		}
	}

	private sealed class TestSerializer : IMessageSerializer
	{
		public string SerializerName => "test";

		public string SerializerVersion => "1.0";

		public byte[] Serialize<T>(T message)
			=> JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());

		public T Deserialize<T>(byte[] data)
			=> JsonSerializer.Deserialize<T>(data)!;
	}
}
