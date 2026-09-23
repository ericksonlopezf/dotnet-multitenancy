// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.MultiTenancy.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace EricksonLopez.MultiTenancy.UnitTests;

public class AnalyzersTests
{
    private sealed class TestAnalysisContext : AnalysisContext
    {
        public GeneratedCodeAnalysisFlags GeneratedCodeFlags { get; private set; } = GeneratedCodeAnalysisFlags.Analyze;
        public bool ConcurrentExecutionEnabled { get; private set; }
        public bool SymbolActionRegistered { get; private set; }
        public bool SyntaxNodeActionRegistered { get; private set; }

        public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags analysisFlags)
        {
            GeneratedCodeFlags = analysisFlags;
        }

        public override void EnableConcurrentExecution()
        {
            ConcurrentExecutionEnabled = true;
        }

        public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, ImmutableArray<SymbolKind> symbolKinds)
        {
            SymbolActionRegistered = true;
        }

        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(Action<SyntaxNodeAnalysisContext> action, ImmutableArray<TLanguageKindEnum> syntaxKinds)
        {
            SyntaxNodeActionRegistered = true;
        }

        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) { }
        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) { }
        public override void RegisterCompilationStartAction(Action<CompilationStartAnalysisContext> action) { }
        public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action) { }
        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) { }
        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action) { }
        public override void RegisterOperationAction(Action<OperationAnalysisContext> action, ImmutableArray<OperationKind> operationKinds) { }
        public override void RegisterOperationBlockAction(Action<OperationBlockAnalysisContext> action) { }
        public override void RegisterOperationBlockStartAction(Action<OperationBlockStartAnalysisContext> action) { }
        public override void RegisterSymbolStartAction(Action<SymbolStartAnalysisContext> action, SymbolKind symbolKind) { }
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        bool includeReferences = true,
        bool isScript = false)
    {
        var parseOptions = isScript ? CSharpParseOptions.Default.WithKind(SourceCodeKind.Script) : CSharpParseOptions.Default;
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

#pragma warning disable IL3000
        var coreAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = includeReferences
            ? new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(coreAssemblyPath, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(System.Data.IDbConnection).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Data.Common.DbConnection).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ITenantContext).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TenantId).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(global::Dapper.SqlMapper).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(EricksonLopez.MultiTenancy.Dapper.TenantDapperExtensions).Assembly.Location),
            }
            : Array.Empty<MetadataReference>();
#pragma warning restore IL3000

        var compilationOptions = isScript
            ? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithScriptClassName("Script")
            : new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            compilationOptions);

        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ─────────────────────────────────────────────────────────
    // ELMT001: TenantContextStaticFieldAnalyzer
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void ELMT001_DescriptorAndInitialization_AreConfiguredCorrectly()
    {
        var analyzer = new TenantContextStaticFieldAnalyzer();
        analyzer.SupportedDiagnostics.Should().ContainSingle();
        var rule = analyzer.SupportedDiagnostics[0];

        rule.Id.Should().Be("ELMT001");
        rule.Title.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("Do not store tenant context in static fields");
        rule.MessageFormat.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("Field '{0}' is static and stores tenant context type '{1}', which can cause cross-tenant context leakage");
        rule.Description.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("ITenantContext and ITenantContextAccessor are scoped abstractions. Storing them in static fields creates concurrency leaks across tenants.");
        rule.Category.Should().Be("Safety");
        rule.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        rule.IsEnabledByDefault.Should().BeTrue();

        var context = new TestAnalysisContext();
        analyzer.Initialize(context);
        context.GeneratedCodeFlags.Should().Be(GeneratedCodeAnalysisFlags.None);
        context.ConcurrentExecutionEnabled.Should().BeTrue();
        context.SymbolActionRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task ELMT001_StaticTenantContextField_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class OrderService
{
    private static ITenantContext _staticContext;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextStaticFieldAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT001");
        diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Contain("_staticContext").And.Contain("ITenantContext");
    }

    [Fact]
    public async Task ELMT001_StaticTenantContextAccessorField_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class PaymentService
{
    private static ITenantContextAccessor _accessor;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextStaticFieldAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT001");
        diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Contain("_accessor").And.Contain("ITenantContextAccessor");
    }

    [Fact]
    public async Task ELMT001_StaticConcreteTenantContextField_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class CustomTenantContext : ITenantContext
{
    public ITenantInfo? Tenant => null;
    public ITenantInfo RequiredTenant => null!;
    public bool HasResolvedTenant => false;
}

public class OrderService
{
    private static CustomTenantContext _customContext;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextStaticFieldAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT001");
    }

    [Fact]
    public async Task ELMT001_InstanceTenantContextField_NoDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class OrderService
{
    private readonly ITenantContext _tenantContext;
    public OrderService(ITenantContext tenantContext) => _tenantContext = tenantContext;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextStaticFieldAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT001_StaticNonTenantField_NoDiagnostic()
    {
        var source = @"
public class OrderService
{
    private static readonly string GlobalPrefix = ""ORDER_"";
    private static int _counter = 0;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextStaticFieldAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT001_StaticTenantInfoField_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class OrderService
{
    private static ITenantInfo _staticTenant;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextStaticFieldAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT001");
        diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Contain("_staticTenant").And.Contain("ITenantInfo");
    }

    [Fact]
    public async Task ELMT001_StaticTenantIdField_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class OrderService
{
    private static TenantId _staticTenantId;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextStaticFieldAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT001");
        diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Contain("_staticTenantId").And.Contain("TenantId");
    }

    // ─────────────────────────────────────────────────────────
    // ELMT002: TenantContextInSingletonAnalyzer
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void ELMT002_DescriptorAndInitialization_AreConfiguredCorrectly()
    {
        var analyzer = new TenantContextInSingletonAnalyzer();
        analyzer.SupportedDiagnostics.Should().ContainSingle();
        var rule = analyzer.SupportedDiagnostics[0];

        rule.Id.Should().Be("ELMT002");
        rule.Title.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("Do not reference scoped tenant context in singleton classes");
        rule.MessageFormat.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("Type '{0}' appears to be a singleton or cache and references scoped tenant context '{1}', which causes captive dependency leaks");
        rule.Description.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("ITenantContext and ITenantContextAccessor are scoped abstractions. Using them as fields or constructor parameters in singleton classes causes captive dependencies across requests.");
        rule.Category.Should().Be("Safety");
        rule.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        rule.IsEnabledByDefault.Should().BeTrue();

        var context = new TestAnalysisContext();
        analyzer.Initialize(context);
        context.GeneratedCodeFlags.Should().Be(GeneratedCodeAnalysisFlags.None);
        context.ConcurrentExecutionEnabled.Should().BeTrue();
        context.SymbolActionRegistered.Should().BeTrue();
    }

    [Theory]
    [InlineData("UserCache")]
    [InlineData("CustomerSingleton")]
    [InlineData("CatalogMemoryStore")]
    [InlineData("OrderHostedService")]
    [InlineData("PaymentBackgroundService")]
    [InlineData("EmailWorker")]
    public async Task ELMT002_SingletonClassWithTenantContextField_EmitsDiagnostic(string className)
    {
        var source = $@"
using EricksonLopez.MultiTenancy;

public class {className}
{{
    private readonly ITenantContext _tenantContext;
    public {className}(ITenantContext tenantContext) => _tenantContext = tenantContext;
}}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT002");
        diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Contain(className).And.Contain("ITenantContext");
    }

    [Fact]
    public async Task ELMT002_SingletonClassWithTenantContextAccessor_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class ProductCache
{
    private readonly ITenantContextAccessor _accessor;
    public ProductCache(ITenantContextAccessor accessor) => _accessor = accessor;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT002");
    }

    [Fact]
    public async Task ELMT002_SingletonClassWithConcreteTenantContext_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class AppTenantContext : ITenantContext
{
    public ITenantInfo? Tenant => null;
    public ITenantInfo RequiredTenant => null!;
    public bool HasResolvedTenant => false;
}

public class OrderCache
{
    private readonly AppTenantContext _tenantContext;
    public OrderCache(AppTenantContext tenantContext) => _tenantContext = tenantContext;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT002");
    }

    [Fact]
    public async Task ELMT002_SingletonClassWithTenantContextProperty_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class OrderCache
{
    public ITenantContext TenantContext { get; }
    public OrderCache(ITenantContext tenantContext) => TenantContext = tenantContext;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT002");
        diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Contain("OrderCache").And.Contain("ITenantContext");
    }

    [Fact]
    public async Task ELMT002_StaticSingletonClass_NoDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public static class GlobalCache
{
    private static ITenantContext? _context;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT002_ScopedService_NoDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class OrderRepository
{
    private readonly ITenantContext _tenantContext;
    public OrderRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT002_AllowedBuildersAndTestContexts_NoDiagnostic()
    {
        var source = @"
public class TenantContextBuilderCache
{
    private readonly object _builder;
}

public class TestTenantContextCache
{
    private readonly object _testContext;
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────
    // ELMT003: DapperWithoutTenantAnalyzer
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void ELMT003_DescriptorAndInitialization_AreConfiguredCorrectly()
    {
        var analyzer = new DapperWithoutTenantAnalyzer();
        analyzer.SupportedDiagnostics.Should().ContainSingle();
        var rule = analyzer.SupportedDiagnostics[0];

        rule.Id.Should().Be("ELMT003");
        rule.Title.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("Dapper query executed without tenant parameter in tenant-aware context");
        rule.MessageFormat.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("Dapper invocation '{0}' in tenant-aware context '{1}' does not provide tenant parameters via WithTenant() or CreateTenantParameters()");
        rule.Description.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("In multi-tenant services and repositories with an active ITenantContext, executing Dapper queries without explicit tenant parameters risks cross-tenant data exposure.");
        rule.Category.Should().Be("Security");
        rule.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        rule.IsEnabledByDefault.Should().BeTrue();

        var context = new TestAnalysisContext();
        analyzer.Initialize(context);
        context.GeneratedCodeFlags.Should().Be(GeneratedCodeAnalysisFlags.None);
        context.ConcurrentExecutionEnabled.Should().BeTrue();
        context.SyntaxNodeActionRegistered.Should().BeTrue();
    }

    [Theory]
    [InlineData("Query")]
    [InlineData("QueryAsync")]
    [InlineData("QueryFirst")]
    [InlineData("QueryFirstAsync")]
    [InlineData("QueryFirstOrDefault")]
    [InlineData("QueryFirstOrDefaultAsync")]
    [InlineData("QuerySingle")]
    [InlineData("QuerySingleAsync")]
    [InlineData("QueryMultiple")]
    [InlineData("QueryMultipleAsync")]
    [InlineData("Execute")]
    [InlineData("ExecuteAsync")]
    [InlineData("ExecuteScalar")]
    [InlineData("ExecuteScalarAsync")]
    [InlineData("ExecuteReader")]
    [InlineData("ExecuteReaderAsync")]
    public async Task ELMT003_AllDapperMethods_WithoutTenantParam_EmitDiagnostic(string dapperMethod)
    {
        var source = $@"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task ExecuteQueryAsync(IDbConnection connection)
    {{
        await connection.{dapperMethod}(""SELECT * FROM invoices"");
    }}
}}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
        diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture).Should().Contain(dapperMethod).And.Contain("InvoiceRepository");
    }

    [Fact]
    public async Task ELMT003_DbConnectionBaseClass_WithoutTenantParam_EmitsDiagnostic()
    {
        var source = @"
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class DbConnRepo
{
    private readonly ITenantContext _tenantContext;
    public DbConnRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task RunAsync(DbConnection dbConnection)
    {
        await dbConnection.QueryAsync(""SELECT * FROM invoices"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Theory]
    [InlineData("conn")]
    [InlineData("db")]
    [InlineData("connection")]
    [InlineData("dbConnection")]
    public async Task ELMT003_FallbackIdentifierHeuristic_EmitsDiagnostic(string varName)
    {
        var source = $@"
using EricksonLopez.MultiTenancy;

public class DynamicRepo
{{
    private readonly ITenantContext _tenantContext;
    public DynamicRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public void Run(dynamic {varName})
    {{
        {varName}.Query(""SELECT * FROM invoices"");
    }}
}}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_UnrelatedReceiverName_NoDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class UnrelatedRepo
{
    private readonly ITenantContext _tenantContext;
    public UnrelatedRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public void Run(dynamic searcher)
    {
        searcher.Query(""SELECT * FROM invoices"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepositoryViaProperty_QueryWithoutParameters_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class PropertyTenantRepo
{
    public ITenantContext Context { get; set; } = null!;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepositoryViaConstructorParam_QueryWithoutParameters_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class CtorTenantRepo
{
    public CtorTenantRepo(ITenantContext context) {}

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TenantAwareMethodViaParameter_QueryWithoutParameters_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class HelperRepo
{
    public async Task GetInvoicesAsync(IDbConnection connection, ITenantContext tenantContext)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithWithTenantExtension_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @TenantId"", new { Status = ""Active"" }.WithTenant(_tenantContext));
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithCreateTenantParameters_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @TenantId"", _tenantContext.CreateTenantParameters(new { Status = ""Active"" }));
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithAnonymousObjectTenantId_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @TenantId"", new { TenantId = _tenantContext.RequiredTenant.Id });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithAnonymousObjectSnakeCaseTenantId_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @tenant_id"", new { tenant_id = 123 });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithAnonymousObjectExpressionContainingTenantId_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        var myTenantId = _tenantContext.RequiredTenant.Id;
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @id"", new { id = myTenantId });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithAnonymousObjectExpressionContainingTenantContext_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @id"", new { id = _tenantContext.Tenant?.Id });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithParametersMissingTenantId_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection, int id)
    {
        await connection.QueryAsync(""SELECT * FROM invoices WHERE id = @id"", new { Id = id });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithNullLiteralParam_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"", param: null);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithNamedParameters_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"", parameters: new { TenantId = 1 });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithNamedParam_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"", param: new { TenantId = 1 });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithConfiguredVariableInMethod_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.Dapper;

public class InvoiceRepository
{
    private readonly ITenantContext _tenantContext;
    public InvoiceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        var dp = new DynamicParameters();
        dp.WithTenant(_tenantContext);
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @TenantId"", dp);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithVariableAddTenantId_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class DynamicParamRepo
{
    private readonly ITenantContext _tenantContext;
    public DynamicParamRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        var dp = new DynamicParameters();
        dp.Add(""TenantId"", 123);
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @TenantId"", dp);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithVariableAddSnakeCaseTenantId_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class SnakeCaseRepo
{
    private readonly ITenantContext _tenantContext;
    public SnakeCaseRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        var dp = new DynamicParameters();
        dp.Add(""tenant_id"", 123);
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @tenant_id"", dp);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithVariableAddOtherField_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class OtherFieldRepo
{
    private readonly ITenantContext _tenantContext;
    public OtherFieldRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        var dp = new DynamicParameters();
        dp.Add(""OtherField"", 123);
        await connection.QueryAsync(""SELECT * FROM invoices WHERE id = @OtherField"", dp);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithWithTenantMethodCallOnVar_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class HelperWithTenantRepo
{
    private readonly ITenantContext _tenantContext;
    public HelperWithTenantRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        var dp = new DynamicParameters();
        WithTenant(dp);
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @TenantId"", dp);
    }

    private void WithTenant(DynamicParameters dp) {}
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithCreateTenantParametersInBody_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class HelperCreateTenantRepo
{
    private readonly ITenantContext _tenantContext;
    public HelperCreateTenantRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        var parameters = CreateTenantParameters();
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @TenantId"", parameters);
    }

    private object CreateTenantParameters() => new { TenantId = 1 };
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithPositionalValidParam_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class PositionalParamRepo
{
    private readonly ITenantContext _tenantContext;
    public PositionalParamRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices WHERE tenant_id = @TenantId"", new { TenantId = 123 }, null, 30);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_CustomExtensionOnDbConnection_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using EricksonLopez.MultiTenancy;

public static class CustomExtensions
{
    public static void Query(this IDbConnection conn, string sql) {}
}

public class CustomExtRepo
{
    private readonly ITenantContext _tenantContext;
    public CustomExtRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public void Run(IDbConnection connection)
    {
        connection.Query(""SELECT * FROM invoices"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_NonTenantClassWithPropertyQuery_NoDiagnostic()
    {
        var source = @"
using System.Data;
using Dapper;

public class NonTenantClassWithPropertyQuery
{
    public int Count => ((IDbConnection)null!).ExecuteScalar<int>(""SELECT 1"");
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithNonTenantInvocationParam_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InvocationParamRepo
{
    private readonly ITenantContext _tenantContext;
    public InvocationParamRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"", GetParams());
    }

    private object GetParams() => new { other = 1 };
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithIntegerParam_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class IntParamRepo
{
    private readonly ITenantContext _tenantContext;
    public IntParamRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"", 123);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_QueryWithNamedParamFollowedByOtherArgs_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class BreakRepo
{
    private readonly ITenantContext _tenantContext;
    public BreakRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task GetInvoicesAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM invoices"", param: new { TenantId = 1 }, transaction: null, commandTimeout: 30);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_PropertyGetterInvocationWithIdentifier_NoDiagnostic()
    {
        var source = @"
using System.Data;
using Dapper;
using EricksonLopez.MultiTenancy;

public class PropertyRepoWithIdentifier
{
    private readonly ITenantContext _ctx;
    public PropertyRepoWithIdentifier(ITenantContext ctx) => _ctx = ctx;

    public int Count => ((IDbConnection)null!).ExecuteScalar<int>(""SELECT 1"", TenantId);
    private static int TenantId => 1;
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_NonTenantAwareClass_QueryWithoutParameters_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;

public class SystemMigrationRunner
{
    public async Task ApplyMigrationsAsync(IDbConnection connection)
    {
        await connection.ExecuteAsync(""CREATE TABLE IF NOT EXISTS schema_history (version int)"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_TenantProperty_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class PropertyTenantRepo
{
    public ITenantContext TenantContext => null!;

    public async Task LoadAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM items"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_TopLevelStatementInvocation_NoDiagnostic()
    {
        var source = @"
using System.Data;
using Dapper;

IDbConnection conn = null!;
conn.Query(""SELECT 1"");
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_UnresolvedMethodOnDbConnectionReceiver_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using EricksonLopez.MultiTenancy;

public class UnresolvedMethodRepo
{
    private readonly ITenantContext _tenantContext;
    public UnresolvedMethodRepo(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public void Run(IDbConnection alpha)
    {
        alpha.Query(""SELECT * FROM items"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_NonDbConnectionQuery_NoDiagnostic()
    {
        var source = @"
using System.Collections.Generic;
using EricksonLopez.MultiTenancy;

public class CustomCollection
{
    public void Query(string sql) {}
}

public class CollectionRepo
{
    private readonly ITenantContext _ctx;
    public CollectionRepo(ITenantContext ctx) => _ctx = ctx;

    public void Run(CustomCollection collection)
    {
        collection.Query(""SELECT * FROM items"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_TenantAwareRepository_VariableNamedTargetWithoutConnOrDb_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class TargetVariableRepo
{
    private readonly ITenantContext _ctx;
    public TargetVariableRepo(ITenantContext ctx) => _ctx = ctx;

    public async Task RunAsync(IDbConnection target)
    {
        await target.QueryAsync(""SELECT * FROM users"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_AnonymousObjectWithInferredTenantId_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class InferredTenantRepo
{
    private readonly ITenantContext _ctx;
    public InferredTenantRepo(ITenantContext ctx) => _ctx = ctx;

    public async Task RunAsync(IDbConnection connection)
    {
        string TenantId = ""tenant-1"";
        await connection.QueryAsync(""SELECT * FROM users"", new { TenantId });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_InvocationWithoutMemberAccess_NoDiagnostic()
    {
        var source = @"
public class LocalInvocationClass
{
    public void Test()
    {
        Query();
    }

    private void Query() {}
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_NonDapperMethodOnConnection_NoDiagnostic()
    {
        var source = @"
using System.Data;
using EricksonLopez.MultiTenancy;

public class ConnectionHelper
{
    private readonly ITenantContext _ctx;
    public ConnectionHelper(ITenantContext ctx) => _ctx = ctx;

    public void OpenConn(IDbConnection connection)
    {
        connection.Open();
        connection.Close();
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ITenantInfo")]
    [InlineData("TenantInfo")]
    [InlineData("TenantId")]
    public async Task ELMT003_TenantAwareViaDifferentFieldTypes_EmitsDiagnostic(string typeName)
    {
        var source = $@"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class CustomTenantRepo
{{
    private readonly {typeName} _tenantInfo;

    public async Task QueryAsync(IDbConnection connection)
    {{
        await connection.QueryAsync(""SELECT * FROM users"");
    }}
}}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Theory]
    [InlineData("ITenantInfo")]
    [InlineData("TenantInfo")]
    [InlineData("TenantId")]
    public async Task ELMT003_TenantAwareViaDifferentPropertyTypes_EmitsDiagnostic(string typeName)
    {
        var source = $@"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class CustomPropRepo
{{
    public {typeName} TenantRef {{ get; set; }}

    public async Task QueryAsync(IDbConnection connection)
    {{
        await connection.QueryAsync(""SELECT * FROM users"");
    }}
}}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Theory]
    [InlineData("ITenantInfo")]
    [InlineData("TenantInfo")]
    [InlineData("TenantId")]
    public async Task ELMT003_TenantAwareViaConstructorParameter_EmitsDiagnostic(string typeName)
    {
        var source = $@"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class CustomCtorRepo
{{
    public CustomCtorRepo({typeName} tenantParam, int other) {{}}

    public async Task QueryAsync(IDbConnection connection)
    {{
        await connection.QueryAsync(""SELECT * FROM users"");
    }}
}}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Theory]
    [InlineData("ITenantInfo")]
    [InlineData("TenantInfo")]
    [InlineData("TenantId")]
    public async Task ELMT003_TenantAwareViaMethodParameter_EmitsDiagnostic(string typeName)
    {
        var source = $@"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class MethodParamRepo
{{
    public async Task QueryAsync(IDbConnection connection, {typeName} tenantRef)
    {{
        await connection.QueryAsync(""SELECT * FROM users"");
    }}
}}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_ClassWithDefaultConstructorAndNonTenantParams_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;

public class PlainRepo
{
    public PlainRepo() {}
    public PlainRepo(string connString, int timeout) {}

    public async Task QueryAsync(IDbConnection connection, int userId)
    {
        await connection.QueryAsync(""SELECT * FROM users WHERE id = @userId"", new { userId });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_AnonymousObjectWithMixedCaseTenantId_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class MixedCaseRepo
{
    private readonly ITenantContext _ctx;
    public MixedCaseRepo(ITenantContext ctx) => _ctx = ctx;

    public async Task RunAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM users"", new { TENANTID = ""t-1"" });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_QueryWithExpressionContainingTenantIdDirectly_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class DirectTenantIdRepo
{
    private readonly ITenantContext _ctx;
    public DirectTenantIdRepo(ITenantContext ctx) => _ctx = ctx;

    public async Task RunAsync(IDbConnection connection)
    {
        await connection.QueryAsync(""SELECT * FROM users"", (object)TenantId.New());
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT002_ClassWithStaticFieldOnly_NoDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class StaticCache
{
    private static ITenantContext? _staticCtx;
    public string Name { get; set; } = """";
    public void Clear() {}
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT002_ClassWithNonTenantInstanceFields_NoDiagnostic()
    {
        var source = @"
public class DataCache
{
    private readonly string _cacheKey;
    private int _hitCount;

    public DataCache(string cacheKey)
    {
        _cacheKey = cacheKey;
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new TenantContextInSingletonAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_SqlMapperDirectCall_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class SqlMapperRepo
{
    private readonly ITenantContext _ctx;
    public SqlMapperRepo(ITenantContext ctx) => _ctx = ctx;

    public void Run(IDbConnection connection)
    {
        SqlMapper.Query(connection, ""SELECT * FROM users"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_NonTenantMethodWithNonTenantParams_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;

public class HelperService
{
    public async Task RunAsync(IDbConnection db, int id, string name)
    {
        await db.QueryAsync(""SELECT * FROM users WHERE id = @id"", new { id });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_NonDbReceiverWithDbName_EmitsDiagnostic()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class CustomExecutor
{
    public void Query(string sql) {}
}

public class DynamicDbRepo
{
    private readonly ITenantContext _ctx;
    public DynamicDbRepo(ITenantContext ctx) => _ctx = ctx;

    public void Run(CustomExecutor db)
    {
        db.Query(""SELECT * FROM users"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_UnresolvedExpressionType_FallsBackToIdentifierHeuristic()
    {
        var source = @"
public class UnresolvedRepo
{
    public void Run()
    {
        conn.Query(""SELECT 1"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source, includeReferences: false);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_UndefinedParameterType_NoDiagnostic()
    {
        var source = @"
using System.Data;

public class UndefinedParamRepo
{
    public void Run(IDbConnection db, MissingTypeParam invalid)
    {
        db.Query(""SELECT 1"", new { id = 1 });
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source, includeReferences: false);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_ScriptSource_NoDiagnostic()
    {
        var source = @"
using System.Data;
using Dapper;

IDbConnection db = null!;
db.Query(""SELECT 1"");
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source, includeReferences: true, isScript: true);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_NamespaceReceiver_FallsBackToIdentifier()
    {
        var source = @"
using System.Data;
using EricksonLopez.MultiTenancy;

public class NamespaceRepo
{
    private readonly ITenantContext _ctx;
    public NamespaceRepo(ITenantContext ctx) => _ctx = ctx;

    public void Run()
    {
        System.Data.Query(""SELECT 1"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_SemanticDbConnectionReceiver_NamedSessionWithoutHeuristic_EmitsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class SessionRepo
{
    private readonly ITenantContext _tenantContext;
    private readonly IDbConnection _session;

    public SessionRepo(ITenantContext tenantContext, IDbConnection session)
    {
        _tenantContext = tenantContext;
        _session = session;
    }

    public async Task GetInvoicesAsync()
    {
        await _session.QueryAsync(""SELECT * FROM invoices"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT004_PostgresSuperuserConnectionString_ReportsDiagnostic()
    {
        var source = @"
public class DbConfig
{
    public const string Connection = ""Host=localhost;Database=myapp;User Id=postgres;Password=secret"";
}
";
        var diagnostics = await RunAnalyzerAsync(new SuperuserConnectionStringAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT004");
    }

    [Fact]
    public async Task ELMT004_SqlServerSuperuserConnectionString_ReportsDiagnostic()
    {
        var source = @"
public class DbConfig
{
    public const string Connection = ""Server=localhost;Database=myapp;User Id=sa;Password=secret"";
}
";
        var diagnostics = await RunAnalyzerAsync(new SuperuserConnectionStringAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT004");
    }

    [Fact]
    public async Task ELMT004_ApplicationUserConnectionString_NoDiagnostic()
    {
        var source = @"
public class DbConfig
{
    public const string Connection = ""Host=localhost;Database=myapp;User Id=app_tenant_user;Password=secret"";
}
";
        var diagnostics = await RunAnalyzerAsync(new SuperuserConnectionStringAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT004_NonConnectionStringLiteral_NoDiagnostic()
    {
        var source = @"
public class OtherConfig
{
    public const string Message = ""Welcome to our tenant application!"";
}
";
        var diagnostics = await RunAnalyzerAsync(new SuperuserConnectionStringAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────
    // ELMT001: TenantContextStaticFieldCodeFixProvider Tests
    // ─────────────────────────────────────────────────────────

    [Fact]
    public void ELMT001_CodeFixProvider_Properties_AreConfiguredCorrectly()
    {
        var provider = new TenantContextStaticFieldCodeFixProvider();
        provider.FixableDiagnosticIds.Should().ContainSingle().Which.Should().Be("ELMT001");
        provider.GetFixAllProvider().Should().NotBeNull();
    }

    [Fact]
    public async Task ELMT001_CodeFix_RemovesStaticModifier()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class OrderService
{
    private static ITenantContext _staticContext;
}
";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
#pragma warning disable IL3000
        var coreAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(coreAssemblyPath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(ITenantContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(TenantId).Assembly.Location),
        };
#pragma warning restore IL3000

        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var compilation = CSharpCompilation.Create("TestAssembly", new[] { syntaxTree }, references, compilationOptions);
        var analyzer = new TenantContextStaticFieldAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT001");

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(projectId, references)
            .AddDocument(documentId, "Test.cs", source);
        var document = solution.GetDocument(documentId)!;

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostics[0], (a, _) => actions.Add(a), CancellationToken.None);

        var provider = new TenantContextStaticFieldCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        actions.Should().ContainSingle();
        actions[0].Title.Should().Be("Remove static modifier from tenant context field");

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDoc = changedSolution.GetDocument(documentId)!;
        var changedText = (await changedDoc.GetTextAsync()).ToString();

        changedText.Should().NotContain("static ITenantContext");
        changedText.Should().Contain("private ITenantContext _staticContext;");
    }

    [Fact]
    public async Task ELMT001_CodeFix_NonFieldDiagnostic_DoesNotRegisterCodeFix()
    {
        var source = "public class OrderService {}";
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddDocument(documentId, "Test.cs", source);
        var document = solution.GetDocument(documentId)!;
        var syntaxTree = await document.GetSyntaxTreeAsync();
        var descriptor = new TenantContextStaticFieldAnalyzer().SupportedDiagnostics[0];
        var diagnostic = Diagnostic.Create(descriptor, Location.Create(syntaxTree!, new Microsoft.CodeAnalysis.Text.TextSpan(0, 5)), "OrderService", "ITenantContext");

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (a, _) => actions.Add(a), CancellationToken.None);
        var provider = new TenantContextStaticFieldCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT001_CodeFix_NonStaticFieldDiagnostic_ReturnsDocumentUnchanged()
    {
        var source = @"
using EricksonLopez.MultiTenancy;

public class OrderService
{
    private ITenantContext _instanceContext;
}
";
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddDocument(documentId, "Test.cs", source);
        var document = solution.GetDocument(documentId)!;
        var syntaxTree = (await document.GetSyntaxTreeAsync())!;
        var descriptor = new TenantContextStaticFieldAnalyzer().SupportedDiagnostics[0];
        var fieldToken = syntaxTree.GetRoot().DescendantTokens().First(t => t.ValueText == "_instanceContext");
        var diagnostic = Diagnostic.Create(descriptor, Location.Create(syntaxTree, fieldToken.Span), "_instanceContext", "ITenantContext");

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (a, _) => actions.Add(a), CancellationToken.None);
        var provider = new TenantContextStaticFieldCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        actions.Should().ContainSingle();
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDoc = changedSolution.GetDocument(documentId)!;
        var changedText = (await changedDoc.GetTextAsync()).ToString();

        changedText.Should().Be(source);
    }

    // ─────────────────────────────────────────────────────────
    // ELMT004: Additional Superuser Connection String Tests
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ELMT004_ShortOrWhitespaceString_NoDiagnostic()
    {
        var source = @"
public class DbConfig
{
    public const string Short = ""Host=x;"";
    public const string Spaces = ""             "";
}
";
        var diagnostics = await RunAnalyzerAsync(new SuperuserConnectionStringAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT004_SuperuserWithoutTrailingSemicolon_ReportsDiagnostic()
    {
        var source = @"
public class DbConfig
{
    public const string Connection = ""Server=localhost;Database=myapp;User Id=postgres"";
}
";
        var diagnostics = await RunAnalyzerAsync(new SuperuserConnectionStringAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT004");
    }

    [Fact]
    public async Task ELMT004_VariousSuperuserFormats_ReportsDiagnostic()
    {
        var source = @"
public class DbConfig
{
    public const string Conn1 = ""Server=localhost;Database=mydb;uid=root;Password=secret"";
    public const string Conn2 = ""Server=localhost;Database=mydb;username=postgres;Password=secret"";
    public const string Conn3 = ""Server=localhost;Database=mydb;user=sa;Password=secret"";
}
";
        var diagnostics = await RunAnalyzerAsync(new SuperuserConnectionStringAnalyzer(), source);
        diagnostics.Should().HaveCount(3);
    }

    // ─────────────────────────────────────────────────────────
    // ELMT003: Additional Dapper Query Without Tenant Tests
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ELMT003_InterfaceExtendingIDbConnection_ReportsDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public interface ICustomAppSession : IDbConnection
{
}

public class CustomSessionRepo
{
    private readonly ITenantContext _ctx;
    public CustomSessionRepo(ITenantContext ctx) => _ctx = ctx;

    public async Task RunAsync(ICustomAppSession session)
    {
        await session.QueryAsync(""SELECT 1"");
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().ContainSingle(d => d.Id == "ELMT003");
    }

    [Fact]
    public async Task ELMT003_VariableAssignment_WithTenant_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class AssignedRepo
{
    private readonly ITenantContext _ctx;
    public AssignedRepo(ITenantContext ctx) => _ctx = ctx;

    public async Task RunAsync(IDbConnection db)
    {
        object p = null!;
        p = new { TenantId = _ctx.Tenant.Id };
        await db.QueryAsync(""SELECT 1"", p);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_StaticMethodWithTenant_ConfiguringVariable_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public static class ParameterHelper
{
    public static void WithTenant(object param) {}
}

public class StaticHelperRepo
{
    private readonly ITenantContext _ctx;
    public StaticHelperRepo(ITenantContext ctx) => _ctx = ctx;

    public async Task RunAsync(IDbConnection db)
    {
        var p = new object();
        ParameterHelper.WithTenant(p);
        await db.QueryAsync(""SELECT 1"", p);
    }
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ELMT003_LocalMethodWithTenant_ConfiguringVariable_NoDiagnostic()
    {
        var source = @"
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.MultiTenancy;

public class LocalHelperRepo
{
    private readonly ITenantContext _ctx;
    public LocalHelperRepo(ITenantContext ctx) => _ctx = ctx;

    public async Task RunAsync(IDbConnection db)
    {
        var p = new object();
        WithTenant(p);
        await db.QueryAsync(""SELECT 1"", p);
    }

    private void WithTenant(object param) {}
}
";
        var diagnostics = await RunAnalyzerAsync(new DapperWithoutTenantAnalyzer(), source);
        diagnostics.Should().BeEmpty();
    }
}


