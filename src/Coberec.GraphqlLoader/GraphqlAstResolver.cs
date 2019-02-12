using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Coberec.MetaSchema;
using G=GraphQLParser;
using Kind=GraphQLParser.AST.ASTNodeKind;
using GraphQLParser.Exceptions;
using GraphQLParser.AST;
using GraphQLParser;

namespace Coberec.GraphqlLoader
{
    class GraphqlAstResolver
    {
        public readonly ISource Source;
        private readonly bool invertNonNull;

        public GraphqlAstResolver(ISource source, bool invertNonNull)
        {
            this.Source = source;
            this.invertNonNull = invertNonNull;
        }

        static JToken ScalarValueToJson(G.AST.GraphQLScalarValue scalar) =>
                scalar.Kind == Kind.StringValue || scalar.Kind == Kind.EnumValue ? JValue.CreateString(scalar.Value) :
                scalar.Kind == Kind.BooleanValue ? JValue.FromObject(bool.Parse(scalar.Value)) :
                scalar.Kind == Kind.IntValue ? JValue.FromObject(int.Parse(scalar.Value)) :
                scalar.Kind == Kind.FloatValue ? JValue.FromObject(double.Parse(scalar.Value)) :
                scalar.Kind == Kind.NullValue ? JValue.CreateNull() :
                throw new NotSupportedException($"Scalar of type {scalar.Kind} is not supported.");

        static JToken ValueToJson(G.AST.GraphQLValue v) =>
            v is G.AST.GraphQLScalarValue scalar ? ScalarValueToJson(scalar) :
            v is G.AST.GraphQLListValue list ? new JArray(list.Values.Select(ValueToJson)) :
            v is G.AST.GraphQLObjectValue obj ? (JToken)new JObject(obj.Fields.Select(ObjParamToJson)) :
            throw new NotSupportedException($"Value of type {v.Kind} is not supported");
        static JProperty ArgToJson(G.AST.GraphQLArgument arg) =>
            new JProperty(arg.Name.Value, ValueToJson(arg.Value));
        static JProperty ObjParamToJson(G.AST.GraphQLObjectField f) =>
            new JProperty(f.Name.Value, ValueToJson(f.Value));
        public Directive ProcessDirective(G.AST.GraphQLDirective d) =>
            new Directive(
                d.Name.Value,
                new JObject(d.Arguments.Select(ArgToJson))
            );
        public TypeField ProcessTypeField(G.AST.GraphQLFieldDefinition f) =>
            f.Arguments.Any() ? throw Error($"Arguments in field definitions are not supported", f.Arguments.First()) :
            new TypeField(
                f.Name.Value,
                ProcessTypeName(f.Type),
                description: null,
                f.Directives.Select(ProcessDirective)
            );

        TypeRef UnwrapNullability(TypeRef type, ASTNode contextNode) =>
            type is TypeRef.NullableTypeCase nullType ? nullType.Type :
            throw Error("Node can not be non-nullable twice.", contextNode);

        public TypeRef ProcessTypeName(G.AST.GraphQLType t, bool onlyDirect = false)
        {
            if (onlyDirect)
            {
                return
                    t is G.AST.GraphQLNamedType name ? TypeRef.ActualType(name.Name.Value) :
                    throw Error($"Unexpected type expression {t.Kind}.", t);
            }
            else if (this.invertNonNull)
            {
                return
                    t is G.AST.GraphQLNamedType name ? TypeRef.ActualType(name.Name.Value) :
                    t is G.AST.GraphQLListType list ? TypeRef.ListType(ProcessTypeName(list.Type)) :
                    t is G.AST.GraphQLNonNullType nullable ? TypeRef.NullableType(ProcessTypeName(nullable.Type)) :
                    throw Error($"Type of type {t.Kind} is not supported.", t);
            }
            else
            {
                return
                    t is G.AST.GraphQLNamedType name ? TypeRef.NullableType(TypeRef.ActualType(name.Name.Value)) :
                    t is G.AST.GraphQLListType list ? TypeRef.NullableType(TypeRef.ListType(ProcessTypeName(list.Type))) :
                    t is G.AST.GraphQLNonNullType nullable ? UnwrapNullability(ProcessTypeName(nullable.Type), t) :
                    throw Error($"Type of type {t.Kind} is not supported.", t);
            }
        }
        public TypeDef ProcessObjectType(G.AST.GraphQLObjectTypeDefinition obj) =>
            new TypeDef(
                obj.Name.Value,
                obj.Directives.Select(ProcessDirective),
                TypeDefCore.Composite(
                    obj.Fields.Select(ProcessTypeField),
                    obj.Interfaces.Select(i => ProcessTypeName(i, true))
                )
            );
        public TypeDef ProcessInterface(G.AST.GraphQLInterfaceTypeDefinition ifc) =>
            new TypeDef(
                ifc.Name.Value,
                ifc.Directives.Select(ProcessDirective),
                TypeDefCore.Interface(
                    ifc.Fields.Select(ProcessTypeField)
                )
            );
        public TypeDef ProcessUnion(G.AST.GraphQLUnionTypeDefinition ifc) =>
            new TypeDef(
                ifc.Name.Value,
                ifc.Directives.Select(ProcessDirective),
                TypeDefCore.Union(
                    ifc.Types.Select(t => ProcessTypeName(t, true))
                )
            );

        public TypeDef ProcessScalarDef(G.AST.GraphQLScalarTypeDefinition scalar) =>
            new TypeDef(
                scalar.Name.Value,
                scalar.Directives.Select(ProcessDirective),
                TypeDefCore.Primitive()
            );

        internal GraphQLSyntaxErrorException Error(string message, ASTNode contextNode) =>
            new GraphQLSyntaxErrorException(message, Source, contextNode.Location.Start);
    }
}
