using System;
using System.Collections.Generic;

namespace OpenTap
{
    public class MixinFactory
    {
        public static IEnumerable<IMixinBuilder> GetMixinBuilders(ITypeData targetType)
        {
            foreach (var factoryType in TypeData.GetDerivedTypes<IMixinFactory>())
            {
                if (factoryType.CanCreateInstance == false)
                    continue;
                var factory = (IMixinFactory)factoryType.CreateInstance();
                foreach (var item in factory.GetMixinBuilders(targetType) ?? Array.Empty<IMixinBuilder>())
                {
                    yield return item;
                }
            }
        }
    }
}