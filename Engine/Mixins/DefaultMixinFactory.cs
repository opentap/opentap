using System.Collections.Generic;

namespace OpenTap
{
    class DefaultMixinFactory : IMixinFactory
    {
        public IEnumerable<IMixinBuilder> GetMixinBuilders(ITypeData targetType)
        {
            yield return new NumberMixinBuilder();
        }
    }
}