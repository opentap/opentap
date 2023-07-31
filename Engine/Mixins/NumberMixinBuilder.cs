namespace OpenTap
{
    class NumberMixinBuilder : IMixinBuilder
    {
        public string Name
        {
            get;
            set;
        } = "Number";
        
        public IMixin CreateInstance()
        {
            return new NumberMixin();
        }
    }
}