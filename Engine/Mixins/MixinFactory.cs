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
                instance.Initialize(targetType);
                yield return instance;
            }
        }
        static readonly TraceSource log = Log.CreateSource("Mixins");
        public static void LoadMixin(object target, IMixinBuilder mixin)
        {

            try
            {
                var mem = mixin.ToDynamicMember(TypeData.GetTypeData(target));
                if (mem == null)
                {
                    if (mixin is IValidatingObject validating && validating.Error is string err && string.IsNullOrEmpty(err) == false)
                    {
                        log.Error($"Unable to load mixin: {err}");
                    }
                    else
                    {
                        log.Error($"Unable to load mixin: {TypeData.GetTypeData(mixin)?.GetDisplayAttribute()?.Name ?? mixin.ToString()}");
                    }
                    return;
                }
                DynamicMember.AddDynamicMember(target, mem);
                mem.SetValue(target, mem.NewInstance());
            }
            catch (Exception e)
            {
                log.Error($"Unable to load mixin: {e.Message}");
                log.Debug(e);
            }
        }
    }
}