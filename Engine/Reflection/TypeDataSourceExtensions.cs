namespace OpenTap
{
    public static class TypeDataSourceExtensions
    {
        /// <summary>
        /// Returns the type data source for a given ITypeData if possible. For source-less(dynamic) type data it will iterate to base class type.
        /// </summary>
        /// <param name="typeData"></param>
        /// <returns></returns>
        public static ITypeDataSource GetTypeSource(this ITypeData typeData)
        {
            ITypeData it = typeData;
            while (it != null)
            {
                if (it is ITypeDataWithSource src) return src.Source;
                it = it.BaseType;
            }

            return null;
        }
    }
}