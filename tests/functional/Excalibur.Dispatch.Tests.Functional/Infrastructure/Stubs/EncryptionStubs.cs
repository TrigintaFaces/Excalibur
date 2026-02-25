// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#nullable enable

namespace Excalibur.Dispatch.Abstractions.Encryption
{
	/// <summary>
	/// Stub interface for encryption provider (removed from main codebase).
	/// </summary>
	public interface IEncryptionProvider
	{
		string Encrypt(string plaintext);

		string Decrypt(string ciphertext);
	}
}

namespace Excalibur.Dispatch.Messaging.Encryption
{
	/// <summary>
	/// Extension method for adding encryption provider.
	/// </summary>
	public static class EncryptionServiceCollectionExtensions
	{
		public static IServiceCollection AddEncryptionProvider(
			this IServiceCollection services,
			Abstractions.Encryption.IEncryptionProvider provider)
		{
			return services.AddSingleton(provider);
		}
	}

	/// <summary>
	/// Stub XOR encryption provider for tests.
	/// </summary>
	public sealed class XorEncryptionProvider : Abstractions.Encryption.IEncryptionProvider
	{
		private readonly byte _key;

		public XorEncryptionProvider(byte key)
		{
			_key = key;
		}

		public string Encrypt(string plaintext)
		{
			var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
			for (var i = 0; i < bytes.Length; i++)
			{
				bytes[i] ^= _key;
			}
			return Convert.ToBase64String(bytes);
		}

		public string Decrypt(string ciphertext)
		{
			var bytes = Convert.FromBase64String(ciphertext);
			for (var i = 0; i < bytes.Length; i++)
			{
				bytes[i] ^= _key;
			}
			return System.Text.Encoding.UTF8.GetString(bytes);
		}
	}
}

namespace Tests.Shared.TestTypes.Infrastructure
{
	/// <summary>
	/// Stub RabbitMQ options for tests.
	/// </summary>
	public sealed class RabbitMqOptions
	{
		public string HostName { get; set; } = "localhost";
		public string UserName { get; set; } = "guest";
		public string Password { get; set; } = "guest";
		public string VirtualHost { get; set; } = "/";
		public string Exchange { get; set; } = string.Empty;
		public string RoutingKey { get; set; } = string.Empty;
		public bool EnableEncryption { get; set; }
	}
}

namespace Tests.Shared
{
	/// <summary>
	/// Stub TestOutputSink for backward compatibility with functional tests.
	/// </summary>
	public sealed class TestOutputSink : IDisposable
	{
		private readonly Xunit.Abstractions.ITestOutputHelper _output;
		private bool _disposed;

		public TestOutputSink(Xunit.Abstractions.ITestOutputHelper output)
		{
			_output = output;
		}

		public void Write(string message)
		{
			if (!_disposed)
			{
				_output.WriteLine(message);
			}
		}

		public void Dispose()
		{
			_disposed = true;
		}
	}
}

namespace Tests.Shared.Fixtures
{
	/// <summary>
	/// Stub AzureServiceBusEmulatorFixture for backward compatibility.
	/// </summary>
	public sealed class AzureServiceBusEmulatorFixture : IAsyncLifetime
	{
		public string ConnectionString { get; } = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test";

		public Task InitializeAsync() => Task.CompletedTask;

		public Task DisposeAsync() => Task.CompletedTask;
	}
}
