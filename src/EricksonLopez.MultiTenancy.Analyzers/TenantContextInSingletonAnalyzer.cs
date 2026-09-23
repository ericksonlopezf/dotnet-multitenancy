// Copyright © Erickson Lopez. MIT License.
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EricksonLopez.MultiTenancy.Analyzers;

/// <summary>
/// Detects injection or storage of scoped tenant context abstractions in singleton or cache types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TenantContextInSingletonAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Defines the diagnostic identifier for ELMT002.
    /// </summary>
    public const string DiagnosticId = "ELMT002";

    private static readonly LocalizableString _title = "Do not reference scoped tenant context in singleton classes";
    private static readonly LocalizableString _messageFormat = "Type '{0}' appears to be a singleton or cache and references scoped tenant context '{1}', which causes captive dependency leaks";
    private static readonly LocalizableString _description = "ITenantContext and ITenantContextAccessor are scoped abstractions. Using them as fields or constructor parameters in singleton classes causes captive dependencies across requests.";
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

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        var typeName = namedType.Name;
        bool isLikelySingleton = typeName.EndsWith("Singleton", System.StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Cache", System.StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("MemoryStore", System.StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("HostedService", System.StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("BackgroundService", System.StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Worker", System.StringComparison.OrdinalIgnoreCase);

        if (!isLikelySingleton)
        {
            return;
        }

        foreach (var member in namedType.GetMembers())
        {
            if (member is IFieldSymbol field && !field.IsStatic && !field.IsImplicitlyDeclared)
            {
                var fieldTypeName = field.Type.ToDisplayString();
                if (IsTenantContextType(fieldTypeName))
                {
                    var diagnostic = Diagnostic.Create(
                        _rule,
                        field.Locations[0],
                        namedType.Name,
                        field.Type.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
            else if (member is IPropertySymbol property && !property.IsStatic)
            {
                var propTypeName = property.Type.ToDisplayString();
                if (IsTenantContextType(propTypeName))
                {
                    var diagnostic = Diagnostic.Create(
                        _rule,
                        property.Locations[0],
                        namedType.Name,
                        property.Type.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static bool IsTenantContextType(string typeName)
    {
        return (typeName.Contains("TenantContext") ||
                typeName.Contains("ITenantInfo") ||
                typeName.Contains("TenantInfo") ||
                typeName.Contains("TenantId"))
            && !typeName.Contains("TenantContextBuilder")
            && !typeName.Contains("TestTenantContext");
    }
}
