using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryScheduleStoreShould
{
	private readonly InMemoryScheduleStore _store = new();
	private readonly CancellationToken _ct = CancellationToken.None;

	[Fact]
	public async Task StoreAsync_AddsNewMessage()
	{
		var msg = new ScheduledMessage { MessageName = "test" };

		await _store.StoreAsync(msg, _ct);

		var all = (await _store.GetAllAsync(_ct)).ToList();
		all.Count.ShouldBe(1);
		all[0].MessageName.ShouldBe("test");
	}

	[Fact]
	public async Task StoreAsync_ThrowsOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _store.StoreAsync(null!, _ct));
	}

	[Fact]
	public async Task StoreAsync_UpdatesExistingMessage()
	{
		var msg = new ScheduledMessage { MessageName = "original" };
		await _store.StoreAsync(msg, _ct);

		msg.MessageName = "updated";
		await _store.StoreAsync(msg, _ct);

		var all = (await _store.GetAllAsync(_ct)).ToList();
		all.Count.ShouldBe(1);
		all[0].MessageName.ShouldBe("updated");
	}

	[Fact]
	public async Task CompleteAsync_DisablesMessage()
	{
		var msg = new ScheduledMessage { Enabled = true };
		await _store.StoreAsync(msg, _ct);

		await _store.CompleteAsync(msg.Id, _ct);

		var all = (await _store.GetAllAsync(_ct)).ToList();
		all[0].Enabled.ShouldBeFalse();
	}

	[Fact]
	public async Task GetAllAsync_ReturnsAllMessages()
	{
		await _store.StoreAsync(new ScheduledMessage { MessageName = "a" }, _ct);
		await _store.StoreAsync(new ScheduledMessage { MessageName = "b" }, _ct);

		var all = (await _store.GetAllAsync(_ct)).ToList();

		all.Count.ShouldBe(2);
	}

	[Fact]
	public async Task GetAllAsync_ReturnsEmptyWhenNoMessages()
	{
		var all = (await _store.GetAllAsync(_ct)).ToList();

		all.ShouldBeEmpty();
	}

	[Fact]
	public async Task AddOrUpdateAsync_AddsNewMessage()
	{
		var msg = new ScheduledMessage { MessageName = "new" };

		await _store.AddOrUpdateAsync(msg, _ct);

		var all = (await _store.GetAllAsync(_ct)).ToList();
		all.Count.ShouldBe(1);
	}

	[Fact]
	public async Task AddOrUpdateAsync_UpdatesExistingMessage()
	{
		var msg = new ScheduledMessage { MessageName = "first" };
		await _store.AddOrUpdateAsync(msg, _ct);

		msg.MessageName = "second";
		await _store.AddOrUpdateAsync(msg, _ct);

		var all = (await _store.GetAllAsync(_ct)).ToList();
		all.Count.ShouldBe(1);
		all[0].MessageName.ShouldBe("second");
	}

	[Fact]
	public async Task AddOrUpdateAsync_ThrowsOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _store.AddOrUpdateAsync(null!, _ct));
	}
}
