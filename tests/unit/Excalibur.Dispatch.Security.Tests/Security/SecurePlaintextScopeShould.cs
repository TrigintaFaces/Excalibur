// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security;

using Excalibur.Security.Internal;

namespace Excalibur.Dispatch.Security.Tests.Security;

/// <summary>
/// bd-rxi674 (S859) — independent engage-test (author≠impl) for <see cref="SecurePlaintextScope"/>.
/// </summary>
/// <remarks>
/// The prior <c>Array.Fill(value.ToCharArray(), '\0')</c> anti-pattern zeroed a throwaway copy while the
/// real plaintext stayed live in memory — a fake scrub. <see cref="SecurePlaintextScope.UseAsync{TResult}"/>
/// replaces it with a pinned buffer that is genuinely cleared in a <see langword="finally"/> block once the
/// caller's delegate returns. This lock makes the regression structurally observable: it captures the very
/// buffer handed to the delegate and asserts every element is zeroed AFTER the scope exits. It is
/// non-vacuous — RED against any impl that drops the <c>Array.Clear</c> (i.e. the throwaway-copy fake), and
/// it independently verifies the scope actually exposes the correct decoded plaintext while live.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
[Trait("Feature", "Cryptography")]
public sealed class SecurePlaintextScopeShould
{
    private static SecureString MakeSecret(string value)
    {
        var secret = new SecureString();
        foreach (var c in value)
        {
            secret.AppendChar(c);
        }

        secret.MakeReadOnly();
        return secret;
    }

    [Fact]
    public async Task ZeroTheBufferAfterTheScopeExits()
    {
        // Arrange
        using var secret = MakeSecret("hunter2-correct-horse");
        char[]? captured = null;

        // Act — capture the exact buffer the scope exposes to the delegate.
        await SecurePlaintextScope.UseAsync(
            secret,
            (buffer, _) =>
            {
                captured = buffer;
                return Task.FromResult(0);
            },
            CancellationToken.None);

        // Assert — the buffer the delegate saw must be genuinely scrubbed once the scope returns.
        // RED on the throwaway-copy fake (which never clears the real buffer).
        captured.ShouldNotBeNull();
        captured.ShouldAllBe(c => c == '\0');
    }

    [Fact]
    public async Task ExposeTheCorrectDecodedPlaintextWhileInScope()
    {
        // Arrange
        const string Plaintext = "s3cr3t-value-éñ";
        using var secret = MakeSecret(Plaintext);

        // Act — snapshot the plaintext INSIDE the scope (before it is scrubbed).
        var observed = await SecurePlaintextScope.UseAsync(
            secret,
            (buffer, _) => Task.FromResult(new string(buffer)),
            CancellationToken.None);

        // Assert — the scope marshals the SecureString into the buffer faithfully (length + content),
        // so the scrub is not achieved by simply never populating it.
        observed.ShouldBe(Plaintext);
        observed.Length.ShouldBe(secret.Length);
    }

    [Fact]
    public async Task PropagateTheDelegateResult()
    {
        // Arrange
        using var secret = MakeSecret("anything");

        // Act
        var result = await SecurePlaintextScope.UseAsync(
            secret,
            (_, _) => Task.FromResult(42),
            CancellationToken.None);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public async Task ScrubTheBufferEvenWhenTheDelegateThrows()
    {
        // Arrange
        using var secret = MakeSecret("must-scrub-on-failure");
        char[]? captured = null;

        // Act / Assert — a failing delegate must not leave plaintext resident; the finally-block scrub
        // is the load-bearing guarantee on the failure path (where a leaked secret matters most).
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await SecurePlaintextScope.UseAsync<int>(
                secret,
                (buffer, _) =>
                {
                    captured = buffer;
                    throw new InvalidOperationException("boom");
                },
                CancellationToken.None));

        captured.ShouldNotBeNull();
        captured.ShouldAllBe(c => c == '\0');
    }

    [Fact]
    public async Task ThrowWhenSecretIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await SecurePlaintextScope.UseAsync<int>(
                secret: null!,
                (_, _) => Task.FromResult(0),
                CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenUseDelegateIsNull()
    {
        using var secret = MakeSecret("x");

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await SecurePlaintextScope.UseAsync<int>(
                secret,
                use: null!,
                CancellationToken.None));
    }
}
