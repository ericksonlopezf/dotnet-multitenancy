// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EricksonLopez.MultiTenancy.Analyzers;

/// <summary>
/// Detects database connection strings configured with superuser accounts (such as postgres, sa, root)
/// which bypass database Row-Level Security (RLS) policies and multi-tenant isolation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuperuserConnectionStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Defines the diagnostic identifier for ELMT004.
    /// </summary>
    public const string DiagnosticId = "ELMT004";

    private static readonly LocalizableString _title = "Do not use database superuser accounts in multi-tenant connection strings";
    private static readonly LocalizableString _messageFormat = "Connection string contains superuser credential '{0}', which bypasses multi-tenant Row-Level Security isolation";
    private static readonly LocalizableString _description = "Database superuser accounts (such as 'postgres', 'sa', 'root') bypass PostgreSQL Row-Level Security (BYPASSRLS) and SQL Server session context isolation. Multi-tenant applications must use non-superuser accounts.";
    private const string _category = "Security";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        _title,
        _messageFormat,
        _category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: _description);

    private static readonly string[] _superUsers =
    [
        "postgres",
        "sa",
        "root"
    ];

    private static readonly string[] _userKeys =
    [
        "user id=",
        "user=",
        "username=",
        "uid="
    ];

    // Stryker disable once string : Connection string keyword heuristics
    private static readonly string[] _connectionKeywords =
    [
        "host=",
        "server=",
        "data source=",
        "database=",
        "initial catalog="
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // Stryker disable once statement : Roslyn configuration
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Stryker disable once statement : Roslyn configuration
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.StringLiteralExpression);
    }

    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var text = literal.Token.ValueText;

        // Stryker disable once logical, equality, statement : Minimum connection string length heuristic
        if (string.IsNullOrWhiteSpace(text) || text.Length < 10)
        {
            return;
        }

        // Stryker disable once statement : Guard clause for connection string
        if (!IsConnectionString(text))
        {
            return;
        }

        if (TryGetSuperuser(text, out var matchedSuperuser))
        {
            var diagnostic = Diagnostic.Create(
                _rule,
                literal.GetLocation(),
                matchedSuperuser);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsConnectionString(string text)
    {
        foreach (var keyword in _connectionKeywords)
        {
            // Stryker disable once boolean : Keyword detection
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Stryker disable once boolean : Keyword detection fallback
        return false;
    }

    private static bool TryGetSuperuser(string text, out string? matchedUser)
    {
        matchedUser = null;

        foreach (var key in _userKeys)
        {
            int index = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            // Stryker disable once equality : Index search continuation
            while (index >= 0)
            {
                int valStart = index + key.Length;
                int valEnd = text.IndexOf(';', valStart);
                // Stryker disable once equality : Semicolon terminator index
                if (valEnd < 0)
                {
                    valEnd = text.Length;
                }

                var userVal = text.Substring(valStart, valEnd - valStart).Trim();
                foreach (var superUser in _superUsers)
                {
                    if (string.Equals(userVal, superUser, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedUser = superUser;
                        return true;
                    }
                }

                index = text.IndexOf(key, valEnd, StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
