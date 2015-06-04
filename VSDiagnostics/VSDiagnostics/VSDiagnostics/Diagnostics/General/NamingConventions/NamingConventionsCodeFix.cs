using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace VSDiagnostics.Diagnostics.General.NamingConventions
{
    [ExportCodeFixProvider("NamingConventions", LanguageNames.CSharp), Shared]
    public class NamingConventionsCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(NamingConventionsAnalyzer.DiagnosticId);
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var identifier = root.FindToken(diagnosticSpan.Start);
            context.RegisterCodeFix(CodeAction.Create("Rename", x => RenameAsync(context.Document, root, identifier)), diagnostic);
        }

        private static Task<Solution> RenameAsync(Document document, SyntaxNode root, SyntaxToken identifier)
        {
            var ancestor = identifier.GetAncestors().First(node => node.GetNamingConvention().HasValue);
            var namingConvention = ancestor.GetNamingConvention().Value;
            var newParent = ancestor.ReplaceToken(identifier, identifier.WithConvention(namingConvention));
            var newRoot = root.ReplaceNode(ancestor, newParent);
            return Task.FromResult(document.WithSyntaxRoot(newRoot).Project.Solution);
        }
    }
}