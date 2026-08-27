// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using BenchmarkDotNet.Attributes;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Testing;

namespace EricksonLopez.MultiTenancy.Benchmarks;

/// <summary>
/// Competitive benchmarks comparing EricksonLopez.MultiTenancy Native AOT-first,
/// zero-allocation struct-based scoped tenant resolution against conventional
/// reflection-heavy, dynamic dictionary, and AsyncLocal ambient lookup patterns.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CompetitiveResolutionBenchmarks
{
    // EricksonLopez setup
    private Guid _guid;
    private string _guidString = null!;
    private TenantId _typedTenantId;
    private ITenantContext _resolvedContext = null!;
    private ScopedTenantContextAccessor _scopedAccessor = null!;

    // Legacy / Reflection / Ambient dictionary setup
    private static readonly AsyncLocal<Dictionary<string, object>> _ambientContext = new();
    private static readonly ConcurrentDictionary<string, object> _globalTenantCache = new();
    private static readonly Regex _subdomainRegex = new(@"^(?<tenant>[a-zA-Z0-9-]+)\.app\.domain\.com$", RegexOptions.Compiled);
    private string _hostHeader = null!;

    [GlobalSetup]
    public void Setup()
    {
        _guid = Guid.NewGuid();
        _guidString = _guid.ToString();
        _typedTenantId = TenantId.Create(_guid);
        _hostHeader = "alpha-corp.app.domain.com";

        _resolvedContext = new TenantContextBuilder()
            .WithId(_guid)
            .WithName("alpha-corp")
            .WithProperty("Tier", "Enterprise")
            .BuildContext();

        _scopedAccessor = new ScopedTenantContextAccessor
        {
            TenantContext = _resolvedContext
        };

        // Ambient dictionary setup
        var dict = new Dictionary<string, object>
        {
            ["TenantId"] = _guidString,
            ["TenantName"] = "alpha-corp",
            ["Resolved"] = true
        };
        _ambientContext.Value = dict;
        _globalTenantCache.TryAdd("alpha-corp", dict);
    }

    // ─── 1. EricksonLopez Zero-Allocation Resolution ─────────────────────────
    [Benchmark(Baseline = true)]
    public TenantId EricksonLopez_StronglyTypedId_Create()
    {
        return TenantId.Create(_guid);
    }

    [Benchmark]
    public ITenantContext? EricksonLopez_ScopedAccessor_GetContext()
    {
        return _scopedAccessor.TenantContext;
    }

    [Benchmark]
    public bool EricksonLopez_TenantId_StructEquality()
    {
        return _typedTenantId == TenantId.Create(_guid);
    }

    // ─── 2. Conventional Dynamic / Ambient Patterns ─────────────────────────
    [Benchmark]
    public object? Conventional_AmbientAsyncLocal_DictionaryLookup()
    {
        var dict = _ambientContext.Value;
        return dict != null && dict.TryGetValue("TenantId", out var val) ? val : null;
    }

    [Benchmark]
    public object? Conventional_ConcurrentDictionary_CacheLookup()
    {
        return _globalTenantCache.TryGetValue("alpha-corp", out var val) ? val : null;
    }

    [Benchmark]
    public string? Conventional_Regex_SubdomainExtraction()
    {
        var match = _subdomainRegex.Match(_hostHeader);
        return match.Success ? match.Groups["tenant"].Value : null;
    }

    [Benchmark]
    public string Conventional_String_Boxing_And_Concatenation()
    {
        return string.Format("tenant_{0}_{1}", _guid.ToString(), "alpha-corp");
    }
}
