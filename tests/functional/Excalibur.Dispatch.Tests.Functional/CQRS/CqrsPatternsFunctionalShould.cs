// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.CQRS;

/// <summary>
/// End-to-end functional tests for CQRS patterns including commands, queries, and events.
/// Tests demonstrate Command Query Responsibility Segregation patterns in isolation.
/// </summary>
[Trait("Category", "Functional")]
public sealed class CqrsPatternsFunctionalShould : FunctionalTestBase
{
	#region Command Tests

	[Fact]
	public async Task Command_ModifiesState()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryUserRepository>();
			_ = services.AddSingleton<ICommandHandler<CreateUserCommand>, CreateUserCommandHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var repository = host.Services.GetRequiredService<InMemoryUserRepository>();
		var handler = host.Services.GetRequiredService<ICommandHandler<CreateUserCommand>>();

		// Act
		var command = new CreateUserCommand(Guid.NewGuid(), "john.doe", "john@example.com");
		await handler.HandleAsync(command, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		var user = repository.GetById(command.UserId);
		_ = user.ShouldNotBeNull();
		user.Username.ShouldBe("john.doe");
		user.Email.ShouldBe("john@example.com");
	}

	[Fact]
	public async Task Command_ValidatesInput()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryUserRepository>();
			_ = services.AddSingleton<ICommandHandler<CreateUserCommand>, ValidatingCreateUserCommandHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<ICommandHandler<CreateUserCommand>>();

		// Act - Empty username should fail validation
		var command = new CreateUserCommand(Guid.NewGuid(), "", "invalid@example.com");

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => handler.HandleAsync(command, TestCancellationToken)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Command_SupportsMultipleCommands()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryUserRepository>();
			_ = services.AddSingleton<ICommandHandler<CreateUserCommand>, CreateUserCommandHandler>();
			_ = services.AddSingleton<ICommandHandler<UpdateUserEmailCommand>, UpdateUserEmailCommandHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var repository = host.Services.GetRequiredService<InMemoryUserRepository>();
		var createHandler = host.Services.GetRequiredService<ICommandHandler<CreateUserCommand>>();
		var updateHandler = host.Services.GetRequiredService<ICommandHandler<UpdateUserEmailCommand>>();

		// Act - Create then update
		var userId = Guid.NewGuid();
		await createHandler.HandleAsync(new CreateUserCommand(userId, "jane.doe", "jane@old.com"), TestCancellationToken).ConfigureAwait(false);
		await updateHandler.HandleAsync(new UpdateUserEmailCommand(userId, "jane@new.com"), TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		var user = repository.GetById(userId);
		_ = user.ShouldNotBeNull();
		user.Email.ShouldBe("jane@new.com");
	}

	#endregion

	#region Query Tests

	[Fact]
	public async Task Query_ReturnsData()
	{
		// Arrange
		var repository = new InMemoryUserRepository();
		repository.Add(new User(Guid.NewGuid(), "test.user", "test@example.com"));

		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton(repository);
			_ = services.AddSingleton<IQueryHandler<GetUserByUsernameQuery, User?>, GetUserByUsernameQueryHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IQueryHandler<GetUserByUsernameQuery, User?>>();

		// Act
		var query = new GetUserByUsernameQuery("test.user");
		var result = await handler.HandleAsync(query, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Email.ShouldBe("test@example.com");
	}

	[Fact]
	public async Task Query_ReturnsNullForMissingData()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryUserRepository>();
			_ = services.AddSingleton<IQueryHandler<GetUserByUsernameQuery, User?>, GetUserByUsernameQueryHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IQueryHandler<GetUserByUsernameQuery, User?>>();

		// Act
		var query = new GetUserByUsernameQuery("nonexistent");
		var result = await handler.HandleAsync(query, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task Query_CanReturnCollections()
	{
		// Arrange
		var repository = new InMemoryUserRepository();
		repository.Add(new User(Guid.NewGuid(), "alice", "alice@example.com"));
		repository.Add(new User(Guid.NewGuid(), "bob", "bob@example.com"));
		repository.Add(new User(Guid.NewGuid(), "charlie", "charlie@example.com"));

		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton(repository);
			_ = services.AddSingleton<IQueryHandler<GetAllUsersQuery, IReadOnlyList<User>>, GetAllUsersQueryHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IQueryHandler<GetAllUsersQuery, IReadOnlyList<User>>>();

		// Act
		var result = await handler.HandleAsync(new GetAllUsersQuery(), TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Count.ShouldBe(3);
	}

	#endregion

	#region Event Tests

	[Fact]
	public async Task Event_PropagatesChanges()
	{
		// Arrange
		var eventReceived = new TaskCompletionSource<bool>();
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton(eventReceived);
			_ = services.AddSingleton<IEventHandler<UserCreatedEvent>, UserCreatedEventHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IEventHandler<UserCreatedEvent>>();

		// Act
		var evt = new UserCreatedEvent(Guid.NewGuid(), "new.user", "new@example.com");
		await handler.HandleAsync(evt, TestCancellationToken).ConfigureAwait(false);

		var received = await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		received.ShouldBeTrue();
	}

	[Fact]
	public async Task Event_CanHaveMultipleHandlers()
	{
		// Arrange
		var handler1Received = new TaskCompletionSource<bool>();
		var handler2Received = new TaskCompletionSource<bool>();

		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<IEventHandler<UserCreatedEvent>>(new MultiEventHandler1(handler1Received));
			_ = services.AddSingleton<IEventHandler<UserCreatedEvent>>(new MultiEventHandler2(handler2Received));
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handlers = host.Services.GetServices<IEventHandler<UserCreatedEvent>>().ToList();

		// Act - Publish to all handlers
		var evt = new UserCreatedEvent(Guid.NewGuid(), "multi.user", "multi@example.com");
		foreach (var handler in handlers)
		{
			await handler.HandleAsync(evt, TestCancellationToken).ConfigureAwait(false);
		}

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		handler1Received.Task.IsCompletedSuccessfully.ShouldBeTrue();
		handler2Received.Task.IsCompletedSuccessfully.ShouldBeTrue();
	}

	#endregion

	#region Full CQRS Flow Test

	[Fact]
	public async Task CqrsFlow_CommandQueryEventIntegration()
	{
		// Arrange
		var eventHandled = new TaskCompletionSource<bool>();
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InMemoryUserRepository>();
			_ = services.AddSingleton(eventHandled);
			_ = services.AddSingleton<ICommandHandler<CreateUserCommand>, CreateUserCommandHandler>();
			_ = services.AddSingleton<IQueryHandler<GetUserByUsernameQuery, User?>, GetUserByUsernameQueryHandler>();
			_ = services.AddSingleton<IEventHandler<UserCreatedEvent>, UserCreatedEventHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var commandHandler = host.Services.GetRequiredService<ICommandHandler<CreateUserCommand>>();
		var queryHandler = host.Services.GetRequiredService<IQueryHandler<GetUserByUsernameQuery, User?>>();
		var eventHandler = host.Services.GetRequiredService<IEventHandler<UserCreatedEvent>>();

		// Act - Create user via command
		var userId = Guid.NewGuid();
		var createCommand = new CreateUserCommand(userId, "cqrs.user", "cqrs@example.com");
		await commandHandler.HandleAsync(createCommand, TestCancellationToken).ConfigureAwait(false);

		// Query for the user
		var query = new GetUserByUsernameQuery("cqrs.user");
		var user = await queryHandler.HandleAsync(query, TestCancellationToken).ConfigureAwait(false);

		// Dispatch event
		var evt = new UserCreatedEvent(userId, "cqrs.user", "cqrs@example.com");
		await eventHandler.HandleAsync(evt, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = user.ShouldNotBeNull();
		user.Username.ShouldBe("cqrs.user");
		eventHandled.Task.IsCompletedSuccessfully.ShouldBeTrue();
	}

	#endregion

	#region Test Interfaces

	private interface ICommandHandler<in TCommand>
	{
		Task HandleAsync(TCommand command, CancellationToken cancellationToken);
	}

	private interface IQueryHandler<in TQuery, TResult>
	{
		Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
	}

	private interface IEventHandler<in TEvent>
	{
		Task HandleAsync(TEvent evt, CancellationToken cancellationToken);
	}

	#endregion

	#region Test Messages

	private sealed record CreateUserCommand(Guid UserId, string Username, string Email);

	private sealed record UpdateUserEmailCommand(Guid UserId, string NewEmail);

	private sealed record GetUserByUsernameQuery(string Username);

	private sealed record GetAllUsersQuery;

	private sealed record UserCreatedEvent(Guid UserId, string Username, string Email);

	private sealed record User(Guid Id, string Username, string Email);

	#endregion

	#region Test Implementations

	private sealed class InMemoryUserRepository
	{
		private readonly Dictionary<Guid, User> _users = new();

		public void Add(User user) => _users[user.Id] = user;
		public User? GetById(Guid id) => _users.GetValueOrDefault(id);
		public User? GetByUsername(string username) => _users.Values.FirstOrDefault(u => u.Username == username);
		public IReadOnlyList<User> GetAll() => _users.Values.ToList();
		public void Update(User user) => _users[user.Id] = user;
	}

	private sealed class CreateUserCommandHandler(InMemoryUserRepository repository) : ICommandHandler<CreateUserCommand>
	{
		public Task HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
		{
			repository.Add(new User(command.UserId, command.Username, command.Email));
			return Task.CompletedTask;
		}
	}

	private sealed class ValidatingCreateUserCommandHandler(InMemoryUserRepository repository) : ICommandHandler<CreateUserCommand>
	{
		public Task HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(command.Username))
			{
				throw new ArgumentException("Username is required", nameof(command));
			}

			repository.Add(new User(command.UserId, command.Username, command.Email));
			return Task.CompletedTask;
		}
	}

	private sealed class UpdateUserEmailCommandHandler(InMemoryUserRepository repository) : ICommandHandler<UpdateUserEmailCommand>
	{
		public Task HandleAsync(UpdateUserEmailCommand command, CancellationToken cancellationToken)
		{
			var user = repository.GetById(command.UserId)
				?? throw new InvalidOperationException($"User {command.UserId} not found");
			repository.Update(user with { Email = command.NewEmail });
			return Task.CompletedTask;
		}
	}

	private sealed class GetUserByUsernameQueryHandler(InMemoryUserRepository repository) : IQueryHandler<GetUserByUsernameQuery, User?>
	{
		public Task<User?> HandleAsync(GetUserByUsernameQuery query, CancellationToken cancellationToken)
		{
			return Task.FromResult(repository.GetByUsername(query.Username));
		}
	}

	private sealed class GetAllUsersQueryHandler(InMemoryUserRepository repository) : IQueryHandler<GetAllUsersQuery, IReadOnlyList<User>>
	{
		public Task<IReadOnlyList<User>> HandleAsync(GetAllUsersQuery query, CancellationToken cancellationToken)
		{
			return Task.FromResult(repository.GetAll());
		}
	}

	private sealed class UserCreatedEventHandler(TaskCompletionSource<bool> eventReceived) : IEventHandler<UserCreatedEvent>
	{
		public Task HandleAsync(UserCreatedEvent evt, CancellationToken cancellationToken)
		{
			_ = eventReceived.TrySetResult(true);
			return Task.CompletedTask;
		}
	}

	private sealed class MultiEventHandler1(TaskCompletionSource<bool> received) : IEventHandler<UserCreatedEvent>
	{
		public Task HandleAsync(UserCreatedEvent evt, CancellationToken cancellationToken)
		{
			_ = received.TrySetResult(true);
			return Task.CompletedTask;
		}
	}

	private sealed class MultiEventHandler2(TaskCompletionSource<bool> received) : IEventHandler<UserCreatedEvent>
	{
		public Task HandleAsync(UserCreatedEvent evt, CancellationToken cancellationToken)
		{
			_ = received.TrySetResult(true);
			return Task.CompletedTask;
		}
	}

	#endregion
}
