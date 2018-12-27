using System;

namespace Coberec.CoreLib
{
    public readonly struct OptParam<T>
    {
        public readonly T Value;
        public readonly bool HasValue;

        public OptParam(T v)
        {
            this.Value = v;
            this.HasValue = true;
        }

        public static implicit operator OptParam<T>(T val) => new OptParam<T>(val);

        public T ValueOrDefault(T defaultValue) => this.HasValue ? this.Value : defaultValue;
    }
}
