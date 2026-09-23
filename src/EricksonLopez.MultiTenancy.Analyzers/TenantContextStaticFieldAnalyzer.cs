// Copyright © Erickson Lopez. MIT License.
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EricksonLopez.MultiTenancy.Analyzers;

/// <summary>
/// Detects storage of scoped tenant context abstractions in static fields.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TenantContextStaticFieldAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Defines the diagnostic identifier for ELMT001.
    /// </summary>
    public const string DiagnosticId = "ELMT001";

    private static readonly LocalizableString _title = "Do not store tenant context in static fields";
    private static readonly LocalizableString _messageFormat = "Field '{0}' is static and stores tenant context type '{1}', which can cause cross-tenant context leakage";
    private static readonly LocalizableString _description = "ITenantContext and ITenantContextAccessor are scoped abstractions. Storing them in static fields creates concurrency leaks across tenants.";
    private const string _category = "Safety";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        _title,
        _messageFormat,
        _category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: _description);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        var fieldSymbol = (IFieldSymbol)context.Symbol;

        if (!fieldSymbol.IsStatic)
        {
            return;
        }

        var fieldType = fieldSymbol.Type;
        var typeName = fieldType.ToDisplayString();

        if (IsTenantState(typeName))
        {
            var diagnostic = Diagnostic.Create(
                _rule,
                fieldSymbol.Locations[0],
                fieldSymbol.Name,
                fieldType.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsTenantState(string typeName)
    {
        return (typeName.Contains("TenantContext") ||
                typeName.Contains("ITenantInfo") ||
                typeName.Contains("TenantInfo") ||
                typeName.Contains("TenantId"))
            && !typeName.Contains("TenantContextBuilder")
            && !typeName.Contains("TestTenantContext");
    }
}
