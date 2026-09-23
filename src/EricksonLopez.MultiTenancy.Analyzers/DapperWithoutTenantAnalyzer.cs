// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EricksonLopez.MultiTenancy.Analyzers;

/// <summary>
/// Detects Dapper database queries executed in tenant-aware contexts without explicit tenant parameterization.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DapperWithoutTenantAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Defines the diagnostic identifier for ELMT003.
    /// </summary>
    public const string DiagnosticId = "ELMT003";

    private static readonly LocalizableString _title = "Dapper query executed without tenant parameter in tenant-aware context";
    private static readonly LocalizableString _messageFormat = "Dapper invocation '{0}' in tenant-aware context '{1}' does not provide tenant parameters via WithTenant() or CreateTenantParameters()";
    private static readonly LocalizableString _description = "In multi-tenant services and repositories with an active ITenantContext, executing Dapper queries without explicit tenant parameters risks cross-tenant data exposure.";
    private const string _category = "Security";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        _title,
        _messageFormat,
        _category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: _description);

    private static readonly ImmutableHashSet<string> _dapperMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Query", "QueryAsync",
        "QueryFirst", "QueryFirstAsync",
        "QueryFirstOrDefault", "QueryFirstOrDefaultAsync",
        "QuerySingle", "QuerySingleAsync",
        "QueryMultiple", "QueryMultipleAsync",
        "Execute", "ExecuteAsync",
        "ExecuteScalar", "ExecuteScalarAsync",
        "ExecuteReader", "ExecuteReaderAsync"
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!_dapperMethodNames.Contains(methodName))
        {
            return;
        }

        // Verify if target receiver is a DbConnection or Dapper method
        if (!IsDatabaseConnectionOrDapperCall(memberAccess, context))
        {
            return;
        }

        // Verify if the enclosing scope (method or class) is tenant-aware
        var enclosingType = context.ContainingSymbol!.ContainingType;
        if (enclosingType is null || (!IsTypeTenantAware(enclosingType) && !IsMethodTenantAware(invocation, context)))
        {
            return;
        }

        // Check the arguments passed to the Dapper method
        if (!HasTenantParameter(invocation))
        {
            var diagnostic = Diagnostic.Create(
                _rule,
                memberAccess.Name.GetLocation(),
                methodName,
                enclosingType.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsDatabaseConnectionOrDapperCall(MemberAccessExpressionSyntax memberAccess, SyntaxNodeAnalysisContext context)
    {
        var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (typeInfo is not null && IsDatabaseConnectionType(typeInfo))
        {
            return true;
        }

        var exprText = memberAccess.Expression.ToString().ToLowerInvariant();
        return exprText.Contains("conn") || exprText.Contains("db");
    }


    private static bool IsDatabaseConnectionType(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            var fullName = current.ToDisplayString();
            // Stryker disable once string : Exact type match fallback
            if (fullName is "System.Data.IDbConnection" or "System.Data.Common.DbConnection")
            {
                return true;
            }

            if (ContainsIgnoreCase(fullName, "DbConnection") || ContainsIgnoreCase(fullName, "Dapper"))
            {
                return true;
            }

            // Stryker disable once equality, string, logical : Subsumed interface match
            if (current.AllInterfaces.Any(i =>
                i.ToDisplayString() == "System.Data.IDbConnection" ||
                ContainsIgnoreCase(i.ToDisplayString(), "DbConnection")))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }


    [SuppressMessage("Major Code Smell", "S3776:Cognitive Complexity of methods should not be too high", Justification = "Roslyn member examination requires checking fields, properties, and constructors.")]
    private static bool IsTypeTenantAware(INamedTypeSymbol namedType)
    {
        // Check fields and properties for ITenantContext, ITenantInfo, etc.
        foreach (var member in namedType.GetMembers())
        {
            if (member is IFieldSymbol field && IsTenantType(field.Type))
            {
                return true;
            }

            if (member is IPropertySymbol prop && IsTenantType(prop.Type))
            {
                return true;
            }

            if (member is IMethodSymbol method && method.MethodKind == MethodKind.Constructor && method.Parameters.Any(p => IsTenantType(p.Type)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMethodTenantAware(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        var methodDecl = invocation.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        if (methodDecl is null)
        {
            return false;
        }

        foreach (var param in methodDecl.ParameterList.Parameters)
        {
            var type = context.SemanticModel.GetTypeInfo(param.Type!, context.CancellationToken).Type;
            if (type is not null && IsTenantType(type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTenantType(ITypeSymbol type)
    {
        var name = type.ToDisplayString();
        return ContainsIgnoreCase(name, "ITenantContext") ||
               ContainsIgnoreCase(name, "ITenantInfo") ||
               ContainsIgnoreCase(name, "TenantInfo") ||
               ContainsIgnoreCase(name, "TenantId");
    }

    private const string _tenantIdIdentifier = "TenantId";

    [SuppressMessage("Major Code Smell", "S3776:Cognitive Complexity of methods should not be too high", Justification = "Roslyn AST parameter and syntax inspection requires checking multiple syntax forms.")]
    private static bool HasTenantParameter(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;

        // Look for named 'param:' / 'parameters:' argument, or positional 2nd argument (index 1)
        ArgumentSyntax? paramArg = arguments.FirstOrDefault(a =>
            a.NameColon is not null &&
            (string.Equals(a.NameColon.Name.Identifier.ValueText, "param", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(a.NameColon.Name.Identifier.ValueText, "parameters", StringComparison.OrdinalIgnoreCase)));

        if (paramArg is null && arguments.Count > 1 && arguments[1].NameColon is null)
        {
            paramArg = arguments[1];
        }

        if (paramArg is null)
        {
            return false;
        }

        var paramExpr = paramArg.Expression;

        // Null literal
        if (paramExpr is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return false;
        }

        // Check if argument is an anonymous object creation expression new { ... }
        if (paramExpr is AnonymousObjectCreationExpressionSyntax anon)
        {
            foreach (var init in anon.Initializers)
            {
                if (init.NameEquals is not null)
                {
                    var name = init.NameEquals.Name.Identifier.ValueText;
                    if (string.Equals(name, _tenantIdIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "tenant_id", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (ContainsIgnoreCase(init.Expression.ToString(), _tenantIdIdentifier) ||
                    ContainsIgnoreCase(init.Expression.ToString(), "TenantContext"))
                {
                    return true;
                }
            }
            return false;
        }

        // Check if argument is a variable identifier
        if (paramExpr is IdentifierNameSyntax identifier)
        {
            var varName = identifier.Identifier.ValueText;
            // Stryker disable once string : Snake case identifier match
            if (string.Equals(varName, _tenantIdIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(varName, "tenant_id", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Stryker disable once linq : Ancestor method declaration search
            var enclosingMethod = invocation.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
            if (enclosingMethod is not null)
            {
                // 1. Check variable declarations: var x = ...
                var declarators = enclosingMethod.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                    .Where(v => v.Identifier.ValueText == varName);

                foreach (var decl in declarators)
                {
                    if (decl.Initializer?.Value is { } initExpr)
                    {
                        var initText = initExpr.ToString();
                        if (ContainsIgnoreCase(initText, "CreateTenantParameters") ||
                            ContainsIgnoreCase(initText, "WithTenant") ||
                            ContainsIgnoreCase(initText, _tenantIdIdentifier) ||
                            ContainsIgnoreCase(initText, "tenant_id"))
                        {
                            return true;
                        }
                    }
                }

                // 2. Check variable assignments: x = ...
                var assignments = enclosingMethod.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                    .Where(a => a.Left is IdentifierNameSyntax id && id.Identifier.ValueText == varName);

                foreach (var assign in assignments)
                {
                    var rightText = assign.Right.ToString();
                    // Stryker disable once string, logical : Assignment text heuristic
                    if (ContainsIgnoreCase(rightText, "CreateTenantParameters") ||
                        ContainsIgnoreCase(rightText, "WithTenant") ||
                        ContainsIgnoreCase(rightText, _tenantIdIdentifier) ||
                        ContainsIgnoreCase(rightText, "tenant_id"))
                    {
                        return true;
                    }
                }

                // 3. Check methods configuring the variable: x.WithTenant(...) or x.Add("TenantId", ...)
                var invocations = enclosingMethod.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var inv in invocations)
                {
                    // Stryker disable once statement, block : Skip current invocation
                    if (inv == invocation)
                    {
                        continue;
                    }

                    if (inv.Expression is MemberAccessExpressionSyntax ma &&
                        ma.Expression is IdentifierNameSyntax id &&
                        id.Identifier.ValueText == varName)
                    {
                        var memberName = ma.Name.Identifier.ValueText;
                        if (string.Equals(memberName, "WithTenant", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (string.Equals(memberName, "Add", StringComparison.OrdinalIgnoreCase))
                        {
                            var argsText = inv.ArgumentList.ToString();
                            if (ContainsIgnoreCase(argsText, $"\"{_tenantIdIdentifier}\"") ||
                                ContainsIgnoreCase(argsText, "\"tenant_id\""))
                            {
                                return true;
                            }
                        }
                    }

                    // Stryker disable once logical, linq : Helper invocation argument matching
                    if (inv.Expression is IdentifierNameSyntax fnId &&
                        string.Equals(fnId.Identifier.ValueText, "WithTenant", StringComparison.OrdinalIgnoreCase) &&
                        inv.ArgumentList.Arguments.Any(a => a.Expression is IdentifierNameSyntax argId && argId.Identifier.ValueText == varName))
                    {
                        return true;
                    }
                    // Stryker disable once logical, linq : Helper invocation argument matching
                    else if (inv.Expression is MemberAccessExpressionSyntax staticMa &&
                             string.Equals(staticMa.Name.Identifier.ValueText, "WithTenant", StringComparison.OrdinalIgnoreCase) &&
                             inv.ArgumentList.Arguments.Any(a => a.Expression is IdentifierNameSyntax argId && argId.Identifier.ValueText == varName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Check expression text
        var exprText = paramExpr.ToString();
        return ContainsIgnoreCase(exprText, "WithTenant") ||
               ContainsIgnoreCase(exprText, "CreateTenantParameters") ||
               ContainsIgnoreCase(exprText, _tenantIdIdentifier);
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return source.ToUpperInvariant().Contains(value.ToUpperInvariant());
    }
}
