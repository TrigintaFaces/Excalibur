// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Trait("Category", "Unit")]
public sealed class HandlerRegistryShould
{
    // Test types
    private interface ITestCommand : IDispatchMessage;
    private interface ITestEvent : IDispatchEvent;

    private sealed class TestCommand : ITestCommand
    {
        public string MessageId => "cmd-1";
        public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
        public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>(StringComparer.Ordinal);
        public object Body => new();
        public string MessageType => nameof(TestCommand);
        public IMessageFeatures Features => new DefaultMessageFeatures();
        public Guid Id => Guid.NewGuid();
        public MessageKinds Kind => MessageKinds.Action;
    }

    private sealed class TestEvent : ITestEvent
    {
        public string MessageId => "evt-1";
        public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
        public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>(StringComparer.Ordinal);
        public object Body => new();
        public string MessageType => nameof(TestEvent);
        public IMessageFeatures Features => new DefaultMessageFeatures();
        public Guid Id => Guid.NewGuid();
        public MessageKinds Kind => MessageKinds.Event;
    }

    private sealed class TestCommandHandler;
    private sealed class TestCommandHandler2;
    private sealed class TestEventHandler1;
    private sealed class TestEventHandler2;

    [Fact]
    public void RegisterAndRetrieveHandler()
    {
        var registry = new HandlerRegistry();
        registry.Register(typeof(TestCommand), typeof(TestCommandHandler), expectsResponse: false);

        var found = registry.TryGetHandler(typeof(TestCommand), out var entry);

        found.ShouldBeTrue();
        entry.HandlerType.ShouldBe(typeof(TestCommandHandler));
        entry.ExpectsResponse.ShouldBeFalse();
    }

    [Fact]
    public void ReturnFalseForUnregisteredType()
    {
        var registry = new HandlerRegistry();

        var found = registry.TryGetHandler(typeof(TestCommand), out _);

        found.ShouldBeFalse();
    }

    [Fact]
    public void ReplaceHandlerForCommands()
    {
        var registry = new HandlerRegistry();
        registry.Register(typeof(TestCommand), typeof(TestCommandHandler), expectsResponse: false);
        registry.Register(typeof(TestCommand), typeof(TestCommandHandler2), expectsResponse: true);

        var found = registry.TryGetHandler(typeof(TestCommand), out var entry);

        found.ShouldBeTrue();
        entry.HandlerType.ShouldBe(typeof(TestCommandHandler2));
        entry.ExpectsResponse.ShouldBeTrue();
    }

    [Fact]
    public void AllowMultipleHandlersForEvents()
    {
        var registry = new HandlerRegistry();
        registry.Register(typeof(TestEvent), typeof(TestEventHandler1), expectsResponse: false);
        registry.Register(typeof(TestEvent), typeof(TestEventHandler2), expectsResponse: false);

        var allEntries = registry.GetAll();

        allEntries.Count(e => e.MessageType == typeof(TestEvent)).ShouldBe(2);
    }

    [Fact]
    public void AvoidDuplicateEventHandlerRegistrations()
    {
        var registry = new HandlerRegistry();
        registry.Register(typeof(TestEvent), typeof(TestEventHandler1), expectsResponse: false);
        registry.Register(typeof(TestEvent), typeof(TestEventHandler1), expectsResponse: false);

        var allEntries = registry.GetAll();

        allEntries.Count(e => e.MessageType == typeof(TestEvent) && e.HandlerType == typeof(TestEventHandler1))
            .ShouldBe(1);
    }

    [Fact]
    public void GetAllReturnsAllRegistrations()
    {
        var registry = new HandlerRegistry();
        registry.Register(typeof(TestCommand), typeof(TestCommandHandler), expectsResponse: false);
        registry.Register(typeof(TestEvent), typeof(TestEventHandler1), expectsResponse: false);

        var allEntries = registry.GetAll();

        allEntries.Count.ShouldBe(2);
    }

    [Fact]
    public void TryGetHandlerReturnsFirstForEvents()
    {
        var registry = new HandlerRegistry();
        registry.Register(typeof(TestEvent), typeof(TestEventHandler1), expectsResponse: false);
        registry.Register(typeof(TestEvent), typeof(TestEventHandler2), expectsResponse: false);

        var found = registry.TryGetHandler(typeof(TestEvent), out var entry);

        found.ShouldBeTrue();
        entry.HandlerType.ShouldBe(typeof(TestEventHandler1));
    }

    [Fact]
    public void GetAllReturnsEmptyForNewRegistry()
    {
        var registry = new HandlerRegistry();

        var allEntries = registry.GetAll();

        allEntries.ShouldBeEmpty();
    }

    [Fact]
    public void HandleConcurrentRegistrations()
    {
        var registry = new HandlerRegistry();

        Parallel.For(0, 50, i =>
        {
            // Alternating event handler registrations
            var handlerType = i % 2 == 0 ? typeof(TestEventHandler1) : typeof(TestEventHandler2);
            registry.Register(typeof(TestEvent), handlerType, expectsResponse: false);
        });

        // Should have exactly 2 unique event handlers despite concurrent registration
        var eventEntries = registry.GetAll()
            .Where(e => e.MessageType == typeof(TestEvent))
            .ToList();

        eventEntries.Count.ShouldBe(2);
    }
}
