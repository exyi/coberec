using System;
using System.Collections.Generic;
using System.Linq;
using TrainedMonkey.MetaSchema;
using G = GraphQLParser;

namespace TrainedMonkey.GraphqlLoader
{
    public class GraphqlLoader
    {
        private static GraphQLParser.AST.GraphQLDocument ParseDocument(string name, string source) =>
            new GraphQLParser.Parser(new GraphQLParser.Lexer())
            .Parse(new GraphQLParser.Source(source, name));

        public static DataSchema LoadFromGraphQL(IEnumerable<(string name, Lazy<string> source)> documents)
        {
            var rrs =
                documents.AsParallel()
                .Select(a => ParseDocument(a.name, a.source.Value))
                .SelectMany(doc => doc.Definitions)
                .Select(TransformDeclaration);
            var c = new CollectedDefinitions();

            foreach (var rr in rrs) rr(c);

            return CreateDataSchema(c);
        }

        private static DataSchema CreateDataSchema(CollectedDefinitions cds) =>
            new DataSchema(cds.Entities, cds.TypeDefinitions);

        private static GqlResolveResult TransformDeclaration(G.AST.ASTNode node)
        {
            if (node is G.AST.GraphQLObjectTypeDefinition obj)
            {
                var typedef = GraphqlAstResolver.ProcessObjectType(obj);
                return c => c.TypeDefinitions.Add(typedef);
            }
            else if (node is G.AST.GraphQLInterfaceTypeDefinition ifc)
            {
                var typedef = GraphqlAstResolver.ProcessInterface(ifc);
                return c => c.TypeDefinitions.Add(typedef);
            }
            else if (node is G.AST.GraphQLUnionTypeDefinition union)
            {
                var typedef = GraphqlAstResolver.ProcessUnion(union);
                return c => c.TypeDefinitions.Add(typedef);
            }
            else if (node is G.AST.GraphQLScalarTypeDefinition scalar)
            {
                var typedef = GraphqlAstResolver.ProcessScalarDef(scalar);
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
