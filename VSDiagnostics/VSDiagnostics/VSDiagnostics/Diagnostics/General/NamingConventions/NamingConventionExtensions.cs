using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VSDiagnostics.Utilities;

namespace VSDiagnostics.Diagnostics.General.NamingConventions
{
    internal static class NamingConventionExtensions
    {
        public static NamingConvention? GetNamingConvention(this SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.PropertyDeclaration:
                case SyntaxKind.MethodDeclaration:
                case SyntaxKind.ClassDeclaration:
                case SyntaxKind.StructDeclaration:
                    return NamingConvention.UpperCamelCase;

                case SyntaxKind.LocalDeclarationStatement:
                case SyntaxKind.Parameter:
                    return NamingConvention.LowerCamelCase;

                case SyntaxKind.InterfaceDeclaration:
                    return NamingConvention.InterfacePrefixUpperCamelCase;

                case SyntaxKind.FieldDeclaration:
                    var modifiers = ((FieldDeclarationSyntax)node).Modifiers;
                    if (modifiers.Any(SyntaxKind.InternalKeyword) ||
                        modifiers.Any(SyntaxKind.ProtectedKeyword) ||
                        modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        return NamingConvention.UpperCamelCase;
                    }

                    return NamingConvention.UnderscoreLowerCamelCase;

                default:
                    return null;
            }
        }

        public static string GetMemberType(this SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.PropertyDeclaration:
                    return "property";

                case SyntaxKind.MethodDeclaration:
                    return "method";

                case SyntaxKind.ClassDeclaration:
                    return "class";

                case SyntaxKind.StructDeclaration:
                    return "struct";

                case SyntaxKind.LocalDeclarationStatement:
                    return "local";

                case SyntaxKind.Parameter:
                    return "parameter";

                case SyntaxKind.InterfaceDeclaration:
                    return "interface";

                case SyntaxKind.FieldDeclaration:
                    return "field";

                default:
                    return string.Empty;
            }
        }

        public static ImmutableArray<SyntaxToken> GetIdentifiers(this SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.PropertyDeclaration:
                    return ImmutableArray.Create(((PropertyDeclarationSyntax)node).Identifier);

                case SyntaxKind.MethodDeclaration:
                    return ImmutableArray.Create(((MethodDeclarationSyntax)node).Identifier);

                case SyntaxKind.ClassDeclaration:
                    return ImmutableArray.Create(((ClassDeclarationSyntax)node).Identifier);

                case SyntaxKind.StructDeclaration:
                    return ImmutableArray.Create(((StructDeclarationSyntax)node).Identifier);

                case SyntaxKind.InterfaceDeclaration:
                    return ImmutableArray.Create(((InterfaceDeclarationSyntax)node).Identifier);

                case SyntaxKind.Parameter:
                    return ImmutableArray.Create(((ParameterSyntax)node).Identifier);

                case SyntaxKind.FieldDeclaration:
                    return ((FieldDeclarationSyntax)node).Declaration.Variables.ToImmutableArray(variable => variable.Identifier);

                case SyntaxKind.LocalDeclarationStatement:
                    return ((LocalDeclarationStatementSyntax)node).Declaration.Variables.ToImmutableArray(variable => variable.Identifier);

                default:
                    return ImmutableArray<SyntaxToken>.Empty;
            }
        }

        public static IEnumerable<SyntaxNode> GetAncestors(this SyntaxToken token)
        {
            for (var ancestor = token.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                yield return ancestor;
            }
        }

        private static ImmutableArray<TResult> ToImmutableArray<TSource, TResult>(
            this SeparatedSyntaxList<TSource> nodes,
            Func<TSource, TResult> selector) where TSource : SyntaxNode
        {
            var array = new TResult[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                array[i] = selector(nodes[i]);
            }

            return array.ToImmutableArray();
        }

        public static SyntaxToken WithConvention(this SyntaxToken identifier, NamingConvention namingConvention)
        {
            // int @class = 5;
            if (identifier.IsVerbatimIdentifier())
            {
                return identifier;
            }

            // int cl\u0061ss = 5;
            if (identifier.Text.Contains("\\"))
            {
                return identifier;
            }

            var originalValue = identifier.ValueText;
            string newValue;

            switch (namingConvention)
            {
                case NamingConvention.LowerCamelCase:
                    newValue = GetLowerCamelCaseIdentifier(originalValue);
                    break;
                case NamingConvention.UpperCamelCase:
                    newValue = GetUpperCamelCaseIdentifier(originalValue);
                    break;
                case NamingConvention.UnderscoreLowerCamelCase:
                    newValue = GetUnderscoreLowerCamelCaseIdentifier(originalValue);
                    break;
                case NamingConvention.InterfacePrefixUpperCamelCase:
                    newValue = GetInterfacePrefixUpperCamelCaseIdentifier(originalValue);
                    break;
                default:
                    throw new ArgumentException(nameof(namingConvention));
            }

            return SyntaxFactory.Identifier(identifier.LeadingTrivia, newValue, identifier.TrailingTrivia);
        }

        // lowerCamelCase
        internal static string GetLowerCamelCaseIdentifier(string identifier)
        {
            if (ContainsSpecialCharacters(identifier, '_'))
            {
                return identifier;
            }

            var normalizedString = GetNormalizedString(identifier);

            if (normalizedString.Length >= 1)
            {
                return char.ToLower(normalizedString[0]) + normalizedString.Substring(1);
            }
            return identifier;
        }

        // UpperCamelCase
        internal static string GetUpperCamelCaseIdentifier(string identifier)
        {
            if (ContainsSpecialCharacters(identifier, '_'))
            {
                return identifier;
            }

            var normalizedString = GetNormalizedString(identifier);

            if (normalizedString.Length == 0)
            {
                return identifier;
            }
            return char.ToUpper(normalizedString[0]) + normalizedString.Substring(1);
        }

        // _lowerCamelCase
        internal static string GetUnderscoreLowerCamelCaseIdentifier(string identifier)
        {
            if (ContainsSpecialCharacters(identifier, '_'))
            {
                return identifier;
            }

            var normalizedString = GetNormalizedString(identifier);
            if (normalizedString.Length == 0)
            {
                return identifier;
            }

            // Var
            if (char.IsUpper(normalizedString[0]))
            {
                return "_" + char.ToLower(normalizedString[0]) + normalizedString.Substring(1);
            }

            // var
            if (char.IsLower(normalizedString[0]))
            {
                return "_" + normalizedString;
            }

            return normalizedString;
        }

        // IInterface
        internal static string GetInterfacePrefixUpperCamelCaseIdentifier(string identifier)
        {
            if (ContainsSpecialCharacters(identifier, '_'))
            {
                return identifier;
            }

            var normalizedString = GetNormalizedString(identifier);

            if (normalizedString.Length == 0)
            {
                return identifier;
            }

            // iSomething
            if (normalizedString.Length >= 2 && normalizedString[0] == 'i' && char.IsUpper(normalizedString[1]))
            {
                return "I" + normalizedString.Substring(1);
            }

            // Something, something, isomething
            if (normalizedString[0] != 'I')
            {
                return "I" + char.ToUpper(normalizedString[0]) + normalizedString.Substring(1);
            }

            // Isomething
            if (normalizedString[0] == 'I' && char.IsLower(normalizedString[1]))
            {
                return "I" + char.ToUpper(normalizedString[1]) + normalizedString.Substring(2);
            }

            return normalizedString;
        }

        private static string GetNormalizedString(string input)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < input.Length; i++)
            {
                if (char.IsLetter(input[i]) || char.IsNumber(input[i]))
                {
                    sb.Append(input[i]);
                }

                if (input[i] == '_' && i + 1 < input.Length && input[i + 1] != '_')
                {
                    sb.Append(char.ToUpper(input[++i]));
                }
            }
            return sb.ToString();
        }

        private static bool ContainsSpecialCharacters(string input, params char[] allowedCharacters)
        {
            return !input.ToCharArray().All(x => char.IsLetter(x) || char.IsNumber(x) || allowedCharacters.Contains(x));
        }
    }
}