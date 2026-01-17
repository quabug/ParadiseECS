using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Paradise.ECS.Generators;

/// <summary>
/// Analyzer that detects when a disposable ref struct is not properly disposed.
/// Disposable ref structs must be wrapped in a 'using' statement or explicitly disposed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposableRefStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.DisposableRefStructNotDisposed);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Analyze local variable declarations
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);

        // Analyze expression statements (discarded return values)
        context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);

        // Analyze simple assignment expressions (reassignment to existing variable)
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);

        // Analyze member access on invocations (e.g., GetComponent<T>().Value = x)
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var localDeclaration = (LocalDeclarationStatementSyntax)context.Node;

        // If it's already a using declaration, it's fine
        if (localDeclaration.UsingKeyword != default)
            return;

        // Check if it's inside a using statement
        if (localDeclaration.Parent is BlockSyntax { Parent: UsingStatementSyntax })
            return;

        foreach (var variable in localDeclaration.Declaration.Variables)
        {
            if (variable.Initializer?.Value == null)
                continue;

            var typeInfo = context.SemanticModel.GetTypeInfo(variable.Initializer.Value, context.CancellationToken);
            if (typeInfo.Type is not INamedTypeSymbol typeSymbol)
                continue;

            if (!IsDisposableRefStruct(typeSymbol))
                continue;

            // Check if the variable is disposed later in the same scope
            var symbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken);
            if (symbol != null && IsDisposedInScope(localDeclaration, symbol, context.SemanticModel, context.CancellationToken))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DisposableRefStructNotDisposed,
                variable.GetLocation(),
                typeSymbol.Name));
        }
    }

    private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        var expressionStatement = (ExpressionStatementSyntax)context.Node;

        // Check if the expression is an invocation whose return value is discarded
        if (expressionStatement.Expression is not InvocationExpressionSyntax invocation)
            return;

        var typeInfo = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol typeSymbol)
            return;

        if (!IsDisposableRefStruct(typeSymbol))
            return;

        // The return value of a method that returns a disposable ref struct is being discarded
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DisposableRefStructNotDisposed,
            invocation.GetLocation(),
            typeSymbol.Name));
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Skip if this is part of a declaration (handled by AnalyzeLocalDeclaration)
        if (assignment.Parent is EqualsValueClauseSyntax)
            return;

        // Check for discard pattern: _ = SomeMethod()
        if (assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "_" })
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(assignment.Right, context.CancellationToken);
            if (typeInfo.Type is INamedTypeSymbol typeSymbol && IsDisposableRefStruct(typeSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DisposableRefStructNotDisposed,
                    assignment.GetLocation(),
                    typeSymbol.Name));
            }
            return;
        }

        // For regular assignments, check if the right side is a disposable ref struct
        var rhsTypeInfo = context.SemanticModel.GetTypeInfo(assignment.Right, context.CancellationToken);
        if (rhsTypeInfo.Type is not INamedTypeSymbol rhsTypeSymbol)
            return;

        if (!IsDisposableRefStruct(rhsTypeSymbol))
            return;

        // Get the symbol for the left side to check for dispose calls
        var lhsSymbol = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
        if (lhsSymbol == null)
            return;

        // Find the containing block to check for dispose calls
        var containingBlock = assignment.FirstAncestorOrSelf<BlockSyntax>();
        if (containingBlock == null)
            return;

        // Check if the variable is disposed after this assignment
        if (IsDisposedAfterStatement(assignment, lhsSymbol, containingBlock, context.SemanticModel, context.CancellationToken))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DisposableRefStructNotDisposed,
            assignment.GetLocation(),
            rhsTypeSymbol.Name));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // We're looking for patterns like: GetComponent<T>().Value
        // where the expression is an invocation returning a disposable ref struct
        if (memberAccess.Expression is not InvocationExpressionSyntax invocation)
            return;

        // Skip if this is a Dispose() call - that's the correct usage
        if (memberAccess.Name.Identifier.ValueText == "Dispose")
            return;

        // Check if the invocation returns a disposable ref struct
        var typeInfo = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol typeSymbol)
            return;

        if (!IsDisposableRefStruct(typeSymbol))
            return;

        // The return value of a method that returns a disposable ref struct is being used
        // for member access without being stored in a variable (so it can't be disposed)
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DisposableRefStructNotDisposed,
            invocation.GetLocation(),
            typeSymbol.Name));
    }

    private static bool IsDisposableRefStruct(INamedTypeSymbol typeSymbol)
    {
        // Check if it's a ref struct
        if (!typeSymbol.IsRefLikeType)
            return false;

        // Check if it has a Dispose method (with no parameters)
        foreach (var member in typeSymbol.GetMembers("Dispose"))
        {
            if (member is IMethodSymbol { Parameters.Length: 0, ReturnsVoid: true })
                return true;
        }

        return false;
    }

    private static bool IsDisposedInScope(
        LocalDeclarationStatementSyntax declaration,
        ISymbol variableSymbol,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        // Find the containing block
        var containingBlock = declaration.Parent as BlockSyntax;
        if (containingBlock == null)
            return false;

        return IsDisposedAfterStatement(declaration, variableSymbol, containingBlock, semanticModel, cancellationToken);
    }

    private static bool IsDisposedAfterStatement(
        SyntaxNode statement,
        ISymbol variableSymbol,
        BlockSyntax containingBlock,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        bool foundStatement = false;

        foreach (var sibling in containingBlock.Statements)
        {
            if (!foundStatement)
            {
                // Keep looking for the target statement
                if (sibling == statement || sibling.Contains(statement))
                {
                    foundStatement = true;
                }
                continue;
            }

            // Look for Dispose() calls on this variable after the statement
            if (HasDisposeCall(sibling, variableSymbol, semanticModel, cancellationToken))
                return true;
        }

        return false;
    }

    private static bool HasDisposeCall(
        SyntaxNode node,
        ISymbol variableSymbol,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var invocation in node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            // Check for variable.Dispose() pattern
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "Dispose")
            {
                var targetSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;
                if (SymbolEqualityComparer.Default.Equals(targetSymbol, variableSymbol))
                    return true;
            }
        }

        return false;
    }
}
