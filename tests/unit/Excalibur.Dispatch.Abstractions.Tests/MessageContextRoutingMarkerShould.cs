// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Collections.ObjectModel;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Abstractions.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Pattern", "Contract")]
public sealed class MessageContextRoutingMarkerShould
{
    private const string MarkerKey = "__MessageTypeIsRoutingDefault";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AvoidAllocationsForRepeatedMarksOnDerivedContexts(bool customItems)
    {
        IMessageContext context = customItems ? new CustomItemsContext() : new InheritedContext();
        for (var index = 0; index < 1000; index++)
        {
            context.MarkMessageTypeAsRoutingDefault();
        }

        _ = GC.GetAllocatedBytesForCurrentThread();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            context.MarkMessageTypeAsRoutingDefault();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(context);
        allocated.ShouldBe(0);
        context.Items[MarkerKey].ShouldBeOfType<bool>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HonorDirectMarkerMutationsAndKeepUnrelatedItems(bool customItems)
    {
        IMessageContext context = customItems ? new CustomItemsContext() : new InheritedContext();
        context.Items["unrelated"] = "preserve";
        context.MarkMessageTypeAsRoutingDefault();
        context.IsMessageTypeRoutingDefault().ShouldBeTrue();
        context.Items[MarkerKey].ShouldBeOfType<bool>().ShouldBeTrue();

        context.Items[MarkerKey] = false;
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();
        context.Items[MarkerKey] = "true";
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();
        context.Items.Remove(MarkerKey).ShouldBeTrue();
        context.IsMessageTypeRoutingDefault().ShouldBeFalse();
        context.MarkMessageTypeAsRoutingDefault();
        context.Items[MarkerKey].ShouldBeOfType<bool>().ShouldBeTrue();
        context.Items["unrelated"].ShouldBe("preserve");
    }

    [Fact]
    public void ConsultTheInterfaceGetterAndDictionarySetterForEveryFallbackMark()
    {
        var context = new CustomItemsContext();
        IMessageContext contract = context;
        for (var index = 0; index < 1000; index++)
        {
            contract.MarkMessageTypeAsRoutingDefault();
        }

        context.ItemsGetterCalls.ShouldBe(1000);
        context.Storage.SetterCalls.ShouldBe(1000);
        context.Storage.LastSetKey.ShouldBe(MarkerKey);
        context.Storage.LastSetValue.ShouldBeOfType<bool>().ShouldBeTrue();
        context.Items.ShouldBeEmpty(); // The base dictionary is not the reimplemented interface dictionary.
    }

    [Fact]
    public void PreserveTheItemsGetterException()
    {
        var expected = new InvalidOperationException("getter failed");
        IMessageContext context = new CustomItemsContext { GetterFailure = expected };

        Should.Throw<InvalidOperationException>(() => context.MarkMessageTypeAsRoutingDefault()).ShouldBeSameAs(expected);
    }

    [Fact]
    public void PreserveTheDictionarySetterException()
    {
        var expected = new InvalidOperationException("setter failed");
        var context = new CustomItemsContext();
        context.Storage.SetterFailure = expected;

        Should.Throw<InvalidOperationException>(() => ((IMessageContext)context).MarkMessageTypeAsRoutingDefault()).ShouldBeSameAs(expected);
        context.ItemsGetterCalls.ShouldBe(1);
        context.Storage.SetterCalls.ShouldBe(1);
    }

    [Fact]
    public void PreserveReadOnlyDictionaryFailure()
    {
        var storage = new RecordingDictionary(new ReadOnlyDictionary<string, object>(new Dictionary<string, object>()));
        var context = new CustomItemsContext(storage);

        Should.Throw<NotSupportedException>(() => ((IMessageContext)context).MarkMessageTypeAsRoutingDefault());
        context.ItemsGetterCalls.ShouldBe(1);
        storage.SetterCalls.ShouldBe(1);
        storage.ShouldBeEmpty();
    }

    [Fact]
    public void NameTheNullContextParameter()
    {
        IMessageContext context = null!;

        Should.Throw<ArgumentNullException>(() => context.MarkMessageTypeAsRoutingDefault()).ParamName.ShouldBe("context");
    }

    private sealed class InheritedContext : MessageContext;

    private sealed class CustomItemsContext : MessageContext, IMessageContext
    {
        internal CustomItemsContext()
            : this(new RecordingDictionary(new Dictionary<string, object>(StringComparer.Ordinal)))
        {
        }

        internal CustomItemsContext(RecordingDictionary storage) => Storage = storage;

        internal RecordingDictionary Storage { get; }
        internal int ItemsGetterCalls { get; private set; }
        internal Exception? GetterFailure { get; init; }

        IDictionary<string, object> IMessageContext.Items
        {
            get
            {
                ItemsGetterCalls++;
                if (GetterFailure is not null)
                {
                    throw GetterFailure;
                }

                return Storage;
            }
        }
    }

    private sealed class RecordingDictionary(IDictionary<string, object> inner) : IDictionary<string, object>
    {
        internal int SetterCalls { get; private set; }
        internal string? LastSetKey { get; private set; }
        internal object? LastSetValue { get; private set; }
        internal Exception? SetterFailure { get; set; }

        public object this[string key]
        {
            get => inner[key];
            set
            {
                SetterCalls++;
                LastSetKey = key;
                LastSetValue = value;
                if (SetterFailure is not null)
                {
                    throw SetterFailure;
                }

                inner[key] = value;
            }
        }

        public ICollection<string> Keys => inner.Keys;
        public ICollection<object> Values => inner.Values;
        public int Count => inner.Count;
        public bool IsReadOnly => inner.IsReadOnly;
        public void Add(string key, object value) => inner.Add(key, value);
        public bool ContainsKey(string key) => inner.ContainsKey(key);
        public bool Remove(string key) => inner.Remove(key);
        public bool TryGetValue(string key, out object value) => inner.TryGetValue(key, out value!);
        public void Add(KeyValuePair<string, object> item) => inner.Add(item);
        public void Clear() => inner.Clear();
        public bool Contains(KeyValuePair<string, object> item) => inner.Contains(item);
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => inner.CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<string, object> item) => inner.Remove(item);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
