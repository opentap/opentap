using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary> Helper class for producing mixin builders. </summary>
    static class MixinFactory
    {
        /// <summary> Creates mixin builders for a type. </summary>
        public static IEnumerable<IMixinBuilder> GetMixinBuilders(ITypeData targetType)
        {
            foreach (var factoryType in TypeData.GetDerivedTypes<IMixinBuilder>())
            {
                if (factoryType.CanCreateInstance == false)
                    continue;
                var types = factoryType.GetAttribute<MixinBuilderAttribute>()?.Types ?? Array.Empty<Type>();
                if (!types.Any(targetType.DescendsTo))
                    continue;
                var instance = (IMixinBuilder)factoryType.CreateInstance();
                yield return instance;
            }
        }
    }
}