using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    class ResultTableOptimizer
    {
        /// <summary>
        /// Tables can be merged if they have the same name, and the same count, types and names of columns.
        /// </summary>
        public static bool CanMerge(ResultTable table1, ResultTable table2)
        {
            if (table2.Name != table1.Name)
                return false;

            if (table2.Columns.Length != table1.Columns.Length)
                return false;
            var count = table1.Columns.Length;
            for (var columnIdx = 0; columnIdx < count; columnIdx++)
            {
                var c1 = table1.Columns[columnIdx];
                var c2 = table2.Columns[columnIdx];
                if (c1.Name != c2.Name || c1.ObjectType != c2.ObjectType)
                    return false;
                if (!c1.Parameters.SequenceEqual(c2.Parameters))
                    return false;
            }

            return true;
        }

        /// <summary> Merges a set of result tables. This assumes that CanMerge has been called and returned true for all elements in the list. </summary>
        public static ResultTable MergeTables(IReadOnlyList<ResultTable> tables)
        {
            int columnSize = tables.Sum(x => x.Rows);
            var columns = tables[0].Columns.Select((v, i) =>
            {
                var elem = v.Data.GetType().GetElementType();
                int offset = 0;
                var newA = Array.CreateInstance(elem, columnSize);
                for (var j = 0; j < tables.Count; j++)
                {
                    var newData = tables[j].Columns[i].Data;
                    newData.CopyTo(newA, offset);
                    offset += newData.Length;
                }

                return new ResultColumn(v.Name, newA);
            }).ToArray();
            return new ResultTable(tables[0].Name, columns);
        }
    }
}