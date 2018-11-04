using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TrainedMonkey.MetaSchema;
using G=GraphQLParser;
using Kind=GraphQLParser.AST.ASTNodeKind;

namespace TrainedMonkey.GraphqlLoader
{
    static class GraphqlAstResolver
    {
        public static JToken ScalarValueToJson(G.AST.GraphQLScalarValue scalar) =>
                scalar.Kind == Kind.StringValue || scalar.Kind == Kind.EnumValue ? JValue.CreateString(scalar.Value) :
                scalar.Kind == Kind.BooleanValue ? JValue.FromObject(bool.Parse(scalar.Value)) :
                scalar.Kind == Kind.IntValue ? JValue.FromObject(int.Parse(scalar.Value)) :
                scalar.Kind == Kind.FloatValue ? JValue.FromObject(double.Parse(scalar.Value)) :
                scalar.Kind == Kind.NullValue ? JValue.CreateNull() :
                throw new NotSupportedException($"Scalar of type {scalar.Kind} is not supported.");

        public static JToken ValueToJson(G.AST.GraphQLValue v) =>
            v is G.AST.GraphQLScalarValue scalar ? ScalarValueToJson(scalar) :
            v is G.AST.GraphQLListValue list ? new JArray(list.Values.Select(ValueToJson)) :
            v is G.AST.GraphQLObjectValue obj ? (JToken)new JObject(obj.Fields.Select(ObjParamToJson)) :
            throw new NotSupportedException($"Value of type {v.Kind} is not supported");
        public static JProperty ArgToJson(G.AST.GraphQLArgument arg) =>
            new JProperty(arg.Name.Value, ValueToJson(arg.Value));
        public static JProperty ObjParamToJson(G.AST.GraphQLObjectField f) =>
            new JProperty(f.Name.Value, ValueToJson(f.Value));
        public static Directive ProcessDirective(G.AST.GraphQLDirective d) =>
            new Directive(
                d.Name.Value,
                new JObject(d.Arguments.Select(ArgToJson))
            );
        public static TypeField ProcessTypeField(G.AST.GraphQLFieldDefinition f) =>
            f.Arguments.Any() ? throw new NotSupportedException($"Arguments in field definitions are not supported") :
            new TypeField(
                f.Name.Value,
                ProcessTypeName(f.Type),
                description: null,
                f.Directives.Select(ProcessDirective)
            );
        public static TypeRef ProcessTypeName(G.AST.GraphQLType t) =>
            t is G.AST.GraphQLNamedType name ? TypeRef.ActualType(name.Name.Value) :
            t is G.AST.GraphQLListType list ? TypeRef.ListType(ProcessTypeName(list.Type)) :
            t is G.AST.GraphQLNonNullType nullable ? TypeRef.NullableType(ProcessTypeName(nullable.Type)) :
            throw new NotSupportedException($"Type of type {t.Kind} is not supported.");
        public static TypeDef ProcessObjectType(G.AST.GraphQLObjectTypeDefinition obj) =>
            new TypeDef(
                obj.Name.Value,
                obj.Directives.Select(ProcessDirective),
                TypeDefCore.Composite(
                    obj.Fields.Select(ProcessTypeField),
                    obj.Interfaces.Select(ProcessTypeName)
                )
            );
        public static TypeDef ProcessInterface(G.AST.GraphQLInterfaceTypeDefinition ifc) =>
            new TypeDef(
                ifc.Name.Value,
                ifc.Directives.Select(ProcessDirective),
                TypeDefCore.Interface(
                    ifc.Fields.Select(ProcessTypeField)
                )
            );
        public static TypeDef ProcessUnion(G.AST.GraphQLUnionTypeDefinition ifc) =>
            new TypeDef(
                ifc.Name.Value,
                ifc.Directives.Select(ProcessDirective),
                TypeDefCore.Union(
                    ifc.Types.Select(ProcessTypeName)
                )
            );

        public static TypeDef ProcessScalarDef(G.AST.GraphQLScalarTypeDefinition scalar) =>
            new TypeDef(
                scalar.Name.Value,
                scalar.Directives.Select(ProcessDirective),
                TypeDefCore.Primitive()
            );
    }
}
