using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Coberec.CoreLib
{
    public interface ITraversableObject
    {
        ImmutableArray<string> Properties { get; }
        int PropertyCount { get; }
        object GetValue(int propIndex);
    }

    public static class TraversableObject
    {
        public static ImmutableArray<string> GetProperties<T>(T o)
        {
            if (o is ITraversableObject to)
                return to.Properties;
            else if (o is System.Collections.IList collection)
            {
                if (collection.Count == 0)
                    return ImmutableArray<string>.Empty;
                else
                    return FmtToken.IntegerTokenMap().Take(collection.Count).ToImmutableArray();
            }
            else if (o is IReadOnlyList<object> roCollection)
            {
                if (roCollection.Count == 0)
                    return ImmutableArray<string>.Empty;
                else
                    return FmtToken.IntegerTokenMap().Take(roCollection.Count).ToImmutableArray();
            }
            else return ImmutableArray<string>.Empty;
        }

        public static bool TryGetValue<T>(T o, int propertyIndex, out object value)
        {
            value = null;
            if (o is ITraversableObject to)
            {
                if (to.PropertyCount <= propertyIndex)
                    return false;
                value = to.GetValue(propertyIndex);
                return true;
            }
            else if (o is System.Collections.IList collection)
            {
                if (collection.Count <= propertyIndex)
                    return false;
                value = collection[propertyIndex];
                return true;
            }
            else if (o is IReadOnlyList<object> roCollection)
            {
                if (roCollection.Count <= propertyIndex)
                    return false;
                value = roCollection[propertyIndex];
                return true;
            }
            else
                return false;
        }

        public static int GetPropertyCount<T>(T o)
        {
            if (o is ITraversableObject to)
                return to.PropertyCount;
            else if (o is System.Collections.IList collection)
                return collection.Count;
            else if (o is IReadOnlyList<object> roCollection)
                return roCollection.Count;
            else
                return 0;
        }

        public static object GetValue<T>(T o, int propertyIndex)
        {
            if (TryGetValue(o, propertyIndex, out var value))
                return value;
            else
                throw new IndexOutOfRangeException($"Object {typeof(T)} does not have property #{propertyIndex}, it has only {GetPropertyCount(o)} properties.");
        }

        public static bool TryGetValue<T>(T o, string property, out object value)
        {
            value = null;
            if (o is ITraversableObject to)
            {
                var index = to.Properties.IndexOf(property, StringComparer.Ordinal);
                if (index < 0)
                    return false;

                value = to.GetValue(index);
                return true;
            }
            else if (o is System.Collections.IList collection)
            {
                if (!int.TryParse(property, out var propertyIndex))
                    return false;
                if (collection.Count <= propertyIndex)
                    return false;
                value = collection[propertyIndex];
                return true;
            }
            else if (o is IReadOnlyList<object> roCollection)
            {
                if (!int.TryParse(property, out var propertyIndex))
                    return false;
                if (roCollection.Count <= propertyIndex)
                    return false;
                value = roCollection[propertyIndex];
                return true;
            }
            else return false;
        }

        public static object GetValue<T>(T o, string propertyName)
        {
            if (TryGetValue(o, propertyName, out var value))
                return value;
            else
                throw new IndexOutOfRangeException($"Object {typeof(T)} does not have property '{propertyName}', it has properties: {string.Join(", ", GetProperties(o))}.");
        }

        public static IEnumerable<ImmutableArray<string>> FindObjects<T>(object obj, Func<T, bool> filter, bool recurseInMatches = false)
        {
            var stack = new List<(object current, int propCount, int index)>();

            ImmutableArray<string> getPropertyPath(string current)
            {
                return stack.Select(a => GetProperties(a.current)[a.index]).Append(current).ToImmutableArray();
            }

            object current = obj;
            int propCount = GetPropertyCount(current);
            int i = 0;
            while (true)
            {
                var next = GetValue(current, i);
                var isMatch = next is T nextT && filter(nextT);
                if (isMatch)
                    yield return getPropertyPath(TraversableObject.GetProperties(current)[i]);

                // move to next property
                var childPropCount = GetPropertyCount(next);
                if (childPropCount > 0 && (!isMatch || recurseInMatches))
                {
                    // "recurse"
                    stack.Add((current, propCount, i));
                    i = 0;
                    propCount = childPropCount;
                    current = next;
                }
                else
                {
                    Sakra:
                    if (i < propCount - 1)
                    {
                        // move to the next property, if we can
                        i++;
                    }
                    else if (stack.Any())
                    {
                        // otherwise, unroll the stack
                        var lastFrame = stack.Last();
                        i = lastFrame.index;
                        propCount = lastFrame.propCount;
                        current = lastFrame.current;
                        stack.RemoveAt(stack.Count - 1);
                        goto Sakra;
                    }
                    else
                    {
                        // if nothing is on stack
                        break;
                    }
                }
            }
        }
    }
}
