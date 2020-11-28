using System.Collections.Generic;

namespace RoaringBitmap_InvisibleJoin.InvisibleJoin.ColumnProcess
{
    /// <summary>
    /// Represents db column. Filters values by predicate and returns all values after number of filtrations.
    /// </summary>
    public interface IColumnProcessor
    {
        void Filter(string operation, string otherValue);

        List<string> GetValues();
    }
}
