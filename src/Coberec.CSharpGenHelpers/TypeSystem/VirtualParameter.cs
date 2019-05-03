using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;

namespace Coberec.CSharpGen.TypeSystem
{
	/// <summary>
	/// Default implementation of <see cref="IParameter"/>.
	/// </summary>
	public sealed class VirtualParameter : IParameter
	{
		readonly IType type;
		readonly string name;
		public readonly List<IAttribute> Attributes;
		readonly bool isRef, isOut, isIn, isParams, isOptional;
		readonly object defaultValue;
		readonly IParameterizedMember owner;
		
		public VirtualParameter(IType type, string name)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			if (name == null)
				throw new ArgumentNullException("name");
			this.type = type;
			this.name = name;
			this.Attributes = new List<IAttribute>();
		}
		
		public VirtualParameter(IType type, string name, IParameterizedMember owner = null, IEnumerable<IAttribute> attributes = null,
		                        bool isRef = false, bool isOut = false, bool isIn = false, bool isParams = false, bool isOptional = false, object defaultValue = null)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			if (name == null)
				throw new ArgumentNullException("name");
			this.type = type;
			this.name = name;
			this.owner = owner;
			this.Attributes = attributes?.ToList() ?? new List<IAttribute>();
			this.isRef = isRef;
			this.isOut = isOut;
			this.isIn = isIn;
			this.isParams = isParams;
			this.isOptional = isOptional;
			this.defaultValue = defaultValue;
		}
		
		SymbolKind ISymbol.SymbolKind {
			get { return SymbolKind.Parameter; }
		}
		
		public IParameterizedMember Owner {
			get { return owner; }
		}
		
		public IEnumerable<IAttribute> GetAttributes() => Attributes;
		
		public bool IsRef {
			get { return isRef; }
		}
		
		public bool IsOut {
			get { return isOut; }
		}

		public bool IsIn {
			get { return isIn; }
		}
		
		public bool IsParams {
			get { return isParams; }
		}
		
		public bool IsOptional {
			get { return isOptional; }
		}
		
		public string Name {
			get { return name; }
		}
		
		public IType Type {
			get { return type; }
		}
		
		bool IVariable.IsConst {
			get { return false; }
		}
		
		public bool HasConstantValueInSignature {
			get { return IsOptional; }
		}
		
		public object ConstantValue {
			get { return defaultValue; }
		}
		
		public override string ToString()
		{
			return ToString(this);
		}
		
		public static string ToString(IParameter parameter)
		{
			StringBuilder b = new StringBuilder();
			if (parameter.IsRef)
				b.Append("ref ");
			if (parameter.IsOut)
				b.Append("out ");
			if (parameter.IsParams)
				b.Append("params ");
			b.Append(parameter.Name);
			b.Append(':');
			b.Append(parameter.Type.ReflectionName);
			if (parameter.IsOptional && parameter.HasConstantValueInSignature) {
				b.Append(" = ");
				if (parameter.GetConstantValue() is object c)
					b.Append(c.ToString());
				else
					b.Append("null");
			}
			return b.ToString();
		}

        public object GetConstantValue(bool throwOnInvalidMetadata = false) => this.ConstantValue;
    }
}
