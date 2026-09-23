// Copyright © Erickson Lopez. MIT License.
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EricksonLopez.MultiTenancy.Analyzers;

/// <summary>
/// Provides a code fix for <see cref="TenantContextStaticFieldAnalyzer"/> (ELMT001) that removes the static modifier.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TenantContextStaticFieldCodeFixProvider)), Shared]
public sealed class TenantContextStaticFieldCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(TenantContextStaticFieldAnalyzer.DiagnosticId);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var declaration = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault();
        if (declaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove static modifier from tenant context field",
                createChangedDocument: c => RemoveStaticModifierAsync(context.Document, declaration, c),
                equivalenceKey: "Remove static modifier from tenant context field"),
            diagnostic);
    }

    private static async Task<Document> RemoveStaticModifierAsync(Document document, FieldDeclarationSyntax fieldDecl, CancellationToken cancellationToken)
    {
        var staticModifier = fieldDecl.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.StaticKeyword));
        if (staticModifier == default)
        {
            return document;
        }

        var newModifiers = fieldDecl.Modifiers.Remove(staticModifier);
        var newFieldDecl = fieldDecl.WithModifiers(newModifiers);

        var root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
        var newRoot = root.ReplaceNode(fieldDecl, newFieldDecl);
        return document.WithSyntaxRoot(newRoot);
    }
}
