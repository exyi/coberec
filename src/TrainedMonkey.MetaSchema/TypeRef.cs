using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.MetaSchema
{
    public abstract class TypeRef
    {
            //                    ___
            //                  //   \\
            //                 ||=====||
            //                  \\___//
            //                   ./O
            //               ___/ //|\\
            //              / o    /}
            //             (       /
            //             \      /
            //             |     (
            //             |      \
            //             )       \
            //            /         \
            //          /            )
            //        /              |
            //      //             / /
            //    /       ___(    ,| \
            //  /       /    \     |  \
            // (      /  /   /\     \  \
            // \\   /___ _-_//'|     |  |
            //  \\_______-/     \     \  \
            //                   \-_-_-_-_-
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
            public override bool Equals(object obj) =>
                obj is ActualTypeCase o && this.TypeName.Equals(o.TypeName);
            public override int GetHashCode() => (TypeName, 67464).GetHashCode();
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
            public override bool Equals(object obj) =>
                obj is NullableTypeCase o && this.Type.Equals(o.Type);
            public override int GetHashCode() => (Type.GetHashCode(), 76545476).GetHashCode();
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
            public override bool Equals(object obj) =>
                obj is ListTypeCase o && this.Type.Equals(o.Type);
            public override int GetHashCode() => (Type.GetHashCode(), 3564364).GetHashCode();
        }

        public T Match<T>(Func<ActualTypeCase, T> actual,
                          Func<NullableTypeCase, T> nullable,
                          Func<ListTypeCase, T> list) =>
            this is ActualTypeCase a ? actual(a) :
            this is NullableTypeCase n ? nullable(n) :
            this is ListTypeCase l ? list(l) :
            throw new Exception("wtf");

        public abstract FormatResult Format();
        public override string ToString() => Format().ToString();
    }
}
