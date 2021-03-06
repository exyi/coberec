using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Coberec.CoreLib;
using Coberec.MetaSchema;
using G = GraphQLParser;

namespace Coberec.GraphqlLoader
{
    public class GraphqlLoader
    {
        private static GraphQLParser.AST.GraphQLDocument ParseDocument(GraphQLParser.ISource source) =>
            new GraphQLParser.Parser(new GraphQLParser.Lexer()).Parse(source);

        public static (DataSchema schema, Func<string[], string, G.Exceptions.GraphQLSyntaxErrorException> resolveValidationError) LoadFromGraphQL(IEnumerable<(string name, Lazy<string> source)> documents, bool invertNonNull = false)
        {
            var rrs =
                documents
#if !DEBUG
                .AsParallel()
#endif
                .Select(a => new GraphQLParser.Source(a.source.Value, a.name))
                .SelectMany(source => {
                    var doc = ParseDocument(source);
                    var resolver = new GraphqlAstResolver(source, invertNonNull);
                    return doc.Definitions.Select(x => TransformDeclaration(resolver, x));
                });
            var c = new CollectedDefinitions();

            foreach (var rr in rrs) rr(c);

            Func<string[], string, G.Exceptions.GraphQLSyntaxErrorException> validationMapper = (path, message) => {
                var ((p, n), source) = FindSourceLocation(path, c);
                return new G.Exceptions.GraphQLSyntaxErrorException(message, source, n?.Location?.Start ?? 0);
            };

            var schema = CreateDataSchema(c);
            return (schema.Expect(v => new AggregateException(MapErrors(v, validationMapper))), validationMapper);
        }

        public static IEnumerable<G.Exceptions.GraphQLSyntaxErrorException> MapErrors(ValidationErrors errors, Func<string[], string, G.Exceptions.GraphQLSyntaxErrorException> mapper) =>
            errors.EnumerateErrors()
            .Select(e => mapper(e.path, e.error));

        static (ReadOnlyMemory<string> remainingPath, G.AST.ASTNode node) FindSourceLocation_Directive(G.AST.ASTNode nodeWithDirectives, ReadOnlyMemory<string> path)
        {
            Debug.Assert(path.Span[0] == "directives");
            var syntaxDirective = (nodeWithDirectives as G.AST.IHasDirectivesNode)?.Directives.ElementAtOrDefault(int.Parse(path.Span[1]));
            if (syntaxDirective == null) return (path, nodeWithDirectives);
            path = path.Slice(2);
            if (path.Length > 0 && path.Span[0] == "args")
            {
                if (path.Span.Length == 1)
                    return (path.Slice(1), (G.AST.ASTNode)syntaxDirective.Arguments.FirstOrDefault() ?? syntaxDirective);
                else
                {
                    var argument = syntaxDirective.Arguments.FirstOrDefault(d => d.Name.Value == path.Span[1]) ??
                                syntaxDirective.Arguments.ElementAt(int.Parse(path.Span[1]));
                    return (path.Slice(2), argument);
                }
            }
            else if (path.Length > 0 && path.Span[0] == "name")
                return (path.Slice(1), syntaxDirective.Name);
            else return (path, syntaxDirective);
        }

        static (ReadOnlyMemory<string> readOnlyMemory, G.AST.ASTNode node) FindSourceLocation_Fields(G.AST.ASTNode defaultNode, IEnumerable<G.AST.GraphQLFieldDefinition> fieldSyntax, IEnumerable<TypeField> fields, ReadOnlyMemory<string> path)
        {
            Debug.Assert(path.Span[0] == "fields");
            var field = fieldSyntax.ElementAtOrDefault(int.Parse(path.Span[1]));
            if (field == null) return (path, defaultNode);
            path = path.Slice(2);
            if (path.Span[0] == "directives" && path.Length >= 2)
                return FindSourceLocation_Directive(field, path);
            else if (path.Span[0] == "type")
                return (path.Slice(1), field.Type);
            return (path, field);
        }

        static ((ReadOnlyMemory<string> remainingPath, G.AST.ASTNode node), G.ISource source) FindSourceLocation(ReadOnlyMemory<string> path, CollectedDefinitions definitions)
        {
            if (path.Length >= 2 && path.Span[0] == "types")
            {
                var (definition, source, t) = definitions.TypeDefinitions[int.Parse(path.Span[1])];
                path = path.Slice(2);
                if (path.Span[0] == "directives" && path.Length >= 2)
                    return (FindSourceLocation_Directive(t, path), source);
                else if (path.Span[0] == "name")
                    return ((path.Slice(1), ((G.AST.INamedNode)t).Name), source);
                else if (path.Span[0] == "core")
                {
                    path = path.Slice(1);
                    return (definition.Core.Match(
                        primitive: _ => (path, ((G.AST.GraphQLScalarTypeDefinition)t).Name),
                        union: union => {
                            if (path.Span[0] == "options" && path.Length >= 2)
                                return (path.Slice(2), ((G.AST.GraphQLUnionTypeDefinition)t).Types.ElementAt(int.Parse(path.Span[1])));
                            return (path, t);
                        },
                        @interface: ifc => {
                            if (path.Span[0] == "fields" && path.Length >= 2)
                                return FindSourceLocation_Fields(t, ((G.AST.GraphQLInterfaceTypeDefinition)t).Fields, ifc.Fields, path);
                            return (path, t);
                        },
                        composite: cmp => {
                            if (path.Span[0] == "fields" && path.Length >= 2)
                                return FindSourceLocation_Fields(t, ((G.AST.GraphQLObjectTypeDefinition)t).Fields, cmp.Fields, path);
                            if (path.Span[0] == "implements" && path.Length >= 2)
                                return (path.Slice(2), ((G.AST.GraphQLObjectTypeDefinition)t).Interfaces.ElementAt(int.Parse(path.Span[1])));
                            return (path, t);
                        }
                    ), source);
                }
                return ((path, t), source);
            }
            else throw new NotSupportedException($"Could not map path {string.Join(".", path.ToArray())} on graphql schema");
        }

        private static ValidationResult<DataSchema> CreateDataSchema(CollectedDefinitions cds) =>
            DataSchema.Create(cds.Entities, cds.TypeDefinitions.Select(t => t.def));

        private static GqlResolveResult TransformDeclaration(GraphqlAstResolver resolver, G.AST.ASTNode node)
        {
            if (node is G.AST.GraphQLObjectTypeDefinition obj)
            {
                var typedef = resolver.ProcessObjectType(obj);
                return c => c.TypeDefinitions.Add((typedef, resolver.Source, node));
            }
            else if (node is G.AST.GraphQLInterfaceTypeDefinition ifc)
            {
                var typedef = resolver.ProcessInterface(ifc);
                return c => c.TypeDefinitions.Add((typedef, resolver.Source, node));
            }
            else if (node is G.AST.GraphQLUnionTypeDefinition union)
            {
                var typedef = resolver.ProcessUnion(union);
                return c => c.TypeDefinitions.Add((typedef, resolver.Source, node));
            }
            else if (node is G.AST.GraphQLScalarTypeDefinition scalar)
            {
                var typedef = resolver.ProcessScalarDef(scalar);
                return c => c.TypeDefinitions.Add((typedef, resolver.Source, node));
            }
            else
                throw resolver.Error($"Top-level node of type {node.Kind} is not expected.", node);
        }
    }

    class CollectedDefinitions
    {
        public List<(TypeDef def, G.ISource source, G.AST.ASTNode node)> TypeDefinitions = new List<(TypeDef, G.ISource, G.AST.ASTNode)>();
        public List<Entity> Entities = new List<Entity>();
    }
    delegate void GqlResolveResult(CollectedDefinitions c);
}
