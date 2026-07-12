using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiContextExtractor;

internal static partial class Program
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: AiContextExtractor <repository-root>");
            return 2;
        }

        var root = Path.GetFullPath(args[0]);
        var sourceRoot = Path.Combine(root, "src");
        if (!Directory.Exists(sourceRoot))
        {
            Console.Error.WriteLine($"Source directory not found: {sourceRoot}");
            return 2;
        }

        try
        {
            var files = EnumerateSourceFiles(root, sourceRoot);
            var types = new List<TypeModel>();

            foreach (var file in files)
            {
                var text = File.ReadAllText(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)));
                var tree = CSharpSyntaxTree.ParseText(
                    text,
                    new CSharpParseOptions(
                        languageVersion: LanguageVersion.Preview,
                        documentationMode: DocumentationMode.Parse),
                    path: file);
                var syntaxRoot = tree.GetCompilationUnitRoot();

                foreach (var declaration in syntaxRoot.DescendantNodes().Where(IsTypeDeclaration))
                {
                    types.Add(CreateTypeModel(declaration, file));
                }
            }

            var output = new ExtractionModel(
                SchemaVersion,
                NormalizePath(root),
                files,
                types);

            Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static List<string> EnumerateSourceFiles(string root, string sourceRoot) =>
        Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasExcludedDirectory(path, sourceRoot))
            .Select(path => NormalizePath(Path.GetRelativePath(root, path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static bool HasExcludedDirectory(string path, string sourceRoot)
    {
        var relative = Path.GetRelativePath(sourceRoot, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                         || part.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTypeDeclaration(SyntaxNode node) =>
        node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;

    private static TypeModel CreateTypeModel(SyntaxNode declaration, string file)
    {
        var name = GetTypeName(declaration);
        var @namespace = GetNamespace(declaration);
        var containingType = GetContainingType(declaration);
        var documentation = GetDocumentation(declaration);
        var typeParameters = GetTypeParameters(declaration);
        var primaryConstructorParameters = GetPrimaryConstructorParameters(declaration);

        return new TypeModel(
            Namespace: @namespace,
            ContainingType: containingType,
            Name: name,
            FullName: JoinQualifiedName(@namespace, containingType, name),
            Kind: GetTypeKind(declaration),
            Access: GetEffectiveAccess(GetModifiers(declaration), declaration),
            Modifiers: GetModifierTexts(GetModifiers(declaration)),
            TypeParameters: typeParameters,
            Bases: GetBaseTypes(declaration),
            PrimaryConstructorParameters: primaryConstructorParameters,
            DelegateReturnType: declaration is DelegateDeclarationSyntax delegateDeclaration
                ? Normalize(delegateDeclaration.ReturnType)
                : null,
            DelegateParameters: declaration is DelegateDeclarationSyntax delegateWithParameters
                ? NormalizeParameters(delegateWithParameters.ParameterList.Parameters)
                : null,
            File: file,
            Line: GetLine(declaration),
            Summary: documentation.Summary,
            DescriptionSource: documentation.Source,
            Members: GetDirectMembers(declaration, file));
    }

    private static IReadOnlyList<MemberModel> GetDirectMembers(SyntaxNode declaration, string file)
    {
        if (declaration is EnumDeclarationSyntax enumDeclaration)
        {
            return enumDeclaration.Members
                .Select(member => CreateEnumValue(member, file))
                .ToList();
        }

        if (declaration is not TypeDeclarationSyntax typeDeclaration)
        {
            return [];
        }

        var members = new List<MemberModel>();
        foreach (var member in typeDeclaration.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    members.Add(CreateMethod(method, file));
                    break;
                case ConstructorDeclarationSyntax constructor:
                    members.Add(CreateConstructor(constructor, file));
                    break;
                case DestructorDeclarationSyntax destructor:
                    members.Add(CreateDestructor(destructor, file));
                    break;
                case OperatorDeclarationSyntax @operator:
                    members.Add(CreateOperator(@operator, file));
                    break;
                case ConversionOperatorDeclarationSyntax conversion:
                    members.Add(CreateConversionOperator(conversion, file));
                    break;
                case PropertyDeclarationSyntax property:
                    members.Add(CreateProperty(property, file));
                    break;
                case IndexerDeclarationSyntax indexer:
                    members.Add(CreateIndexer(indexer, file));
                    break;
                case FieldDeclarationSyntax field:
                    members.AddRange(CreateFields(field, file));
                    break;
                case EventDeclarationSyntax @event:
                    members.Add(CreateEvent(@event, file));
                    break;
                case EventFieldDeclarationSyntax eventField:
                    members.AddRange(CreateEventFields(eventField, file));
                    break;
            }
        }

        return members;
    }

    private static MemberModel CreateMethod(MethodDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return CreateCallable(
            kind: "method",
            name: declaration.Identifier.ValueText,
            declaration,
            declaration.Modifiers,
            returnType: Normalize(declaration.ReturnType),
            typeParameters: declaration.TypeParameterList?.Parameters
                .Select(parameter => parameter.Identifier.ValueText)
                .ToList() ?? [],
            parameters: NormalizeParameters(declaration.ParameterList.Parameters),
            explicitInterface: NormalizeExplicitInterface(declaration.ExplicitInterfaceSpecifier),
            file,
            documentation);
    }

    private static MemberModel CreateConstructor(ConstructorDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return CreateCallable(
            kind: "constructor",
            name: declaration.Identifier.ValueText,
            declaration,
            declaration.Modifiers,
            returnType: null,
            typeParameters: [],
            parameters: NormalizeParameters(declaration.ParameterList.Parameters),
            explicitInterface: null,
            file,
            documentation);
    }

    private static MemberModel CreateDestructor(DestructorDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return CreateCallable(
            kind: "destructor",
            name: $"~{declaration.Identifier.ValueText}",
            declaration,
            declaration.Modifiers,
            returnType: null,
            typeParameters: [],
            parameters: NormalizeParameters(declaration.ParameterList.Parameters),
            explicitInterface: null,
            file,
            documentation);
    }

    private static MemberModel CreateOperator(OperatorDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return CreateCallable(
            kind: "operator",
            name: $"operator {declaration.OperatorToken.Text}",
            declaration,
            declaration.Modifiers,
            returnType: Normalize(declaration.ReturnType),
            typeParameters: [],
            parameters: NormalizeParameters(declaration.ParameterList.Parameters),
            explicitInterface: null,
            file,
            documentation);
    }

    private static MemberModel CreateConversionOperator(ConversionOperatorDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return CreateCallable(
            kind: "operator",
            name: $"{declaration.ImplicitOrExplicitKeyword.Text} operator",
            declaration,
            declaration.Modifiers,
            returnType: Normalize(declaration.Type),
            typeParameters: [],
            parameters: NormalizeParameters(declaration.ParameterList.Parameters),
            explicitInterface: null,
            file,
            documentation);
    }

    private static MemberModel CreateCallable(
        string kind,
        string name,
        SyntaxNode declaration,
        SyntaxTokenList modifiers,
        string? returnType,
        IReadOnlyList<string> typeParameters,
        IReadOnlyList<string> parameters,
        string? explicitInterface,
        string file,
        Documentation documentation) =>
        new(
            Kind: kind,
            Name: name,
            Access: GetEffectiveAccess(modifiers, declaration),
            Modifiers: GetModifierTexts(modifiers),
            Type: null,
            ReturnType: returnType,
            TypeParameters: typeParameters,
            Parameters: parameters,
            Accessors: null,
            ExplicitInterface: explicitInterface,
            File: file,
            Line: GetLine(declaration),
            Summary: documentation.Summary,
            DescriptionSource: documentation.Source);

    private static MemberModel CreateProperty(PropertyDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return new MemberModel(
            Kind: "property",
            Name: declaration.Identifier.ValueText,
            Access: GetEffectiveAccess(declaration.Modifiers, declaration),
            Modifiers: GetModifierTexts(declaration.Modifiers),
            Type: Normalize(declaration.Type),
            ReturnType: null,
            TypeParameters: null,
            Parameters: null,
            Accessors: GetAccessors(declaration.AccessorList, declaration.ExpressionBody is not null),
            ExplicitInterface: NormalizeExplicitInterface(declaration.ExplicitInterfaceSpecifier),
            File: file,
            Line: GetLine(declaration),
            Summary: documentation.Summary,
            DescriptionSource: documentation.Source);
    }

    private static MemberModel CreateIndexer(IndexerDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return new MemberModel(
            Kind: "indexer",
            Name: "this",
            Access: GetEffectiveAccess(declaration.Modifiers, declaration),
            Modifiers: GetModifierTexts(declaration.Modifiers),
            Type: Normalize(declaration.Type),
            ReturnType: null,
            TypeParameters: null,
            Parameters: NormalizeParameters(declaration.ParameterList.Parameters),
            Accessors: GetAccessors(declaration.AccessorList, declaration.ExpressionBody is not null),
            ExplicitInterface: NormalizeExplicitInterface(declaration.ExplicitInterfaceSpecifier),
            File: file,
            Line: GetLine(declaration),
            Summary: documentation.Summary,
            DescriptionSource: documentation.Source);
    }

    private static IEnumerable<MemberModel> CreateFields(FieldDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        var kind = declaration.Modifiers.Any(SyntaxKind.ConstKeyword) ? "constant" : "field";
        foreach (var variable in declaration.Declaration.Variables)
        {
            yield return new MemberModel(
                Kind: kind,
                Name: variable.Identifier.ValueText,
                Access: GetEffectiveAccess(declaration.Modifiers, declaration),
                Modifiers: GetModifierTexts(declaration.Modifiers),
                Type: Normalize(declaration.Declaration.Type),
                ReturnType: null,
                TypeParameters: null,
                Parameters: null,
                Accessors: null,
                ExplicitInterface: null,
                File: file,
                Line: GetLine(variable),
                Summary: documentation.Summary,
                DescriptionSource: documentation.Source);
        }
    }

    private static MemberModel CreateEvent(EventDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return new MemberModel(
            Kind: "event",
            Name: declaration.Identifier.ValueText,
            Access: GetEffectiveAccess(declaration.Modifiers, declaration),
            Modifiers: GetModifierTexts(declaration.Modifiers),
            Type: Normalize(declaration.Type),
            ReturnType: null,
            TypeParameters: null,
            Parameters: null,
            Accessors: GetAccessors(declaration.AccessorList, expressionBodied: false),
            ExplicitInterface: NormalizeExplicitInterface(declaration.ExplicitInterfaceSpecifier),
            File: file,
            Line: GetLine(declaration),
            Summary: documentation.Summary,
            DescriptionSource: documentation.Source);
    }

    private static IEnumerable<MemberModel> CreateEventFields(EventFieldDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        foreach (var variable in declaration.Declaration.Variables)
        {
            yield return new MemberModel(
                Kind: "event",
                Name: variable.Identifier.ValueText,
                Access: GetEffectiveAccess(declaration.Modifiers, declaration),
                Modifiers: GetModifierTexts(declaration.Modifiers),
                Type: Normalize(declaration.Declaration.Type),
                ReturnType: null,
                TypeParameters: null,
                Parameters: null,
                Accessors: null,
                ExplicitInterface: null,
                File: file,
                Line: GetLine(variable),
                Summary: documentation.Summary,
                DescriptionSource: documentation.Source);
        }
    }

    private static MemberModel CreateEnumValue(EnumMemberDeclarationSyntax declaration, string file)
    {
        var documentation = GetDocumentation(declaration);
        return new MemberModel(
            Kind: "enumValue",
            Name: declaration.Identifier.ValueText,
            Access: "public",
            Modifiers: [],
            Type: null,
            ReturnType: null,
            TypeParameters: null,
            Parameters: null,
            Accessors: null,
            ExplicitInterface: null,
            File: file,
            Line: GetLine(declaration),
            Summary: documentation.Summary,
            DescriptionSource: documentation.Source);
    }

    private static string GetTypeName(SyntaxNode declaration) => declaration switch
    {
        BaseTypeDeclarationSyntax baseType => baseType.Identifier.ValueText,
        DelegateDeclarationSyntax delegateType => delegateType.Identifier.ValueText,
        _ => throw new ArgumentOutOfRangeException(nameof(declaration))
    };

    private static string GetTypeKind(SyntaxNode declaration) => declaration switch
    {
        ClassDeclarationSyntax => "class",
        InterfaceDeclarationSyntax => "interface",
        StructDeclarationSyntax => "struct",
        RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => "record struct",
        RecordDeclarationSyntax => "record class",
        EnumDeclarationSyntax => "enum",
        DelegateDeclarationSyntax => "delegate",
        _ => throw new ArgumentOutOfRangeException(nameof(declaration))
    };

    private static SyntaxTokenList GetModifiers(SyntaxNode declaration) => declaration switch
    {
        BaseTypeDeclarationSyntax baseType => baseType.Modifiers,
        DelegateDeclarationSyntax delegateType => delegateType.Modifiers,
        _ => default
    };

    private static IReadOnlyList<string> GetTypeParameters(SyntaxNode declaration) => declaration switch
    {
        TypeDeclarationSyntax type => type.TypeParameterList?.Parameters
            .Select(parameter => parameter.Identifier.ValueText)
            .ToList() ?? [],
        DelegateDeclarationSyntax delegateType => delegateType.TypeParameterList?.Parameters
            .Select(parameter => parameter.Identifier.ValueText)
            .ToList() ?? [],
        _ => []
    };

    private static IReadOnlyList<string> GetBaseTypes(SyntaxNode declaration) => declaration switch
    {
        BaseTypeDeclarationSyntax baseType => baseType.BaseList?.Types
            .Select(type => Normalize(type.Type))
            .ToList() ?? [],
        _ => []
    };

    private static IReadOnlyList<string> GetPrimaryConstructorParameters(SyntaxNode declaration) => declaration switch
    {
        TypeDeclarationSyntax type when type.ParameterList is not null =>
            NormalizeParameters(type.ParameterList.Parameters),
        _ => []
    };

    private static string GetNamespace(SyntaxNode declaration)
    {
        var namespaces = declaration.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse()
            .Select(item => Normalize(item.Name));
        return string.Join('.', namespaces);
    }

    private static string? GetContainingType(SyntaxNode declaration)
    {
        var containingTypes = declaration.Ancestors()
            .Where(IsTypeDeclaration)
            .Reverse()
            .Select(GetTypeName)
            .ToList();
        return containingTypes.Count == 0 ? null : string.Join('.', containingTypes);
    }

    private static string JoinQualifiedName(string @namespace, string? containingType, string name) =>
        string.Join('.', new[] { @namespace, containingType, name }.Where(part => !string.IsNullOrWhiteSpace(part))!);

    private static string GetEffectiveAccess(SyntaxTokenList modifiers, SyntaxNode declaration)
    {
        var hasPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);
        var hasProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
        var hasInternal = modifiers.Any(SyntaxKind.InternalKeyword);

        if (modifiers.Any(SyntaxKind.FileKeyword)) return "file";
        if (modifiers.Any(SyntaxKind.PublicKeyword)) return "public";
        if (hasPrivate && hasProtected) return "private protected";
        if (hasProtected && hasInternal) return "protected internal";
        if (hasPrivate) return "private";
        if (hasProtected) return "protected";
        if (hasInternal) return "internal";

        if (declaration is EnumMemberDeclarationSyntax) return "public";

        var isType = IsTypeDeclaration(declaration);
        var containingType = declaration.Ancestors().FirstOrDefault(IsTypeDeclaration);
        if (isType)
        {
            if (containingType is null) return "internal";
            return containingType is InterfaceDeclarationSyntax ? "public" : "private";
        }

        return containingType is InterfaceDeclarationSyntax ? "public" : "private";
    }

    private static IReadOnlyList<string> GetModifierTexts(SyntaxTokenList modifiers) =>
        modifiers.Select(modifier => modifier.Text).ToList();

    private static IReadOnlyList<string> NormalizeParameters(SeparatedSyntaxList<ParameterSyntax> parameters) =>
        parameters.Select(parameter => Normalize(parameter.WithDefault(null))).ToList();

    private static IReadOnlyList<string> GetAccessors(AccessorListSyntax? accessorList, bool expressionBodied)
    {
        if (accessorList is null)
        {
            return expressionBodied ? ["get"] : [];
        }

        return accessorList.Accessors.Select(accessor =>
        {
            var modifiers = GetModifierTexts(accessor.Modifiers);
            return string.Join(' ', modifiers.Append(accessor.Keyword.Text));
        }).ToList();
    }

    private static string? NormalizeExplicitInterface(ExplicitInterfaceSpecifierSyntax? explicitInterface) =>
        explicitInterface is null ? null : Normalize(explicitInterface.Name);

    private static string Normalize(SyntaxNode node) =>
        node.WithoutTrivia().NormalizeWhitespace().ToFullString();

    private static int GetLine(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static Documentation GetDocumentation(SyntaxNode declaration)
    {
        var documentation = declaration.GetLeadingTrivia()
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .LastOrDefault();
        if (documentation is null)
        {
            return Documentation.None;
        }

        var summary = documentation.DescendantNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(element => element.StartTag.Name.LocalName.ValueText.Equals(
                "summary",
                StringComparison.OrdinalIgnoreCase));
        if (summary is not null)
        {
            var text = string.Concat(summary.Content.Select(GetXmlText));
            text = WhitespaceRegex().Replace(text, " ").Trim();
            if (text.Length > 0)
            {
                return new Documentation(text, "xml-summary");
            }
        }

        var inheritsDocumentation = documentation.DescendantNodes()
            .OfType<XmlEmptyElementSyntax>()
            .Any(element => element.Name.LocalName.ValueText.Equals(
                "inheritdoc",
                StringComparison.OrdinalIgnoreCase));
        return inheritsDocumentation
            ? new Documentation(null, "inheritdoc")
            : Documentation.None;
    }

    private static string GetXmlText(XmlNodeSyntax node) => node switch
    {
        XmlTextSyntax text => string.Concat(text.TextTokens.Select(token => token.ValueText)),
        XmlCDataSectionSyntax cdata => string.Concat(cdata.TextTokens.Select(token => token.ValueText)),
        XmlElementSyntax element => string.Concat(element.Content.Select(GetXmlText)),
        XmlEmptyElementSyntax emptyElement => GetEmptyElementText(emptyElement),
        _ => string.Empty
    };

    private static string GetEmptyElementText(XmlEmptyElementSyntax element)
    {
        var name = element.Name.LocalName.ValueText;
        if (name.Equals("see", StringComparison.OrdinalIgnoreCase))
        {
            var cref = element.Attributes.OfType<XmlCrefAttributeSyntax>().FirstOrDefault();
            if (cref is not null) return cref.Cref.ToString();

            var langword = element.Attributes.OfType<XmlTextAttributeSyntax>()
                .FirstOrDefault(attribute => attribute.Name.LocalName.ValueText.Equals(
                    "langword",
                    StringComparison.OrdinalIgnoreCase));
            if (langword is not null)
            {
                return string.Concat(langword.TextTokens.Select(token => token.ValueText));
            }
        }

        if (name is "paramref" or "typeparamref")
        {
            var nameAttribute = element.Attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault();
            if (nameAttribute is not null) return nameAttribute.Identifier.Identifier.ValueText;
        }

        return string.Empty;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record ExtractionModel(
        int SchemaVersion,
        string Root,
        IReadOnlyList<string> Files,
        IReadOnlyList<TypeModel> Types);

    private sealed record TypeModel(
        string Namespace,
        string? ContainingType,
        string Name,
        string FullName,
        string Kind,
        string Access,
        IReadOnlyList<string> Modifiers,
        IReadOnlyList<string> TypeParameters,
        IReadOnlyList<string> Bases,
        IReadOnlyList<string> PrimaryConstructorParameters,
        string? DelegateReturnType,
        IReadOnlyList<string>? DelegateParameters,
        string File,
        int Line,
        string? Summary,
        string DescriptionSource,
        IReadOnlyList<MemberModel> Members);

    private sealed record MemberModel(
        string Kind,
        string Name,
        string Access,
        IReadOnlyList<string> Modifiers,
        string? Type,
        string? ReturnType,
        IReadOnlyList<string>? TypeParameters,
        IReadOnlyList<string>? Parameters,
        IReadOnlyList<string>? Accessors,
        string? ExplicitInterface,
        string File,
        int Line,
        string? Summary,
        string DescriptionSource);

    private sealed record Documentation(string? Summary, string Source)
    {
        public static Documentation None { get; } = new(null, "none");
    }
}
