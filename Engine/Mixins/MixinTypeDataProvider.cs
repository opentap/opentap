namespace OpenTap
{
    class MixinTypeDataProvider : ITypeDataProvider
    {
        public ITypeData GetTypeData(string identifier)
        {
            return null;
        }

        public ITypeData GetTypeData(object obj)
        {
            if (obj is MixinBuilderUi builder)
            {
                return new MixinBuilderUiTypeData(builder);
            }

            return null;
        }

        public double Priority { get; } = 1;
    }
}