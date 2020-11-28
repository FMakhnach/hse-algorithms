using System;

namespace RoaringBitmap_InvisibleJoin.Utils
{
    /// <summary>
    /// Builds a string predicate based on operator, rhs value and type converter.
    /// </summary>
    public class FilterBuilder
    {
        public static Predicate<string> Get<T>(
            string operation, string other, Converter<string, T> converter)
            where T : IComparable<T>
        {
            switch (operation)
            {
                case "<":
                    return x => converter(x).CompareTo(converter(other)) < 0;
                case "<=":
                    return x => converter(x).CompareTo(converter(other)) <= 0;
                case ">":
                    return x => converter(x).CompareTo(converter(other)) > 0;
                case ">=":
                    return x => converter(x).CompareTo(converter(other)) >= 0;
                case "=":
                    return x => converter(x).CompareTo(converter(other)) == 0;
                case "<>":
                    return x => converter(x).CompareTo(converter(other)) != 0;
                default:
                    throw new ArgumentException("Wrong operation: " + operation);
            }
        }
    }
}
