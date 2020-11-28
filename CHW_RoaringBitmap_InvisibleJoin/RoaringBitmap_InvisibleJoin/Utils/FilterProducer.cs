using System;

namespace RoaringBitmap_InvisibleJoin.Utils
{
    /// <summary>
    /// Takes operation and rhs value and returns predicate. Declared it to keep it simple.
    /// </summary>
    public delegate Predicate<string> FilterProducer(string operation, string otherValue);
}
