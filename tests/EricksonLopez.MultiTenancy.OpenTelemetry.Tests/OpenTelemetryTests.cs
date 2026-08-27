// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.OpenTelemetry;
using EricksonLopez.MultiTenancy.Testing;
using EricksonLopez.Result;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public sealed class OpenTelemetryTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly TenantId ExpectedTenantId = new(ExpectedGuid);

    [Fact]
    public void ActivitySource_ConstantsAndProperties_ShouldBeCorrect()
    {
        TenantActivitySource.ActivitySourceName.Should().Be("EricksonLopez.MultiTenancy");
        TenantActivitySource.Source.Name.Should().Be("EricksonLopez.MultiTenancy");
        TenantActivitySource.Source.Version.Should().Be("1.0.0");

        TenantActivitySource.Tags.TenantId.Should().Be("tenant.id");
        TenantActivitySource.Tags.TenantName.Should().Be("tenant.name");
        TenantActivitySource.Tags.TenantSource.Should().Be("tenant.source");
        TenantActivitySource.Tags.TenantIsActive.Should().Be("tenant.is_active");
        TenantActivitySource.Tags.ResolutionStrategy.Should().Be("tenant.resolution_strategy");

        TenantActivitySource.Baggage.TenantId.Should().Be("tenant.id");
    }

    [Fact]
    public void EnrichWithTenant_NullActivity_ReturnsNull()
    {
        Activity? activity = null;
        var tenantContext = new TenantContextBuilder().BuildContext();

        var result = activity.EnrichWithTenant(tenantContext);

        result.Should().BeNull();
    }

    [Fact]
    public void EnrichWithTenant_NullTenantContext_ReturnsUnmodifiedActivity()
    {
        using var activity = new Activity("TestSpan").Start();

        var result = activity.EnrichWithTenant(null);

        result.Should().BeSameAs(activity);
        activity.Tags.Should().BeEmpty();
    }

    [Fact]
    public void EnrichWithTenant_UnresolvedContext_ReturnsUnmodifiedActivity()
    {
        using var activity = new Activity("TestSpan").Start();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(false);

        var result = activity.EnrichWithTenant(tenantContext);

        result.Should().BeSameAs(activity);
        activity.Tags.Should().BeEmpty();
    }

    [Fact]
    public void EnrichWithTenant_ResolvedContextWithNullTenant_ReturnsUnmodifiedActivity()
    {
        using var activity = new Activity("TestSpan").Start();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.Tenant.Returns((ITenantInfo?)null);

        var result = activity.EnrichWithTenant(tenantContext);

        result.Should().BeSameAs(activity);
        activity.Tags.Should().BeEmpty();
    }

    [Fact]
    public void EnrichWithTenant_ResolvedContext_SetsAllExpectedTags()
    {
        var tenantInfo = new TenantInfo(ExpectedTenantId, "OTel Tenant", isActive: true);
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.Tenant.Returns(tenantInfo);
        tenantContext.Source.Returns(TenantResolutionSource.JwtClaim);

        using var activity = new Activity("TestSpan").Start();

        var result = activity.EnrichWithTenant(tenantContext);

        result.Should().BeSameAs(activity);
        activity.GetTagItem(TenantActivitySource.Tags.TenantId).Should().Be(ExpectedTenantId.ToString());
        activity.GetTagItem(TenantActivitySource.Tags.TenantName).Should().Be("OTel Tenant");
        activity.GetTagItem(TenantActivitySource.Tags.TenantSource).Should().Be(TenantResolutionSource.JwtClaim.ToString());
        activity.GetTagItem(TenantActivitySource.Tags.TenantIsActive).Should().Be(true);
    }

    [Fact]
    public void EnrichCurrentActivity_WhenAmbientActivityExists_EnrichesAmbientActivity()
    {
        var tenantContext = new TenantContextBuilder()
            .WithName("Ambient Tenant")
            .WithSource(TenantResolutionSource.Host)
            .BuildContext();

        using var activity = new Activity("AmbientSpan").Start();
        Activity.Current.Should().BeSameAs(activity);

        TenantActivityExtensions.EnrichCurrentActivity(tenantContext);

        activity.GetTagItem(TenantActivitySource.Tags.TenantName).Should().Be("Ambient Tenant");
        activity.GetTagItem(TenantActivitySource.Tags.TenantSource).Should().Be(TenantResolutionSource.Host.ToString());
    }

    [Fact]
    public void EnrichCurrentActivity_WhenNoAmbientActivity_DoesNotThrow()
    {
        Activity.Current = null;
        var tenantContext = new TenantContextBuilder().BuildContext();

        var act = () => TenantActivityExtensions.EnrichCurrentActivity(tenantContext);
        act.Should().NotThrow();
    }

    [Fact]
    public void SetTenantBaggage_NullActivity_ReturnsNull()
    {
        Activity? activity = null;
        var result = activity.SetTenantBaggage(ExpectedTenantId);
        result.Should().BeNull();
    }

    [Fact]
    public void SetTenantBaggage_EmptyTenantId_ReturnsUnmodifiedActivityWithoutBaggage()
    {
        using var activity = new Activity("BaggageSpan").Start();

        var result = activity.SetTenantBaggage(TenantId.Empty);

        result.Should().BeSameAs(activity);
        activity.Baggage.Should().BeEmpty();
    }

    [Fact]
    public void SetTenantBaggage_ValidTenantId_AttachesW3CBaggage()
    {
        using var activity = new Activity("BaggageSpan").Start();

        var result = activity.SetTenantBaggage(ExpectedTenantId);

        result.Should().BeSameAs(activity);
        activity.GetBaggageItem(TenantActivitySource.Baggage.TenantId).Should().Be(ExpectedTenantId.ToString());
    }

    [Fact]
    public void RecordTenantResolutionFailure_NullActivity_ReturnsNull()
    {
        Activity? activity = null;
        var error = Error.Validation("Tenant.InvalidFormat", "The tenant identifier format is invalid.");

        var result = activity.RecordTenantResolutionFailure(error, "RouteStrategy");

        result.Should().BeNull();
    }

    [Fact]
    public void RecordTenantResolutionFailure_WithStrategyName_AddsEventWithStrategyTag()
    {
        using var activity = new Activity("FailureSpan").Start();
        var error = Error.Validation("Tenant.InvalidFormat", "The tenant identifier format is invalid.");

        var result = activity.RecordTenantResolutionFailure(error, "RouteStrategy");

        result.Should().BeSameAs(activity);
        activity.Events.Should().ContainSingle(e => e.Name == "tenant.resolution_failed");

        var ev = activity.Events.First();
        var tagDict = new Dictionary<string, object?>();
        foreach (var tag in ev.Tags)
        {
            tagDict[tag.Key] = tag.Value;
        }

        tagDict["error.type"].Should().Be("Tenant.InvalidFormat");
        tagDict["error.message"].Should().Be("The tenant identifier format is invalid.");
        tagDict[TenantActivitySource.Tags.ResolutionStrategy].Should().Be("RouteStrategy");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RecordTenantResolutionFailure_WithoutStrategyName_AddsEventWithoutStrategyTag(string? strategyName)
    {
        using var activity = new Activity("FailureSpan").Start();
        var error = Error.NotFound("Tenant.NotFound", "Tenant was not found.");

        var result = activity.RecordTenantResolutionFailure(error, strategyName);

        result.Should().BeSameAs(activity);
        activity.Events.Should().ContainSingle(e => e.Name == "tenant.resolution_failed");

        var ev = activity.Events.First();
        var tagDict = new Dictionary<string, object?>();
        foreach (var tag in ev.Tags)
        {
            tagDict[tag.Key] = tag.Value;
        }

        tagDict["error.type"].Should().Be("Tenant.NotFound");
        tagDict["error.message"].Should().Be("Tenant was not found.");
        tagDict.ContainsKey(TenantActivitySource.Tags.ResolutionStrategy).Should().BeFalse();
    }

    [Fact]
    public void TenantTraceEnricher_NullContext_ThrowsArgumentNullException()
    {
        var enricher = new TenantTraceEnricher();

        var act = () => enricher.Enrich(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void TenantTraceEnricher_ValidContext_EnrichesAmbientActivity()
    {
        var enricher = new TenantTraceEnricher();
        var tenantContext = new TenantContextBuilder()
            .WithName("Enricher Tenant")
            .WithSource(TenantResolutionSource.Route)
            .BuildContext();

        using var activity = new Activity("EnricherSpan").Start();

        enricher.Enrich(tenantContext);

        activity.GetTagItem(TenantActivitySource.Tags.TenantName).Should().Be("Enricher Tenant");
        activity.GetTagItem(TenantActivitySource.Tags.TenantSource).Should().Be(TenantResolutionSource.Route.ToString());
    }

    [Fact]
    public void AddMultiTenancyOpenTelemetry_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var act = () => services.AddMultiTenancyOpenTelemetry();

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddMultiTenancyOpenTelemetry_ValidServices_RegistersSingletonEnricher()
    {
        var services = new ServiceCollection();
        var returnedServices = services.AddMultiTenancyOpenTelemetry();

        returnedServices.Should().BeSameAs(services);

        var provider = services.BuildServiceProvider();
        var enricher1 = provider.GetService<ITenantTraceEnricher>();
        var enricher2 = provider.GetService<ITenantTraceEnricher>();

        enricher1.Should().NotBeNull();
        enricher1.Should().BeOfType<TenantTraceEnricher>();
        enricher2.Should().BeSameAs(enricher1);
    }

    [Fact]
    public void TenantMetrics_Meter_Properties_ShouldBeCorrect()
    {
        TenantMetrics.MeterName.Should().Be("EricksonLopez.MultiTenancy");
        TenantMetrics.MeterVersion.Should().Be("1.0.0");
        TenantMetrics.Meter.Name.Should().Be("EricksonLopez.MultiTenancy");
        TenantMetrics.Meter.Version.Should().Be("1.0.0");

        TenantMetrics.ResolutionTotal.Name.Should().Be("tenant.resolution.total");
        TenantMetrics.ResolutionTotal.Unit.Should().Be("{resolutions}");
        TenantMetrics.ResolutionTotal.Description.Should().Be("Total number of tenant resolution attempts.");

        TenantMetrics.ResolutionFailures.Name.Should().Be("tenant.resolution.failures");
        TenantMetrics.ResolutionFailures.Unit.Should().Be("{failures}");
        TenantMetrics.ResolutionFailures.Description.Should().Be("Total number of tenant resolution failures.");

        TenantMetrics.ResolutionConflicts.Name.Should().Be("tenant.resolution.conflicts");
        TenantMetrics.ResolutionConflicts.Unit.Should().Be("{conflicts}");
        TenantMetrics.ResolutionConflicts.Description.Should().Be("Total number of resolution conflicts detected across strategies.");

        TenantMetrics.ResolutionDuration.Name.Should().Be("tenant.resolution.duration");
        TenantMetrics.ResolutionDuration.Unit.Should().Be("ms");
        TenantMetrics.ResolutionDuration.Description.Should().Be("Duration of tenant resolution in milliseconds.");
    }

    [Fact]
    public void TenantMetrics_RecordResolutionSuccess_WithAndWithoutDuration_EmitsCorrectMetricsAndTags()
    {
        var measurements = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        var durationMeasurements = new List<(string Name, double Value, Dictionary<string, object?> Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == TenantMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            measurements.Add((instrument.Name, measurement, tagDict));
        });

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            durationMeasurements.Add((instrument.Name, measurement, tagDict));
        });

        listener.Start();

        // 1. Without duration (durationMs = 0)
        TenantMetrics.RecordResolutionSuccess("ClaimStrategy", TenantResolutionSource.JwtClaim, 0);

        measurements.Should().ContainSingle(m => m.Name == "tenant.resolution.total" && m.Value == 1);
        var totalTags = measurements.First().Tags;
        totalTags["tenant.strategy"].Should().Be("ClaimStrategy");
        totalTags["tenant.source"].Should().Be("JwtClaim");
        totalTags["status"].Should().Be("success");

        durationMeasurements.Should().BeEmpty();

        // 2. With positive duration (durationMs = 42.5)
        measurements.Clear();
        durationMeasurements.Clear();

        TenantMetrics.RecordResolutionSuccess("HostStrategy", TenantResolutionSource.Host, 42.5);

        measurements.Should().ContainSingle(m => m.Name == "tenant.resolution.total" && m.Value == 1);
        durationMeasurements.Should().ContainSingle(m => m.Name == "tenant.resolution.duration" && m.Value == 42.5);

        var durationTags = durationMeasurements.First().Tags;
        durationTags["tenant.strategy"].Should().Be("HostStrategy");
        durationTags["tenant.source"].Should().Be("Host");
        durationTags["status"].Should().Be("success");
    }

    [Fact]
    public void TenantMetrics_RecordResolutionFailure_WithAndWithoutDuration_EmitsCorrectMetricsAndTags()
    {
        var measurements = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        var durationMeasurements = new List<(string Name, double Value, Dictionary<string, object?> Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == TenantMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            measurements.Add((instrument.Name, measurement, tagDict));
        });

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            durationMeasurements.Add((instrument.Name, measurement, tagDict));
        });

        listener.Start();

        // 1. Without duration (durationMs = 0)
        TenantMetrics.RecordResolutionFailure("RouteStrategy", "Tenant.NotFound", 0);

        measurements.Should().HaveCount(2);
        measurements.Should().ContainSingle(m => m.Name == "tenant.resolution.total" && m.Value == 1);
        measurements.Should().ContainSingle(m => m.Name == "tenant.resolution.failures" && m.Value == 1);

        var totalTags = measurements.First(m => m.Name == "tenant.resolution.total").Tags;
        totalTags["tenant.strategy"].Should().Be("RouteStrategy");
        totalTags["error.code"].Should().Be("Tenant.NotFound");
        totalTags["status"].Should().Be("failure");

        durationMeasurements.Should().BeEmpty();

        // 2. With positive duration (durationMs = 18.2)
        measurements.Clear();
        durationMeasurements.Clear();

        TenantMetrics.RecordResolutionFailure("HeaderStrategy", "Tenant.InvalidHeader", 18.2);

        measurements.Should().HaveCount(2);
        durationMeasurements.Should().ContainSingle(m => m.Name == "tenant.resolution.duration" && m.Value == 18.2);

        var durationTags = durationMeasurements.First().Tags;
        durationTags["tenant.strategy"].Should().Be("HeaderStrategy");
        durationTags["error.code"].Should().Be("Tenant.InvalidHeader");
        durationTags["status"].Should().Be("failure");
    }

    [Fact]
    public void TenantMetrics_RecordResolutionConflict_EmitsConflictCounterWithTags()
    {
        var measurements = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == TenantMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            measurements.Add((instrument.Name, measurement, tagDict));
        });

        listener.Start();

        TenantMetrics.RecordResolutionConflict("ClaimStrategy", "HeaderStrategy");

        measurements.Should().ContainSingle(m => m.Name == "tenant.resolution.conflicts" && m.Value == 1);

        var tags = measurements.First().Tags;
        tags["tenant.strategy.first"].Should().Be("ClaimStrategy");
        tags["tenant.strategy.second"].Should().Be("HeaderStrategy");
    }
}
