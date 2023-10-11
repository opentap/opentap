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
        public static MixinMemberData LoadMixin(object target, IMixinBuilder mixin)
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
                    return null;
                }
                DynamicMember.AddDynamicMember(target, mem);
                mem.SetValue(target, mem.NewInstance());
                return mem;
            }
            catch (Exception e)
            {
                log.Error($"Unable to load mixin: {e.Message}");
                log.Debug(e);
            }
            return null;
        }
        
        // Unload and dispose mixin member data
        public static void UnloadMixin(object src2, MixinMemberData remMember)
        {
            var member = DynamicMember.GetDynamicMembers(src2).FirstOrDefault(x => remMember.Name == x.Name);
            //var member = TypeData.GetTypeData(src2).GetMember(remMember.Name);
            if (member == null) return;
            
            DynamicMember.RemoveDynamicMember(src2, member);
            if (member is MixinMemberData mixin)
                mixin.Dispose();
        }
    }
}