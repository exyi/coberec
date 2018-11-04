using System;
using System.Collections.Generic;
using System.Linq;

namespace TrainedMonkey.MetaSchema
{
    public abstract class TypeRef
    {
        private protected abstract void Seal();

        public static TypeRef ActualType(string typeName) => new ActualTypeCase(typeName);
        public static TypeRef NullableType(TypeRef type) => new NullableTypeCase(type);
        public static TypeRef ListType(TypeRef type) => new ListTypeCase(type);

        public class ActualTypeCase: TypeRef
        {
            private protected override void Seal(){}
            public ActualTypeCase(string typeName)
            {
                TypeName = typeName;
            }
            public string TypeName { get; }
            public override FormatResult Format() => TypeName;
        }
        public class NullableTypeCase: TypeRef
        {
            private protected override void Seal(){}
            public NullableTypeCase(TypeRef type)
            {
                Type = type;
            }

            public TypeRef Type { get; }
            public override FormatResult Format() => FormatResult.Concat(Type.Format(), "!");
        }
        public class ListTypeCase: TypeRef
        {
            private protected override void Seal(){}
            public ListTypeCase(TypeRef type)
            {
                Type = type;
            }

            public TypeRef Type { get; }

            public override FormatResult Format() => FormatResult.Concat("[", Type.Format(), "]");
        }

        public abstract FormatResult Format();
        public override string ToString() => Format().ToString();
    }
}
