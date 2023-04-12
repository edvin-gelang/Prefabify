using System.Collections.Generic;

namespace Gelang.Prefabify.Editor
{
    public class TypeComparer<T> : IEqualityComparer<T>
    {
        public static TypeComparer<T> Instance { get; } = new TypeComparer<T>();

        public bool Equals(T x, T y)
        {
            if (x == null || y == null)
                return false;
        
            return x.GetType() == y.GetType();
        }

        public int GetHashCode(T obj)
        {
            return obj.GetType().GetHashCode();
        }
    }
}