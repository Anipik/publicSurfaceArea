using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PublicSurfaceArea
{
    class Program
    {
        static void Main(string[] args)
        {
            string AssemblyName = "System.Runtime";
            string srcdirectoryName = @"C:\git\corefx\src\" + AssemblyName + @"\src\";
            string refdirectoryName = @"C:\git\corefx\src\" + AssemblyName + @"\ref\";

            List<string> reftypes = new List<string>();
            List<string> srcTypes = new List<string>();

            GetEverythingFromDirectory(srcdirectoryName, srcTypes);
            PrintList(srcTypes);
            Console.WriteLine("\nPrinting Types in Ref\n");
            GetEverythingFromDirectory(refdirectoryName, reftypes);
            PrintList(reftypes);
            //File.WriteAllLines(srcdirectoryName + AssemblyName + ".reftypes.cs", reftypes);
            //string typeForwards = WriteAllTypeForwards(reftypes, srcTypes);
            //File.WriteAllText(srcdirectoryName + AssemblyName + ".Forwards.cs", typeForwards);
            //Console.WriteLine("Done");
            Console.ReadLine();   
        }


        private static void GetEverythingFromDirectory(string directoryName, List<string> types)
        {
            var sources = GetListOfFiles(directoryName);
            var syntaxTreeCollection = GetSourceTrees(sources);

            foreach (var tree in syntaxTreeCollection)
            {
                AddTypesFromSyntaxTree(tree, types);
                AddDelegatesFromSyntaxTree(tree, types);
            }
        }

        private static void AddTypesFromSyntaxTree(SyntaxTree tree, List<string> types)
        {
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var allPublicTypes = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(t => HasPublicModifier(t));
                                                                                       
            foreach (var item in allPublicTypes)
            {
                string fullyQualifiedName = GetFullyQualifiedName(item);
                if (!types.Contains(fullyQualifiedName))
                    types.Add(fullyQualifiedName);
            }
        }

        private static void AddDelegatesFromSyntaxTree(SyntaxTree tree, List<string> types)
        {
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var allPublicTypes = root.DescendantNodes().OfType<DelegateDeclarationSyntax>()
                .Where(t => HasPublicModifier(t));

            foreach (var item in allPublicTypes)
            {
                string fullyQualifiedName;
                if (item.Parent is NamespaceDeclarationSyntax)
                {
                    fullyQualifiedName = ((NamespaceDeclarationSyntax)item.Parent).Name.ToFullString().Trim() + "." + GetDelegateTypeName(item);
                }
                else
                {
                    fullyQualifiedName = GetFullyQualifiedName((BaseTypeDeclarationSyntax)item.Parent, item.Identifier.ValueText.Trim());
                }

                if (!types.Contains(fullyQualifiedName))
                    types.Add(fullyQualifiedName);
            }
        }

        private static string GetFullyQualifiedName(BaseTypeDeclarationSyntax node, string nested = "")
        {
            string typeName = GetBaseTypeName(node);
            if (node.Parent is NamespaceDeclarationSyntax)
            {
                string namespaceName = GetNamespaceName((NamespaceDeclarationSyntax)node.Parent);
                string withoutNested = namespaceName + "." + typeName;
                return  string.IsNullOrEmpty(nested) ? withoutNested : withoutNested + "." + nested ;
            }

            return GetFullyQualifiedName((BaseTypeDeclarationSyntax)node.Parent, string.IsNullOrEmpty(nested) ? typeName : typeName + "." + nested);
        }

        private static string GetBaseTypeName(BaseTypeDeclarationSyntax type)
        {
            string typeName = type.Identifier.ValueText;
            
            if (type is TypeDeclarationSyntax)
            {
                var actualType = (TypeDeclarationSyntax)type;
                return GetTypeNameWithTypeParameter(actualType.TypeParameterList, typeName);
            }
            return typeName;
        }

        private static string GetDelegateTypeName(DelegateDeclarationSyntax type)
        {
            string typeName = type.Identifier.ValueText;
            return GetTypeNameWithTypeParameter(type.TypeParameterList, typeName);
        }

        public static string GetTypeNameWithTypeParameter(TypeParameterListSyntax typeParameterList, string identifier)
        {
            return typeParameterList != null ? identifier + "`" + typeParameterList.Parameters.Count : identifier;
        }

        private static void PrintList(List<string> types)
        {
            int i = 0;
            foreach (var item in types)
            {
                Console.WriteLine(i.ToString() + " " + item);
                i++;
            }
        }

        private static string GetNamespaceName(NamespaceDeclarationSyntax namespaceSyntax)
        {
            return namespaceSyntax.Name.ToFullString().Trim();
        }

        private static IEnumerable<SyntaxTree> GetSourceTrees(IEnumerable<string> sourceFiles)
        {
            List<SyntaxTree> result = new List<SyntaxTree>();
            foreach (string sourceFile in sourceFiles)
            {
                if (string.IsNullOrEmpty(sourceFile))
                {
                    continue;
                }
                if (!File.Exists(sourceFile))
                {
                    throw new FileNotFoundException($"File {sourceFile} was not found.");
                }
                string rawText = File.ReadAllText(sourceFile);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(rawText);
                result.Add(tree);
            }
            return result;
        }

        private static bool HasPublicModifier(BaseTypeDeclarationSyntax token)
        {
            return HasPublicModifier(token.Modifiers);
        }

        private static bool HasPublicModifier(DelegateDeclarationSyntax token)
        {
            return HasPublicModifier(token.Modifiers);
        }

        private static bool HasPublicModifier(SyntaxTokenList modifiers)
        {
            foreach (SyntaxToken modifier in modifiers)
            {
                if (modifier.Text == "public")
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<string> GetListOfFiles(string DirectoryName)
        {
            return Directory.EnumerateFiles(DirectoryName, "*", SearchOption.AllDirectories);
        }

        private static string WriteAllTypeForwards(Dictionary<string, string> refTypes, Dictionary<string, string> srcTypes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var key in refTypes.Keys)
            {
                if (!srcTypes.ContainsKey(key))
                    AddTypeForwardToStringBuilder(sb, key);
            }
            return sb.ToString();
        }

        private static void AddTypeForwardToStringBuilder(StringBuilder sb, string typeName)
        {
            sb.Append("[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(" + typeName + "))]\n");
        }

        private static void WriteTypeForwards(string text, string projectNamePath)
        {
            File.WriteAllText(projectNamePath, text);
        }
    }
}
