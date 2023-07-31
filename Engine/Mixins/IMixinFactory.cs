using System.Collections.Generic;

namespace OpenTap
{
    public interface IMixinFactory : ITapPlugin
    {
        IEnumerable<IMixinBuilder> GetMixinBuilders(ITypeData targetType);
    }
}