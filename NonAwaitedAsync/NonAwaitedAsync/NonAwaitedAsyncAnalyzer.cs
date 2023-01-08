using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NonAwaitedAsync
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NonAwaitedAsyncAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NonAwaitedAsync";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information

            context.RegisterSyntaxNodeAction(AnalyzeSymbolNode, SyntaxKind.InvocationExpression);
        }
        private void AnalyzeSymbolNode(SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
        {
            if (syntaxNodeAnalysisContext.Node is InvocationExpressionSyntax node)
            {
                if (syntaxNodeAnalysisContext
                        .SemanticModel
                        .GetSymbolInfo(node.Expression, syntaxNodeAnalysisContext.CancellationToken)
                        .Symbol is IMethodSymbol methodSymbol)
                {
                    
                    if (node.Parent is AwaitExpressionSyntax)
                    {
                        return;
                    }

                    if (node.Parent is ReturnStatementSyntax)
                    {
                        return;
                    }

                    //if (node.Parent is ExpressionStatementSyntax)
                    //{
                    //    return;
                    //}

                    //if (node.Expression is MemberAccessExpressionSyntax)
                    //{
                    //    return;
                    //}

                    var taskNamedTypeSymbol = syntaxNodeAnalysisContext.SemanticModel.Compilation.GetTypeByMetadataName(typeof(Task).FullName);

                    if (methodSymbol.ReturnType.ContainingNamespace is null)
                    {
                        return;
                    }

                    var returnTypesFullNameOfNamespace = GetFullname(methodSymbol.ReturnType.ContainingNamespace);

                    if (returnTypesFullNameOfNamespace != "System.Threading.Tasks")
                    {
                        return;
                    }

                    //if (methodSymbol.ReturnType.Equals(taskNamedTypeSymbol))
                    if (methodSymbol.ReturnType.Name == taskNamedTypeSymbol.Name)
                    {
                        if (node.Parent is EqualsValueClauseSyntax equalsValueClauseParent)
                        {
                            // If the task is assigned to a variable with name task then we don't report diagnostics
                            if (equalsValueClauseParent.Parent is VariableDeclaratorSyntax variableDeclarator)
                            {
                                if(variableDeclarator.Identifier.Text.ToLowerInvariant().Contains("task"))
                                {
                                    return;
                                }
                            }
                        }

                        if (node.Parent is ArgumentSyntax argumentSyntax)
                        {
                            if (argumentSyntax.Parent is ArgumentListSyntax argumentListSyntax)
                            {
                                if (argumentListSyntax.Parent is InvocationExpressionSyntax invocationExpressionSyntax)
                                {
                                    var memberAccesSyntaxNode = invocationExpressionSyntax.ChildNodes()
                                        .FirstOrDefault(x => x is MemberAccessExpressionSyntax) as MemberAccessExpressionSyntax;
                                    if (memberAccesSyntaxNode != null)
                                    {
                                        if (memberAccesSyntaxNode.Expression is IdentifierNameSyntax expressionSyntax)
                                        {
                                            if (memberAccesSyntaxNode.Name.Identifier.Text == "Add"
                                                && expressionSyntax.Identifier.Text.ToLowerInvariant().Contains("task"))
                                            {
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // For all such symbols, produce a diagnostic.
                        var diagnostic =
                            Diagnostic.Create(Rule, node.GetLocation(), methodSymbol.ToDisplayString());

                        syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                        
                        //TODO: Check if code below this return is really needed
                        return;
                    }

                    if (methodSymbol.ReturnType is INamedTypeSymbol symbol)
                    {
                        //if (symbol.IsGenericType && symbol.BaseType.Equals(taskNamedTypeSymbol))
                        if (symbol.IsGenericType && symbol.BaseType.Name == taskNamedTypeSymbol.Name)
                        {
                            // For all such symbols, produce a diagnostic.
                            var diagnostic =
                                Diagnostic.Create(Rule, node.GetLocation(), methodSymbol.ToDisplayString());

                            syntaxNodeAnalysisContext.ReportDiagnostic(diagnostic);
                        }
                    }

                }
            }
        }

        private static string GetFullname(INamespaceSymbol ns)
        {
            if (ns == null)
            {
                throw new ArgumentNullException(nameof(ns));
            }

            if (ns.IsGlobalNamespace)
                return "";

            if (ns.ContainingNamespace.IsGlobalNamespace)
                return ns.Name;

            return GetFullname(ns.ContainingNamespace) + "." + ns.Name;
        }

    }
}
