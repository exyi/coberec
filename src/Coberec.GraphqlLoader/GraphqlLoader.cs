using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.MetaSchema;
using G = GraphQLParser;

namespace Coberec.GraphqlLoader
{
    public class GraphqlLoader
    {
        private static GraphQLParser.AST.GraphQLDocument ParseDocument(GraphQLParser.ISource source) =>
            new GraphQLParser.Parser(new GraphQLParser.Lexer()).Parse(source);

        public static DataSchema LoadFromGraphQL(IEnumerable<(string name, Lazy<string> source)> documents, bool invertNonNull = false)
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

            return CreateDataSchema(c);
        }

        private static DataSchema CreateDataSchema(CollectedDefinitions cds) =>
            new DataSchema(cds.Entities, cds.TypeDefinitions);

        private static GqlResolveResult TransformDeclaration(GraphqlAstResolver resolver, G.AST.ASTNode node)
        {
            if (node is G.AST.GraphQLObjectTypeDefinition obj)
            {
                var typedef = resolver.ProcessObjectType(obj);
                return c => c.TypeDefinitions.Add(typedef);
            }
            else if (node is G.AST.GraphQLInterfaceTypeDefinition ifc)
            {
                var typedef = resolver.ProcessInterface(ifc);
                return c => c.TypeDefinitions.Add(typedef);
            }
            else if (node is G.AST.GraphQLUnionTypeDefinition union)
            {
                var typedef = resolver.ProcessUnion(union);
                return c => c.TypeDefinitions.Add(typedef);
            }
            else if (node is G.AST.GraphQLScalarTypeDefinition scalar)
            {
                var typedef = resolver.ProcessScalarDef(scalar);
                return c => c.TypeDefinitions.Add(typedef);
            }
            else
                throw new NotSupportedException($"Top-level node of type {node.Kind} is not expected.");
        }
    }

    class CollectedDefinitions
    {
        public List<TypeDef> TypeDefinitions = new List<TypeDef>();
        public List<Entity> Entities = new List<Entity>();
    }
    delegate void GqlResolveResult(CollectedDefinitions c);
}
