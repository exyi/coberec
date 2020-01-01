using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Coberec.CSharpGen
{
    public static class FunctionalExtensions
    {
        public static TValue GetValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
            => dictionary[key];

        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                return default(TValue);
            }
            return value;
        }

        public static TTarget ApplyAction<TTarget>(this TTarget target, Action<TTarget> outerAction)
        {
            outerAction(target);
            return target;
        }

        public static TResult Apply<TTarget, TResult>(this TTarget target, Func<TTarget, TResult> outerFunction)
            => outerFunction(target);

        public static T AssignTo<T>(this T a, out T var)
        {
            var = a;
            return a;
        }

        public static T Assert<T>(this T target, Func<T, bool> predicate, string message = "A check has failed")
            => predicate(target) ? target : throw new Exception($"{message} | '{target.ToString()}' checked by {GetDebugFunctionInfo(predicate)}]");

        private static string GetDebugFunctionInfo(Delegate func)
        {
            var fields = func.Target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fieldsFormatted = string.Join("; ", fields.Select(f => f.Name + ": " + f.GetValue(func.Target)?.ToString() ?? "null"));
            return $"'{func.Method.DeclaringType.FullName}.{func.Method.Name}' with closure [{fieldsFormatted}]";
        }

        public static TOut CastTo<TOut>(this object original)
            where TOut : class
            => (TOut)original;

        public static TOut As<TOut>(this object original)
            where TOut : class
            => original as TOut;

        public static IEnumerable<T> SelectRecursively<T>(this IEnumerable<T> enumerable, Func<T, IEnumerable<T>> children)
        {
            foreach (var e in enumerable)
            {
                yield return e;
                foreach (var ce in children(e).SelectRecursively(children))
                    yield return ce;
            }
        }

        public static string StringJoin(this IEnumerable<string> enumerable, string separator) =>
            string.Join(separator, enumerable);

        public static void Deconstruct<K, V>(this KeyValuePair<K, V> pair, out K key, out V value)
        {
            key = pair.Key;
            value = pair.Value;
        }
        public static IEnumerable<(int index, T value)> Indexed<T>(this IEnumerable<T> enumerable) =>
            enumerable.Select((a, b) => (b, a));
        public static (T, U) MakeTuple<T, U>(T a, U b) => (a, b);
        public static IEnumerable<(T, U)> ZipTuples<T, U>(this IEnumerable<T> a, IEnumerable<U> b) => a.Zip(b, MakeTuple);

        public static IEnumerable<T> DistinctBy<T, U>(this IEnumerable<T> a, Func<T, U> key) => a.GroupBy(key).Select(Enumerable.First);

        public static ImmutableDictionary<K, V> TryAdd<K, V>(this ImmutableDictionary<K, V> @this, K key, V val) =>
            @this.ContainsKey(key) ?
            @this :
            @this.Add(key, val);


        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> xs, IEqualityComparer<T> eq) => new HashSet<T>(xs, eq);
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> xs) => new HashSet<T>(xs);


        public static ImmutableArray<U> EagerSelect<T, U>(this ImmutableArray<T> arr, Func<T, U> fn)
        {
            if (arr.IsEmpty) return ImmutableArray<U>.Empty;

            var builder = ImmutableArray.CreateBuilder<U>(initialCapacity: arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                builder.Add(fn(arr[i]));
            }
            return builder.MoveToImmutable();
        }

        public static ImmutableArray<U> EagerSelect<T, U>(this ImmutableArray<T> arr, Func<T, int, U> fn)
        {
            if (arr.IsEmpty) return ImmutableArray<U>.Empty;

            var builder = ImmutableArray.CreateBuilder<U>(initialCapacity: arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                builder.Add(fn(arr[i], i));
            }
            return builder.MoveToImmutable();
        }

        public static ImmutableArray<U> EagerSelect<T, U>(this T[] arr, Func<T, U> fn)
        {
            if (arr.Length == 0) return ImmutableArray<U>.Empty;

            var builder = ImmutableArray.CreateBuilder<U>(initialCapacity: arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                builder.Add(fn(arr[i]));
            }
            return builder.MoveToImmutable();
        }

        public static ImmutableArray<V> EagerZip<T, U, V>(this ImmutableArray<T> arr1, ImmutableArray<U> arr2, Func<T, U, V> fn)
        {
            if (arr1.IsEmpty || arr2.IsEmpty) return ImmutableArray<V>.Empty;
            var len = Math.Min(arr1.Length, arr2.Length);
            var builder = ImmutableArray.CreateBuilder<V>(initialCapacity: len);
            for (int i = 0; i < len; i++)
            {
                builder.Add(fn(arr1[i], arr2[i]));
            }
            return builder.MoveToImmutable();
        }

        public static ImmutableArray<T> EagerSlice<T>(this ImmutableArray<T> arr, int skip = 0, int? take = null)
        {
            if (skip < 0 || take < 0) throw new ArgumentException();
            var take_ = take ?? arr.Length - skip;
            if (skip == 0 && take_ == arr.Length) return arr;

            var builder = ImmutableArray.CreateBuilder<T>(initialCapacity: take_);
            for (int i = 0; i < take_; i++)
            {
                builder.Add(arr[i + skip]);
            }
            return builder.MoveToImmutable();
        }

        public static IReadOnlyList<T> NullIfEmpty<T>(this ImmutableArray<T> array) =>
            array.IsEmpty ? null : (IReadOnlyList<T>)array;

        public static IEnumerable<(T, U)> ZipSelectMany<T, U>(this IEnumerable<T> xs, Func<T, IEnumerable<U>> fn)
        {
            foreach (var x in xs)
            {
                foreach (var y in fn(x))
                    yield return (x, y);
            }
        }
    }
}
