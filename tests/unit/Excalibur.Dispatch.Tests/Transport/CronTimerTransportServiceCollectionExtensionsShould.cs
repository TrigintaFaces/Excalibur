// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Transport;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="CronTimerTransportServiceCollectionExtensions"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class CronTimerTransportServiceCollectionExtensionsShould
{
    private const string ValidCron = "*/5 * * * *";

    #region Generic Overload -- Happy Path

    [Fact]
    public void AddCronTimerTransport_Generic_RegisterKeyedSingletonWithTimerTypeName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddCronTimerTransport<TestTimer>(ValidCron);

        // Assert
        services.Any(d =>
            d.ServiceType == typeof(CronTimerTransportAdapter) &&
            d.ServiceKey is string key && key == nameof(TestTimer)).ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Generic_RegisterCronSchedulerIdempotently()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act -- call twice
        _ = services.AddCronTimerTransport<TestTimer>(ValidCron);
        _ = services.AddCronTimerTransport<AnotherTimer>("0 * * * *");

        // Assert -- only one ICronScheduler registration
        services.Count(d => d.ServiceType == typeof(ICronScheduler)).ShouldBe(1);
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ApplyConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        // Act
        _ = services.AddCronTimerTransport<TestTimer>(ValidCron, opts =>
        {
            opts.TimeZone = tz;
            opts.RunOnStartup = true;
            opts.PreventOverlap = false;
        });

        // Assert -- registration exists (configure was invoked without error)
        services.Any(d =>
            d.ServiceType == typeof(CronTimerTransportAdapter) &&
            d.ServiceKey is string key && key == nameof(TestTimer)).ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Generic_RegisterTransportInRegistry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddCronTimerTransport<TestTimer>(ValidCron);

        // Assert -- TransportRegistry should be registered
        services.Any(d => d.ServiceType == typeof(ITransportRegistry)).ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Generic_RegisterTransportAdapterLifecycle()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddCronTimerTransport<TestTimer>(ValidCron);

        // Assert -- registered via AddHostedService as IHostedService
        services.Any(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(TransportAdapterHostedService)).ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ReturnServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddCronTimerTransport<TestTimer>(ValidCron);

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddCronTimerTransport_Generic_AcceptNullConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- should not throw
        Should.NotThrow(() =>
            services.AddCronTimerTransport<TestTimer>(ValidCron, configure: null));
    }

    #endregion

    #region Named Overload -- Happy Path

    [Fact]
    public void AddCronTimerTransport_Named_RegisterKeyedSingletonWithGivenName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddCronTimerTransport("health-check", ValidCron);

        // Assert
        services.Any(d =>
            d.ServiceType == typeof(CronTimerTransportAdapter) &&
            d.ServiceKey is string key && key == "health-check").ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Named_RegisterCronSchedulerIdempotently()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act -- call twice with different names
        _ = services.AddCronTimerTransport("timer-a", ValidCron);
        _ = services.AddCronTimerTransport("timer-b", "0 * * * *");

        // Assert -- only one ICronScheduler registration
        services.Count(d => d.ServiceType == typeof(ICronScheduler)).ShouldBe(1);
    }

    [Fact]
    public void AddCronTimerTransport_Named_ApplyConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddCronTimerTransport("daily-cleanup", ValidCron, opts =>
        {
            opts.RunOnStartup = true;
        });

        // Assert -- registration exists
        services.Any(d =>
            d.ServiceType == typeof(CronTimerTransportAdapter) &&
            d.ServiceKey is string key && key == "daily-cleanup").ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Named_RegisterTransportInRegistry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddCronTimerTransport("my-timer", ValidCron);

        // Assert
        services.Any(d => d.ServiceType == typeof(ITransportRegistry)).ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Named_RegisterTransportAdapterLifecycle()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddCronTimerTransport("my-timer", ValidCron);

        // Assert -- registered via AddHostedService as IHostedService
        services.Any(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(TransportAdapterHostedService)).ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Named_ReturnServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddCronTimerTransport("my-timer", ValidCron);

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddCronTimerTransport_Named_AcceptNullConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- should not throw
        Should.NotThrow(() =>
            services.AddCronTimerTransport("my-timer", ValidCron, configure: null));
    }

    #endregion

    #region Generic Overload -- Failure Path

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowWhenServicesIsNull()
    {
        // Act & Assert
        _ = Should.Throw<ArgumentNullException>(() =>
            CronTimerTransportServiceCollectionExtensions.AddCronTimerTransport<TestTimer>(null!, ValidCron));
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowWhenCronExpressionIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport<TestTimer>(null!));
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowWhenCronExpressionIsEmpty()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport<TestTimer>(""));
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowWhenCronExpressionIsWhitespace()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport<TestTimer>("   "));
    }

    #endregion

    #region Named Overload -- Failure Path

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenServicesIsNull()
    {
        // Act & Assert
        _ = Should.Throw<ArgumentNullException>(() =>
            CronTimerTransportServiceCollectionExtensions.AddCronTimerTransport(null!, "timer", ValidCron));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenNameIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport(null!, ValidCron));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenNameIsEmpty()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("", ValidCron));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenNameIsWhitespace()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("   ", ValidCron));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenCronExpressionIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("timer", null!));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenCronExpressionIsEmpty()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("timer", ""));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenCronExpressionIsWhitespace()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        _ = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("timer", "   "));
    }

    #endregion

    #region Cron Expression Format Validation

    [Fact]
    public void AddCronTimerTransport_Generic_AcceptFiveFieldExpression()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- should not throw (5-field minute-level)
        Should.NotThrow(() =>
            services.AddCronTimerTransport<TestTimer>("0 0 * * *"));
    }

    [Fact]
    public void AddCronTimerTransport_Generic_AcceptSixFieldExpression()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- should not throw (6-field second-level)
        Should.NotThrow(() =>
            services.AddCronTimerTransport<TestTimer>("*/30 * * * * *"));
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowWhenExpressionHasThreeFields()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- 3 fields is invalid
        var ex = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport<TestTimer>("* * *"));

        ex.Message.ShouldContain("3");
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowWhenExpressionHasSevenFields()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- 7 fields is invalid
        var ex = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport<TestTimer>("* * * * * * *"));

        ex.Message.ShouldContain("7");
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenExpressionHasThreeFields()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("timer", "* * *"));

        ex.Message.ShouldContain("3");
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenExpressionHasSevenFields()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("timer", "* * * * * * *"));

        ex.Message.ShouldContain("7");
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowWhenExpressionHasSingleField()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport<TestTimer>("not-a-cron"));

        ex.Message.ShouldContain("1");
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenExpressionHasFourFields()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("timer", "* * * *"));

        ex.Message.ShouldContain("4");
    }

    #endregion

    #region Semantic Validation (Service Resolution)

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowOnResolveWhenExpressionSemanticInvalid()
    {
        // Arrange -- 5 fields but semantically invalid values
        var services = new ServiceCollection();
        _ = services.AddCronTimerTransport<TestTimer>("99 99 99 99 99");

        // Add required dependencies so the factory can run
        services.AddLogging();

        // Fake ICronScheduler that rejects the expression
        var fakeScheduler = A.Fake<ICronScheduler>();
        ICronExpression? outExpr;
        A.CallTo(() => fakeScheduler.TryParse(
            A<string>.That.Matches(s => s == "99 99 99 99 99"),
            A<TimeZoneInfo>._,
            out outExpr)).Returns(false);

        // Replace the TryAddSingleton registration with our fake
        services.AddSingleton<ICronScheduler>(fakeScheduler);

        var sp = services.BuildServiceProvider();

        // Act & Assert -- keyed service resolution should throw InvalidOperationException
        _ = Should.Throw<InvalidOperationException>(() =>
            sp.GetRequiredKeyedService<CronTimerTransportAdapter>(nameof(TestTimer)));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowOnResolveWhenExpressionSemanticInvalid()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddCronTimerTransport("bad-timer", "99 99 99 99 99");

        services.AddLogging();

        var fakeScheduler = A.Fake<ICronScheduler>();
        ICronExpression? outExpr;
        A.CallTo(() => fakeScheduler.TryParse(
            A<string>.That.Matches(s => s == "99 99 99 99 99"),
            A<TimeZoneInfo>._,
            out outExpr)).Returns(false);

        services.AddSingleton<ICronScheduler>(fakeScheduler);

        var sp = services.BuildServiceProvider();

        // Act & Assert
        _ = Should.Throw<InvalidOperationException>(() =>
            sp.GetRequiredKeyedService<CronTimerTransportAdapter>("bad-timer"));
    }

    #endregion

    #region Multiple Transports

    [Fact]
    public void AddCronTimerTransport_SupportMultipleGenericTransports()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services
            .AddCronTimerTransport<TestTimer>(ValidCron)
            .AddCronTimerTransport<AnotherTimer>("0 * * * *");

        // Assert -- both keyed registrations
        services.Count(d =>
            d.ServiceType == typeof(CronTimerTransportAdapter) &&
            d.ServiceKey is string).ShouldBe(2);
    }

    [Fact]
    public void AddCronTimerTransport_SupportMixedGenericAndNamedTransports()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services
            .AddCronTimerTransport<TestTimer>(ValidCron)
            .AddCronTimerTransport("named-timer", "0 * * * *");

        // Assert
        services.Count(d =>
            d.ServiceType == typeof(CronTimerTransportAdapter) &&
            d.ServiceKey is string).ShouldBe(2);
    }

    #endregion

    #region Double Registration (Same Key)

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowOnDuplicateRegistrationWithSameType()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddCronTimerTransport<TestTimer>(ValidCron);

        // Act & Assert -- TransportRegistry rejects duplicate names
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddCronTimerTransport<TestTimer>("0 * * * *"));

        ex.Message.ShouldContain(nameof(TestTimer));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowOnDuplicateRegistrationWithSameName()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddCronTimerTransport("my-timer", ValidCron);

        // Act & Assert -- TransportRegistry rejects duplicate names
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddCronTimerTransport("my-timer", "0 * * * *"));

        ex.Message.ShouldContain("my-timer");
    }

    #endregion

    #region Configure Delegate Value Propagation

    [Fact]
    public void AddCronTimerTransport_Generic_PropagateOptionsToResolvedAdapter()
    {
        // Arrange
        var services = new ServiceCollection();
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        _ = services.AddCronTimerTransport<TestTimer>(ValidCron, opts =>
        {
            opts.TimeZone = tz;
            opts.RunOnStartup = true;
            opts.PreventOverlap = false;
        });

        services.AddLogging();

        // Fake ICronScheduler that accepts the expression
        var fakeScheduler = A.Fake<ICronScheduler>();
        var fakeExpression = A.Fake<ICronExpression>();
        ICronExpression? outExpr = fakeExpression;
        A.CallTo(() => fakeScheduler.TryParse(A<string>._, A<TimeZoneInfo>._, out outExpr))
            .Returns(true);
        services.AddSingleton<ICronScheduler>(fakeScheduler);

        var sp = services.BuildServiceProvider();

        // Act
        var adapter = sp.GetRequiredKeyedService<CronTimerTransportAdapter>(nameof(TestTimer));

        // Assert -- Name is derived from the marker type
        adapter.Name.ShouldBe(nameof(TestTimer));
    }

    [Fact]
    public void AddCronTimerTransport_Named_PropagateNameToResolvedAdapter()
    {
        // Arrange
        var services = new ServiceCollection();

        _ = services.AddCronTimerTransport("custom-name", ValidCron);

        services.AddLogging();

        var fakeScheduler = A.Fake<ICronScheduler>();
        ICronExpression? outExpr = A.Fake<ICronExpression>();
        A.CallTo(() => fakeScheduler.TryParse(A<string>._, A<TimeZoneInfo>._, out outExpr))
            .Returns(true);
        services.AddSingleton<ICronScheduler>(fakeScheduler);

        var sp = services.BuildServiceProvider();

        // Act
        var adapter = sp.GetRequiredKeyedService<CronTimerTransportAdapter>("custom-name");

        // Assert
        adapter.Name.ShouldBe("custom-name");
    }

    [Fact]
    public void AddCronTimerTransport_Generic_InvokeConfigureDelegate()
    {
        // Arrange
        var services = new ServiceCollection();
        var invoked = false;

        // Act
        _ = services.AddCronTimerTransport<TestTimer>(ValidCron, opts =>
        {
            invoked = true;
        });

        // Assert
        invoked.ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Named_InvokeConfigureDelegate()
    {
        // Arrange
        var services = new ServiceCollection();
        var invoked = false;

        // Act
        _ = services.AddCronTimerTransport("my-timer", ValidCron, opts =>
        {
            invoked = true;
        });

        // Assert
        invoked.ShouldBeTrue();
    }

    [Fact]
    public void AddCronTimerTransport_Generic_UseDefaultOptionsWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        _ = services.AddCronTimerTransport<TestTimer>(ValidCron, configure: null);

        services.AddLogging();

        var fakeScheduler = A.Fake<ICronScheduler>();
        ICronExpression? outExpr = A.Fake<ICronExpression>();
        A.CallTo(() => fakeScheduler.TryParse(A<string>._, A<TimeZoneInfo>._, out outExpr))
            .Returns(true);
        services.AddSingleton<ICronScheduler>(fakeScheduler);

        var sp = services.BuildServiceProvider();

        // Act -- should resolve without error (defaults applied)
        var adapter = sp.GetRequiredKeyedService<CronTimerTransportAdapter>(nameof(TestTimer));

        // Assert
        adapter.ShouldNotBeNull();
        adapter.Name.ShouldBe(nameof(TestTimer));
    }

    #endregion

    #region Cron Expression Whitespace Edge Cases

    [Fact]
    public void AddCronTimerTransport_Generic_AcceptExpressionWithExtraInternalSpaces()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- extra spaces between fields still produces 5 tokens with RemoveEmptyEntries
        Should.NotThrow(() =>
            services.AddCronTimerTransport<TestTimer>("*/5  *   *  *  *"));
    }

    [Fact]
    public void AddCronTimerTransport_Named_AcceptExpressionWithExtraInternalSpaces()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.NotThrow(() =>
            services.AddCronTimerTransport("timer", "*/5  *   *  *  *"));
    }

    [Fact]
    public void AddCronTimerTransport_Generic_AcceptExpressionWithLeadingAndTrailingSpaces()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- leading/trailing spaces are trimmed by Split RemoveEmptyEntries
        Should.NotThrow(() =>
            services.AddCronTimerTransport<TestTimer>("  */5 * * * *  "));
    }

    [Fact]
    public void AddCronTimerTransport_Named_AcceptExpressionWithLeadingAndTrailingSpaces()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.NotThrow(() =>
            services.AddCronTimerTransport("timer", "  */5 * * * *  "));
    }

    #endregion

    #region Named Overload -- Cron Format Parity

    [Fact]
    public void AddCronTimerTransport_Named_AcceptSixFieldExpression()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert -- 6-field second-level
        Should.NotThrow(() =>
            services.AddCronTimerTransport("timer", "*/30 * * * * *"));
    }

    [Fact]
    public void AddCronTimerTransport_Named_ThrowWhenExpressionHasSingleField()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport("timer", "not-a-cron"));

        ex.Message.ShouldContain("1");
    }

    [Fact]
    public void AddCronTimerTransport_Generic_ThrowWhenExpressionHasFourFields()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<ArgumentException>(() =>
            services.AddCronTimerTransport<TestTimer>("* * * *"));

        ex.Message.ShouldContain("4");
    }

    #endregion

    #region Marker Types

    internal struct TestTimer : ICronTimerMarker;
    internal struct AnotherTimer : ICronTimerMarker;

    #endregion
}