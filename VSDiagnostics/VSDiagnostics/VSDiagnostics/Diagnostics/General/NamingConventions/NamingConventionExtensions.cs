using System;
using System.Collections.Generic;
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

        public static IEnumerable<SyntaxToken> GetIdentifiers(this SyntaxNode node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.PropertyDeclaration:
                    yield return ((PropertyDeclarationSyntax)node).Identifier;
                    yield break;

                case SyntaxKind.MethodDeclaration:
                    yield return ((MethodDeclarationSyntax)node).Identifier;
                    yield break;

                case SyntaxKind.ClassDeclaration:
                    yield return ((ClassDeclarationSyntax)node).Identifier;
                    yield break;

                case SyntaxKind.StructDeclaration:
                    yield return ((StructDeclarationSyntax)node).Identifier;
                    yield break;

                case SyntaxKind.InterfaceDeclaration:
                    yield return ((InterfaceDeclarationSyntax)node).Identifier;
                    yield break;

                case SyntaxKind.Parameter:
                    yield return ((ParameterSyntax)node).Identifier;
                    yield break;

                case SyntaxKind.FieldDeclaration:
                    foreach (var variable in  ((FieldDeclarationSyntax)node).Declaration.Variables)
                    {
                        yield return variable.Identifier;
                    }
                    yield break;

                case SyntaxKind.LocalDeclarationStatement:
                    foreach (var variable in ((LocalDeclarationStatementSyntax)node).Declaration.Variables)
                    {
                        yield return variable.Identifier;
                    }
                    yield break;

                default:
                    yield break;
            }
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