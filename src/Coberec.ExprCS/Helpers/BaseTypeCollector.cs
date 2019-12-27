// // Inspired by BaseTypeCollector from ILSpy project
// using System.Collections.Generic;

// namespace Coberec.ExprCS
// {
// 	/// <summary>
// 	/// Helper class for the GetAllBaseTypes() implementation.
// 	/// </summary>
// 	sealed class BaseTypeCollector : List<SpecializedType>
// 	{
// 		readonly Stack<SpecializedType> activeTypes = new Stack<SpecializedType>();
//         private readonly MetadataContext cx;

//         /// <summary>
//         /// If this option is enabled, the list will not contain interfaces when retrieving the base types
//         /// of a class.
//         /// </summary>
//         internal bool SkipImplementedInterfaces;

//         public BaseTypeCollector(MetadataContext cx)
//         {
//             this.cx = cx;
//         }

//         public void CollectBaseTypes(SpecializedType type)
// 		{
// 			// Maintain a stack of currently active type definitions, and avoid having one definition
// 			// multiple times on that stack.
// 			// This is necessary to ensure the output is finite in the presence of cyclic inheritance:
// 			// class C<X> : C<C<X>> {} would not be caught by the 'no duplicate output' check, yet would
// 			// produce infinite output.
// 			if (activeTypes.Contains(type))
// 				return;
// 			activeTypes.Push(type);
// 			// Note that we also need to push non-type definitions, e.g. for protecting against
// 			// cyclic inheritance in type parameters (where T : S where S : T).
// 			// The output check doesn't help there because we call Add(type) only at the end.
// 			// We can't simply call this.Add(type); at the start because that would return in an incorrect order.
			
// 			// Avoid outputting a type more than once - necessary for "diamond" multiple inheritance
// 			// (e.g. C implements I1 and I2, and both interfaces derive from Object)
// 			if (!this.Contains(type)) {
// 				foreach (IType baseType in cx.GetBaseTypes type.DirectBaseTypes) {
// 					if (SkipImplementedInterfaces && def != null && def.Kind != TypeKind.Interface && def.Kind != TypeKind.TypeParameter) {
// 						if (baseType.Kind == TypeKind.Interface) {
// 							// skip the interface
// 							continue;
// 						}
// 					}
// 					CollectBaseTypes(baseType);
// 				}
// 				// Add(type) at the end - we want a type to be output only after all its base types were added.
// 				this.Add(type);
// 				// Note that this is not the same as putting the this.Add() call in front and then reversing the list.
// 				// For the diamond inheritance, Add() at the start produces "C, I1, Object, I2",
// 				// while Add() at the end produces "Object, I1, I2, C".
// 			}
// 			activeTypes.Pop();
// 		}
// 	}
// }
