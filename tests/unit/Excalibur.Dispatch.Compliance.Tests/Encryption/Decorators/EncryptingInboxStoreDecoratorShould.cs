using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingInboxStoreDecoratorShould
{
	private readonly IInboxStore _inner = A.Fake<IInboxStore>();
	private readonly IEncryptionProviderRegistry _registry = A.Fake<IEncryptionProviderRegistry>();
	private readonly IEncryptionProvider _provider = A.Fake<IEncryptionProvider>();

	private EncryptingInboxStoreDecorator CreateSut(EncryptionMode mode = EncryptionMode.EncryptAndDecrypt)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions
		{
			Mode = mode,
			DefaultPurpose = "test",
		});
		return new EncryptingInboxStoreDecorator(_inner, _registry, options);
	}

	[Fact]
	public async Task Encrypt_payload_on_create_entry_when_encrypt_mode()
	{
		// Arrange
		var sut = CreateSut(EncryptionMode.EncryptAndDecrypt);
		A.CallTo(() => _registry.GetPrimary()).Returns(_provider);
		A.CallTo(() => _provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [99],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		var entry = new InboxEntry
		{
			MessageId = "m1",
			HandlerType = "handler",
			MessageType = "type",
			Payload = [1, 2, 3],
			Status = InboxStatus.Received,
			ReceivedAt = DateTimeOffset.UtcNow,
		};

		A.CallTo(() => _inner.CreateEntryAsync(A<string>._, A<string>._, A<string>._, A<byte[]>._, A<IDictionary<string, object>>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry>(entry));

		// Act
		await sut.CreateEntryAsync("m1", "handler", "type", [1, 2, 3], new Dictionary<string, object>(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — encryption was called
		A.CallTo(() => _provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Throw_on_create_entry_in_read_only_mode()
	{
		// Arrange
		var sut = CreateSut(EncryptionMode.DecryptOnlyReadOnly);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await sut.CreateEntryAsync("m1", "handler", "type", [1, 2, 3], new Dictionary<string, object>(), CancellationToken.None)
				.ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Delegate_mark_processed_to_inner()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		await sut.MarkProcessedAsync("m1", "handler", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _inner.MarkProcessedAsync("m1", "handler", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Delegate_try_mark_as_processed_to_inner()
	{
		// Arrange
		var sut = CreateSut();
		A.CallTo(() => _inner.TryMarkAsProcessedAsync("m1", "handler", A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));

		// Act
		var result = await sut.TryMarkAsProcessedAsync("m1", "handler", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task Delegate_is_processed_to_inner()
	{
		// Arrange
		var sut = CreateSut();
		A.CallTo(() => _inner.IsProcessedAsync("m1", "handler", A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));

		// Act
		var result = await sut.IsProcessedAsync("m1", "handler", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_null_when_entry_not_found_on_get()
	{
		// Arrange
		var sut = CreateSut();
		A.CallTo(() => _inner.GetEntryAsync("m1", "handler", A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>((InboxEntry?)null));

		// Act
		var result = await sut.GetEntryAsync("m1", "handler", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task Return_unencrypted_entry_unchanged_on_get()
	{
		// Arrange
		var sut = CreateSut();
		var entry = new InboxEntry
		{
			MessageId = "m1",
			HandlerType = "handler",
			MessageType = "type",
			Payload = [1, 2, 3], // Not encrypted (no magic prefix)
			Status = InboxStatus.Processed,
			ReceivedAt = DateTimeOffset.UtcNow,
		};
		A.CallTo(() => _inner.GetEntryAsync("m1", "handler", A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>(entry));

		// Act
		var result = await sut.GetEntryAsync("m1", "handler", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.Payload.ShouldBe(new byte[] { 1, 2, 3 });
	}

	[Fact]
	public async Task Delegate_mark_failed_to_inner()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		await sut.MarkFailedAsync("m1", "handler", "error", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		A.CallTo(() => _inner.MarkFailedAsync("m1", "handler", "error", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Delegate_get_statistics_to_inner()
	{
		// Arrange
		var sut = CreateSut();
		var stats = new InboxStatistics();
		A.CallTo(() => _inner.GetStatisticsAsync(A<CancellationToken>._))
			.Returns(new ValueTask<InboxStatistics>(stats));

		// Act
		var result = await sut.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(stats);
	}

	[Fact]
	public async Task Delegate_cleanup_to_inner()
	{
		// Arrange
		var sut = CreateSut();
		A.CallTo(() => _inner.CleanupAsync(A<TimeSpan>._, A<CancellationToken>._))
			.Returns(new ValueTask<int>(5));

		// Act
		var result = await sut.CleanupAsync(TimeSpan.FromDays(30), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBe(5);
	}

	[Fact]
	public void Throw_for_null_inner()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions());
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingInboxStoreDecorator(null!, _registry, options));
	}

	[Fact]
	public void Throw_for_null_registry()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions());
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingInboxStoreDecorator(_inner, null!, options));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EncryptingInboxStoreDecorator(_inner, _registry, null!));
	}
}
