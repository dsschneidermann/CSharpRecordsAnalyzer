// *********************************************************************
// Copyright (c). All rights reserved.
// See license file in root dir for details.
// *********************************************************************

using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpRecordsAnalyzer
{
    public static class TypeSyntaxFactory
    {
        /// <summary>
        ///     Used to generate a type without generic arguments
        /// </summary>
        /// <param name="identifier">The name of the type to be generated</param>
        /// <returns>An instance of TypeSyntax from the Roslyn Model</returns>
        public static TypeSyntax GetTypeSyntax(string identifier)
        {
            return SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(identifier));
        }

        /// <summary>
        ///     Used to generate a type with generic arguments
        /// </summary>
        /// <param name="identifier">Name of the Generic Type</param>
        /// <param name="arguments">
        ///     Types of the Generic Arguments, which must be basic identifiers
        /// </param>
        /// <returns>An instance of TypeSyntax from the Roslyn Model</returns>
        public static TypeSyntax GetTypeSyntax(string identifier, params string[] arguments)
        {
            return arguments == null || !arguments.Any()
                ? GetTypeSyntax(identifier)
                : GetTypeSyntax(identifier, arguments.Select(GetTypeSyntax).ToArray());
        }

        /// <summary>
        ///     Used to generate a type with generic arguments
        /// </summary>
        /// <param name="identifier">Name of the Generic Type</param>
        /// <param name="arguments">
        ///     Types of the Generic Arguments, which themselves may be generic types
        /// </param>
        /// <returns>An instance of TypeSyntax from the Roslyn Model</returns>
        public static TypeSyntax GetTypeSyntax(string identifier, params TypeSyntax[] arguments)
        {
            if (arguments == null || !arguments.Any())
            {
                return GetTypeSyntax(identifier);
            }

            return SyntaxFactory.GenericName(
                SyntaxFactory.Identifier(identifier), SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList(
                        arguments.Select(
                            x => {
                                if (x is GenericNameSyntax genX)
                                {
                                    return GetTypeSyntax(
                                        genX.Identifier.ToString(), genX.TypeArgumentList.Arguments.ToArray()
                                    );
                                }

                                return x;
                            }
                        )
                    )
                )
            );
        }
    }
}
