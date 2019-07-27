// *********************************************************************
// Copyright (c). All rights reserved.
// See license file in root dir for details.
// *********************************************************************

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CSharpRecordsAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpRecordsAnalyzer : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor RecordCreateDiagnostic = new DiagnosticDescriptor(
            "RecordCreate", "Record constructor and modifier can be created",
            "Record constructor and modifier can be created", "Refactoring", DiagnosticSeverity.Hidden, true
        );

        internal static readonly DiagnosticDescriptor RecordUpdateDiagnostic = new DiagnosticDescriptor(
            "RecordUpdate", "Record constructor and modifier can be updated",
            "Record constructor and modifier can be updated", "Refactoring", DiagnosticSeverity.Warning, true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(RecordUpdateDiagnostic, RecordCreateDiagnostic);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        }

        private void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
        {
            var typeDeclaration = (TypeDeclarationSyntax) context.Node;
            if (IsTypeEligible(typeDeclaration))
            {
                var location = Location.Create(
                    typeDeclaration.SyntaxTree,
                    TextSpan.FromBounds(
                        typeDeclaration.FullSpan.Start, typeDeclaration.OpenBraceToken.GetLocation().SourceSpan.End
                    )
                );
                context.ReportDiagnostic(
                    IsImplementedWrong(typeDeclaration)
                        ? Diagnostic.Create(RecordUpdateDiagnostic, location)
                        : Diagnostic.Create(RecordCreateDiagnostic, location)
                );
            }
        }

        public static bool IsImplementedWrong(TypeDeclarationSyntax typeDeclaration)
        {
            var nonStaticPublicFields = typeDeclaration.Members.OfType<FieldDeclarationSyntax>()
                .Where(field => field.Modifiers.All(m => m.Kind() != SyntaxKind.StaticKeyword))
                .Where(field => field.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword));

            var nonStaticPublicPropertiesWithNoBody = typeDeclaration.Members.OfType<PropertyDeclarationSyntax>()
                .Where(property => property.Modifiers.All(m => m.Kind() != SyntaxKind.StaticKeyword))
                .Where(property => property.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword))
                .Where(property => property.AccessorList != null)
                .Where(property => property.AccessorList.Accessors.All(x => x.Body == null));

            var fieldNames = nonStaticPublicFields.SelectMany(x => x.Declaration.Variables)
                .Select(x => x.Identifier.Text)
                .Distinct();
            var propertyNames = nonStaticPublicPropertiesWithNoBody.Select(x => x.Identifier.Text);
            var fieldsAndPropertiesSorted = fieldNames.Concat(propertyNames).OrderBy(x => x).ToList();

            var ctorDeclaration = typeDeclaration.Members.OfType<ConstructorDeclarationSyntax>()
                .Where(
                    ctor => ctor.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword) &&
                        ctor.ParameterList != null && ctor.ParameterList.Parameters.Any(
                            param => param.Identifier.Text.Length > 0 && char.IsUpper(param.Identifier.Text[0])
                        )
                )
                .ToList();

            var isCtorImplemented = ctorDeclaration.Any();

            var bestCtor = ctorDeclaration.FirstOrDefault(
                ctor => {
                    var parameters = ctor.ParameterList.Parameters.Select(x => x.Identifier.Text).OrderBy(x => x);

                    // Correct if one parameter for each field and property.
                    var isDeclarationCorrect = parameters.SequenceEqual(fieldsAndPropertiesSorted);

                    var ctorAssignments = ctor.Body.Statements
                        .Select(
                            x => new {left = GetLeftMemberAssignmentIdentifier(x), right = GetRightSideIdentifier(x)}
                        )
                        .Where(x => fieldsAndPropertiesSorted.Contains(x.left))
                        .ToList();

                    var isImplementationCorrect = ctorAssignments.Count == fieldsAndPropertiesSorted.Count &&
                        ctorAssignments.All(x => x.left == x.right);

                    return isDeclarationCorrect && isImplementationCorrect;

                    string GetLeftMemberAssignmentIdentifier(StatementSyntax s)
                    {
                        var memberAccess = GetAssignmentExp(s)?.Left as MemberAccessExpressionSyntax;
                        if (memberAccess?.Expression.Kind() != SyntaxKind.ThisExpression)
                        {
                            return null;
                        }

                        return (memberAccess.Name as IdentifierNameSyntax)?.Identifier.Text;
                    }

                    string GetRightSideIdentifier(StatementSyntax s)
                    {
                        return (GetAssignmentExp(s)?.Right as IdentifierNameSyntax)?.Identifier.Text;
                    }

                    AssignmentExpressionSyntax GetAssignmentExp(StatementSyntax s)
                    {
                        return (s as ExpressionStatementSyntax)?.Expression as AssignmentExpressionSyntax;
                    }
                }
            );

            var isCtorImplementedCorrectly = bestCtor != null;

            var withMethodDeclaration = typeDeclaration.Members.OfType<MethodDeclarationSyntax>()
                .Where(
                    method => method.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword) &&
                        method.Identifier.Text == "With" && method.ParameterList != null &&
                        method.ParameterList.Parameters.All(
                            parameter =>
                                // All parameters to With method are nullable and default to null
                                parameter.Default?.Value?.Kind() == SyntaxKind.NullLiteralExpression
                        )
                )
                .ToList();

            var isWithMethodImplemented = withMethodDeclaration.Any();

            var bestWithMethod = withMethodDeclaration.FirstOrDefault(
                method => {
                    if (bestCtor == null)
                    {
                        return false;
                    }

                    var parametersSorted = method.ParameterList.Parameters.Select(x => x.Identifier.Text)
                        .OrderBy(x => x);

                    // Correct if one parameter for each field and property.
                    var isDeclarationCorrect = parametersSorted.SequenceEqual(fieldsAndPropertiesSorted);

                    var isSingleStatement = method.Body.Statements.Count == 1;

                    var isImplementationCorrect = isSingleStatement && method.Body.Statements.Any(
                        statement => {
                            var ctorInvocation =
                                (statement as ReturnStatementSyntax)?.Expression as ObjectCreationExpressionSyntax;

                            var ctorTypeMatch = GetTypeMatch(ctorInvocation);

                            bool GetTypeMatch(ObjectCreationExpressionSyntax ctor)
                            {
                                switch (ctor?.Type)
                                {
                                    case IdentifierNameSyntax nameSyntax:
                                        return nameSyntax.Identifier.Text == bestCtor.Identifier.Text;
                                    case GenericNameSyntax genericSyntax:
                                        var typeArgNames =
                                            typeDeclaration.TypeParameterList?.Parameters.Select(
                                                (x, idx) => (idx, x.Identifier.Text)
                                            );

                                        var nameMatch = genericSyntax.Identifier.Text == bestCtor.Identifier.Text;

                                        var typeArgumentsMatch = genericSyntax.TypeArgumentList.Arguments
                                            .Select((x, idx) => (idx, (x as IdentifierNameSyntax)?.Identifier.Text))
                                            .OrderBy(x => x.idx)
                                            .SequenceEqual(typeArgNames ?? Enumerable.Empty<(int, string)>());

                                        return nameMatch && typeArgumentsMatch;
                                    default:
                                        return false;
                                }
                            }

                            var ctorArgumentsMatch = ctorInvocation?.ArgumentList != null &&
                                ctorInvocation.ArgumentList.Arguments.Count == bestCtor.ParameterList.Parameters.Count;

                            return ctorTypeMatch && ctorArgumentsMatch;
                        }
                    );

                    return isDeclarationCorrect && isImplementationCorrect;
                }
            );

            var isWithMethodImplementedCorrectly = bestWithMethod != null;

            // Warn if it's possible to implement record and ctor has been created for it,
            // but ctor is not correct or With method is not correct.
            return isCtorImplemented && !isCtorImplementedCorrectly ||
                isCtorImplemented && isWithMethodImplemented && !isWithMethodImplementedCorrectly;
        }

        public static bool IsTypeEligible(TypeDeclarationSyntax typeDeclaration)
        {
            var atLeastOnePublicReadonlyField = typeDeclaration.Members.OfType<FieldDeclarationSyntax>()
                .Where(field => field.Modifiers.All(m => m.Kind() != SyntaxKind.StaticKeyword))
                .Any(
                    field => field.Modifiers.Any(m => m.Kind() == SyntaxKind.ReadOnlyKeyword) &&
                        field.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword)
                );

            var atLeastOnePublicProperty = typeDeclaration.Members.OfType<PropertyDeclarationSyntax>()
                .Any(property => property.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword));

            var areAllNonStaticPublicFieldsReadonly = typeDeclaration.Members.OfType<FieldDeclarationSyntax>()
                .Where(field => field.Modifiers.All(m => m.Kind() != SyntaxKind.StaticKeyword))
                .Where(field => field.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword))
                .All(field => field.Modifiers.Any(m => m.Kind() == SyntaxKind.ReadOnlyKeyword));

            var areAllNonStaticPublicPropertiesWithNoBodyReadonly = typeDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(property => property.Modifiers.All(m => m.Kind() != SyntaxKind.StaticKeyword))
                .Where(property => property.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword))
                .Where(property => property.AccessorList != null)
                .Where(property => property.AccessorList.Accessors.All(x => x.Body == null))
                .All(
                    property => property.AccessorList.Accessors.All(x => x.Kind() == SyntaxKind.GetAccessorDeclaration)
                );

            return (atLeastOnePublicReadonlyField || atLeastOnePublicProperty) && areAllNonStaticPublicFieldsReadonly &&
                areAllNonStaticPublicPropertiesWithNoBodyReadonly;
        }
    }
}
