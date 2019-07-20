// *********************************************************************
// Copyright (c). All rights reserved.
// See license file in root dir for details.
// *********************************************************************

using System;
using System.Collections.Generic;
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
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CSharpRecordsAnalyzer
{
    public class Field
    {
        private static readonly HashSet<SyntaxKind> NonNullablePredefinedTypes = new HashSet<SyntaxKind>(
            new[]
            {
                SyntaxKind.SByteKeyword,
                SyntaxKind.ShortKeyword,
                SyntaxKind.IntKeyword,
                SyntaxKind.ByteKeyword,
                SyntaxKind.UShortKeyword,
                SyntaxKind.UIntKeyword,
                SyntaxKind.ULongKeyword,
                SyntaxKind.LongKeyword,
                SyntaxKind.FloatKeyword,
                SyntaxKind.DoubleKeyword,
                SyntaxKind.BoolKeyword,
                SyntaxKind.CharKeyword,
                SyntaxKind.DecimalKeyword
            }
        );

        private static readonly HashSet<string> NonNullableTypeName = new HashSet<string>(
            new[]
            {
                "Guid",
                "System.Guid",
                "DateTime",
                "System.DateTime",
                "DateTimeOffset",
                "System.DateTimeOffset",
                "TimeSpan",
                "System.TimeSpan",
                "ValueTuple",
                "System.ValueTuple"
            }
        );

        public Field(
            string Name, TypeSyntax Type, SyntaxTriviaList? StructuredTrivia, bool IsNonNullable,
            SyntaxList<AttributeListSyntax> Attributes)
        {
            this.Name = Name;
            this.Type = Type;
            this.StructuredTrivia = StructuredTrivia;
            this.IsNonNullable = IsNonNullable;
            this.Attributes = Attributes;
        }

        public string Name { get; }
        public TypeSyntax Type { get; }
        public SyntaxTriviaList? StructuredTrivia { get; }
        public bool IsNonNullable { get; }
        public SyntaxList<AttributeListSyntax> Attributes { get; }

        public Field With(
            string Name = null, TypeSyntax Type = null, SyntaxTriviaList? StructuredTrivia = null,
            bool? IsNonNullable = null, SyntaxList<AttributeListSyntax>? Attributes = null)
        {
            return new Field(
                Name ?? this.Name, Type ?? this.Type, StructuredTrivia ?? this.StructuredTrivia,
                IsNonNullable ?? this.IsNonNullable, Attributes ?? this.Attributes
            );
        }

        public static bool IsTypeSyntaxNonNullable(TypeSyntax type)
        {
            if (type is PredefinedTypeSyntax predefinedTypeSyntax)
            {
                return NonNullablePredefinedTypes.Contains(predefinedTypeSyntax.Keyword.Kind());
            }

            if (type is IdentifierNameSyntax typeIdentifierNameSyntax)
            {
                return NonNullableTypeName.Contains(typeIdentifierNameSyntax.Identifier.Text);
            }

            return false;
        }

        public static Field MaybeConvertFromMember(MemberDeclarationSyntax member)
        {
            if (member is FieldDeclarationSyntax fieldDeclarationSyntax)
            {
                if (fieldDeclarationSyntax.Modifiers.All(m => m.Kind() != SyntaxKind.StaticKeyword) &&
                    fieldDeclarationSyntax.Modifiers.Any(m => m.Kind() == SyntaxKind.ReadOnlyKeyword) &&
                    fieldDeclarationSyntax.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword) &&
                    fieldDeclarationSyntax.Declaration.Variables.Any())
                {
                    return new Field(
                        fieldDeclarationSyntax.Declaration.Variables.First().Identifier.Text,
                        fieldDeclarationSyntax.Declaration.Type,
                        fieldDeclarationSyntax.HasStructuredTrivia
                            ? fieldDeclarationSyntax.GetLeadingTrivia()
                            : (SyntaxTriviaList?) null,
                        IsTypeSyntaxNonNullable(fieldDeclarationSyntax.Declaration.Type), default
                    );
                }
            }

            if (member is PropertyDeclarationSyntax propertyDeclarationSyntax)
            {
                if (propertyDeclarationSyntax.AccessorList != null && // Expression Bodied properties
                    propertyDeclarationSyntax.AccessorList.Accessors.All(x => x.Body == null) &&
                    propertyDeclarationSyntax.Modifiers.All(m => m.Kind() != SyntaxKind.StaticKeyword) &&
                    propertyDeclarationSyntax.Modifiers.Any(m => m.Kind() == SyntaxKind.PublicKeyword))
                {
                    return new Field(
                        propertyDeclarationSyntax.Identifier.Text, propertyDeclarationSyntax.Type,
                        propertyDeclarationSyntax.HasStructuredTrivia
                            ? propertyDeclarationSyntax.GetLeadingTrivia()
                            : (SyntaxTriviaList?) null, IsTypeSyntaxNonNullable(propertyDeclarationSyntax.Type), default
                    );
                }
            }

            return null;
        }
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSharpRecordsCodeFixProvider))]
    [Shared]
    public class CSharpRecordsCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(
                CSharpRecordsAnalyzer.RecordUpdateDiagnostic.Id, CSharpRecordsAnalyzer.RecordCreateDiagnostic.Id
            );

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start)
                .Parent.AncestorsAndSelf()
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault();
            if (declaration == null)
            {
                throw new ArgumentNullException(nameof(declaration));
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Update record constructor and modifier",
                    c => ApplyToTypeDeclarationInDocument(
                        context.Document, declaration, c, UpdateConstructorAndWithMethod
                    ), "Update record constructor and modifier"
                ), diagnostic
            );

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Update record constructor",
                    c => ApplyToTypeDeclarationInDocument(context.Document, declaration, c, UpdateConstructor),
                    "Update record constructor"
                ), diagnostic
            );
        }

        private async Task<Document> ApplyToTypeDeclarationInDocument(
            Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken,
            Func<TypeDeclarationSyntax, TypeDeclarationSyntax> f)
        {
            var root =
                await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) as CompilationUnitSyntax;
            var newRoot = root.ReplaceNode(typeDeclaration, f(typeDeclaration));
            document = document.WithSyntaxRoot(newRoot);
            return document;
        }

        public static IEnumerable<Field> GetApplicableFields(TypeDeclarationSyntax typeDeclaration)
        {
            var maybePreviousWithMethod = MaybePreviousWithMethod(typeDeclaration);

            var maybePreviousConstructor = MaybePreviousConstructor(typeDeclaration);

            var knownNullableTypeParameterNames =
                maybePreviousWithMethod?.ParameterList.Parameters.Where(param => param.Type is NullableTypeSyntax)
                    .Select(param => param.Identifier.Text) ?? Enumerable.Empty<string>();

            var constructorParameterAttributes =
                maybePreviousConstructor?.ParameterList.Parameters.Select(p => (p.Identifier.Text, p.AttributeLists)) ??
                Enumerable.Empty<(string, SyntaxList<AttributeListSyntax>)>();

            return typeDeclaration.Members.Select(Field.MaybeConvertFromMember)
                .Where(x => x != null)
                .Select(
                    x => {
                        var field = x;
                        field = knownNullableTypeParameterNames.Contains(field.Name)
                            ? field.With(IsNonNullable: !(field.Type is NullableTypeSyntax))
                            : field;

                        var ctorFieldAttrs = constructorParameterAttributes.SingleOrDefault(p => p.Text == field.Name);

                        field = ctorFieldAttrs != default
                            ? field.With(Attributes: ctorFieldAttrs.AttributeLists)
                            : field;

                        return field;
                    }
                )
                .ToList();
        }

        public static TypeDeclarationSyntax UpdateConstructor(TypeDeclarationSyntax typeDeclaration)
        {
            var applicableFields = GetApplicableFields(typeDeclaration);
            return UpdateOrAddConstructor(typeDeclaration, applicableFields);
        }

        public static TypeDeclarationSyntax UpdateConstructorAndWithMethod(TypeDeclarationSyntax typeDeclaration)
        {
            var applicableFields = GetApplicableFields(typeDeclaration).ToList();
            return UpdateOrAddWithMethod(UpdateOrAddConstructor(typeDeclaration, applicableFields), applicableFields);
        }

        public static TypeDeclarationSyntax UpdateOrAddConstructor(
            TypeDeclarationSyntax typeDeclaration, IEnumerable<Field> fields)
        {
            var maybePreviousConstructor = MaybePreviousConstructor(typeDeclaration);

            var constructor = MakeConstructor(typeDeclaration, fields, maybePreviousConstructor);

            return maybePreviousConstructor == null
                ? typeDeclaration.AddMembers(constructor)
                : typeDeclaration.ReplaceNode(maybePreviousConstructor, constructor);
        }

        public static TypeDeclarationSyntax UpdateOrAddWithMethod(
            TypeDeclarationSyntax typeDeclaration, IEnumerable<Field> fields)
        {
            var maybePreviousWithMethod = MaybePreviousWithMethod(typeDeclaration);

            var withMethod = MakeWithMethod(typeDeclaration, fields, maybePreviousWithMethod);

            return maybePreviousWithMethod == null
                ? typeDeclaration.AddMembers(withMethod)
                : typeDeclaration.ReplaceNode(maybePreviousWithMethod, withMethod);
        }

        private static ConstructorDeclarationSyntax MakeConstructor(
            TypeDeclarationSyntax type, IEnumerable<Field> fields,
            ConstructorDeclarationSyntax maybePreviousConstructor)
        {
            var fieldsList = fields.ToList();

            var constructorParameters = SF.ParameterList(
                SF.SeparatedList(
                    fieldsList.Select(
                        field => SF.Parameter(SF.Identifier(field.Name))
                            .WithType(field.Type)
                            .WithAttributeLists(field.Attributes)
                    )
                )
            );

            var constructorBodyStatements = fieldsList.Select(
                field => SF.ExpressionStatement(
                    SF.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SF.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression, SF.ThisExpression(), SF.IdentifierName(field.Name)
                        ), SF.IdentifierName(field.Name)
                    )
                )
            );

            var previousAttributes = maybePreviousConstructor?.AttributeLists ?? new SyntaxList<AttributeListSyntax>();

            var res = SF.ConstructorDeclaration(type.Identifier.Text)
                .WithAttributeLists(previousAttributes)
                .WithModifiers(SF.TokenList(new[] {SF.Token(SyntaxKind.PublicKeyword)}))
                .WithParameterList(constructorParameters)
                .WithBody(SF.Block(constructorBodyStatements));

            res = fieldsList.Any(x => x.StructuredTrivia != null) ? res.WithLeadingTrivia(
                    MakeXmlDocComments(maybePreviousConstructor, fieldsList, Constants.DefaultCtorSummary)
                ) :
                maybePreviousConstructor?.HasLeadingTrivia ?? false ?
                    res.WithLeadingTrivia(maybePreviousConstructor.GetLeadingTrivia()) : res;

            return res;
        }

        private static MethodDeclarationSyntax MakeWithMethod(
            TypeDeclarationSyntax type, IEnumerable<Field> fields, MethodDeclarationSyntax maybePreviousWithMethod)
        {
            var fieldsList = fields.ToList();

            var withMethodParameters = SF.ParameterList(
                SF.SeparatedList(
                    fieldsList.Select(
                        field => SF.Parameter(SF.Identifier(field.Name))
                            .WithType(field.IsNonNullable ? SF.NullableType(field.Type) : field.Type)
                            .WithDefault(
                                SF.EqualsValueClause(
                                    SF.Token(SyntaxKind.EqualsToken),
                                    SF.LiteralExpression(SyntaxKind.NullLiteralExpression)
                                )
                            )
                            .WithAttributeLists(field.Attributes)
                    )
                )
            );

            var withMethodBodyStatements = fieldsList.Select(
                field => SF.Argument(
                    SF.BinaryExpression(
                        SyntaxKind.CoalesceExpression, SF.IdentifierName(field.Name),
                        SF.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression, SF.ThisExpression(), SF.IdentifierName(field.Name)
                        )
                    )
                )
            );

            var previousAttributes = maybePreviousWithMethod?.AttributeLists ?? new SyntaxList<AttributeListSyntax>();

            var typeSyntax = TypeSyntaxFactory.GetTypeSyntax(
                type.Identifier.Text, type.TypeParameterList?.Parameters.Select(x => x.Identifier.Text).ToArray()
            );

            var res = SF.MethodDeclaration(typeSyntax, "With")
                .WithAttributeLists(previousAttributes)
                .WithModifiers(SF.TokenList(new[] {SF.Token(SyntaxKind.PublicKeyword)}))
                .WithParameterList(withMethodParameters)
                .WithBody(
                    SF.Block(
                        SF.ReturnStatement(
                            SF.ObjectCreationExpression(
                                typeSyntax, SF.ArgumentList(SF.SeparatedList(withMethodBodyStatements)), null
                            )
                        )
                    )
                );

            res = fieldsList.Any(x => x.StructuredTrivia != null) ? res.WithLeadingTrivia(
                    MakeXmlDocComments(maybePreviousWithMethod, fieldsList, Constants.DefaultModifierSummary)
                ) :
                maybePreviousWithMethod?.HasLeadingTrivia ?? false ?
                    res.WithLeadingTrivia(maybePreviousWithMethod.GetLeadingTrivia()) : res;

            return res;
        }

        private static SyntaxTriviaList MakeXmlDocComments(
            SyntaxNode maybePrevious, IEnumerable<Field> fields, string defaultSummary)
        {
            var xmlParameters = fields.Where(x => x.StructuredTrivia != null)
                .Select(
                    x => new
                    {
                        Field = x,
                        FieldSummary = x.StructuredTrivia?.Select(i => i.GetStructure())
                            .OfType<DocumentationCommentTriviaSyntax>()
                            .FirstOrDefault()
                            ?.ChildNodes()
                            .OfType<XmlElementSyntax>()
                            .FirstOrDefault(i => i.StartTag.Name.ToString() == "summary")
                    }
                );

            var newXmlParameters = xmlParameters.Select(
                x => x.FieldSummary.WithStartTag(
                        SF.XmlElementStartTag(
                            SF.XmlName("param"),
                            SF.SingletonList((XmlAttributeSyntax) SF.XmlNameAttribute(x.Field.Name))
                        )
                    )
                    .WithEndTag(SF.XmlElementEndTag(SF.XmlName("param")))
            );

            var existingXmlTrivia = maybePrevious?.HasStructuredTrivia == true
                ? maybePrevious.GetLeadingTrivia()
                    .Select(i => i.GetStructure())
                    .OfType<DocumentationCommentTriviaSyntax>()
                    .FirstOrDefault()
                : null;

            var xmlTrivia = existingXmlTrivia ?? SF.DocumentationCommentTrivia(
                    SyntaxKind.SingleLineDocumentationCommentTrivia, SF.List(
                        new XmlNodeSyntax[]
                        {
                            SF.XmlText(" "),
                            SF.XmlSummaryElement(SF.XmlText(defaultSummary))
                        }
                    )
                )
                .WithLeadingTrivia(SF.TriviaList(SF.DocumentationCommentExterior("///")));

            var xmlNodes = xmlTrivia.ChildNodes()
                .OfType<XmlNodeSyntax>()
                .Where(node => !(node is XmlElementSyntax xmlElement) || xmlElement.StartTag.Name.ToString() != "param")
                .ToList();

            var xmlElementsToKeep = xmlNodes.FindLastIndex(x => x is XmlElementSyntax) + 1;
            var xmlElements = xmlNodes.Take(xmlElementsToKeep).ToList();

            return SF.TriviaList(
                SF.LineFeed,
                SF.Trivia(
                    xmlTrivia.WithContent(
                        SF.List(
                            xmlElements.Concat(
                                newXmlParameters.SelectMany(
                                    x => new XmlNodeSyntax[]
                                    {
                                        SF.XmlText("\n"),
                                        x.WithLeadingTrivia(
                                            SF.TriviaList()
                                                .Concat(xmlTrivia.GetLeadingTrivia())
                                                .Concat(new[] {SF.Space})
                                        )
                                    }
                                )
                            )
                        )
                    )
                ), SF.LineFeed
            );
        }

        private static ConstructorDeclarationSyntax MaybePreviousConstructor(TypeDeclarationSyntax typeDeclaration) =>
            typeDeclaration.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();

        private static MethodDeclarationSyntax MaybePreviousWithMethod(TypeDeclarationSyntax typeDeclaration) =>
            typeDeclaration.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.Text == "With");
    }
}
