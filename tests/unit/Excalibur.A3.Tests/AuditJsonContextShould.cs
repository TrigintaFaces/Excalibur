// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.A3.Audit;

namespace Excalibur.Tests;

/// <summary>
/// Round-trip serialization tests for <see cref="RaisedBy"/> and <see cref="Dictionary{TKey, TValue}"/>
/// via <see cref="AuditJsonContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Serialization")]
public sealed class AuditJsonContextShould
{
    [Fact]
    public void InheritFromJsonSerializerContext()
    {
        var context = AuditJsonContext.Default;
        context.ShouldBeAssignableTo<JsonSerializerContext>();
    }

    [Fact]
    public void ProvideDefaultInstance()
    {
        AuditJsonContext.Default.ShouldNotBeNull();
    }

    [Fact]
    public void HaveRaisedByTypeInfo()
    {
        AuditJsonContext.Default
            .GetTypeInfo(typeof(RaisedBy))
            .ShouldNotBeNull();
    }

    [Fact]
    public void HaveDictionaryStringStringTypeInfo()
    {
        AuditJsonContext.Default
            .GetTypeInfo(typeof(Dictionary<string, string>))
            .ShouldNotBeNull();
    }

    [Fact]
    public void RoundTripRaisedByWithFullNameAndLogin()
    {
        var original = new RaisedBy("John Doe", "jdoe");

        var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.RaisedBy);
        var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.RaisedBy);

        deserialized.ShouldNotBeNull();
        deserialized.FullName.ShouldBe("John Doe");
        deserialized.Login.ShouldBe("jdoe");
    }

    [Fact]
    public void RoundTripRaisedByWithAllProperties()
    {
        var original = new RaisedBy
        {
            FullName = "Jane Smith",
            Login = "jsmith",
            FirstName = "Jane",
            LastName = "Smith",
            UserId = "user-123",
        };

        var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.RaisedBy);
        var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.RaisedBy);

        deserialized.ShouldNotBeNull();
        deserialized.FullName.ShouldBe("Jane Smith");
        deserialized.Login.ShouldBe("jsmith");
        deserialized.FirstName.ShouldBe("Jane");
        deserialized.LastName.ShouldBe("Smith");
        deserialized.UserId.ShouldBe("user-123");
    }

    [Fact]
    public void RoundTripRaisedByWithNullProperties()
    {
        var original = new RaisedBy();

        var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.RaisedBy);
        var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.RaisedBy);

        deserialized.ShouldNotBeNull();
        deserialized.FullName.ShouldBeNull();
        deserialized.Login.ShouldBeNull();
        deserialized.FirstName.ShouldBeNull();
        deserialized.LastName.ShouldBeNull();
        deserialized.UserId.ShouldBeNull();
    }

    [Fact]
    public void RoundTripDictionaryStringString()
    {
        var original = new Dictionary<string, string>
        {
            ["action"] = "UserLogin",
            ["resource"] = "/api/users",
            ["ip"] = "192.168.1.1",
        };

        var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.DictionaryStringString);
        var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.DictionaryStringString);

        deserialized.ShouldNotBeNull();
        deserialized.Count.ShouldBe(3);
        deserialized["action"].ShouldBe("UserLogin");
        deserialized["resource"].ShouldBe("/api/users");
        deserialized["ip"].ShouldBe("192.168.1.1");
    }

    [Fact]
    public void RoundTripEmptyDictionary()
    {
        var original = new Dictionary<string, string>();

        var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.DictionaryStringString);
        var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.DictionaryStringString);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeEmpty();
    }

    [Fact]
    public void NotWriteIndented()
    {
        var options = AuditJsonContext.Default.Options;
        options.WriteIndented.ShouldBeFalse();
    }

    [Fact]
    public void ProduceBackwardCompatibleRaisedByJsonShape()
    {
        // RaisedBy was previously serialized with default System.Text.Json (PascalCase).
        // Verify the new context preserves property names.
        var raisedBy = new RaisedBy("Test User", "tuser")
        {
            FirstName = "Test",
            LastName = "User",
            UserId = "uid-1",
        };

        var json = JsonSerializer.Serialize(raisedBy, AuditJsonContext.Default.RaisedBy);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("FullName", out _).ShouldBeTrue();
        root.TryGetProperty("Login", out _).ShouldBeTrue();
        root.TryGetProperty("FirstName", out _).ShouldBeTrue();
        root.TryGetProperty("LastName", out _).ShouldBeTrue();
        root.TryGetProperty("UserId", out _).ShouldBeTrue();
    }
}
