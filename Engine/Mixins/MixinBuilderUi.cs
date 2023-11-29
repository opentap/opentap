using System;
using System.ComponentModel;
using System.Linq;

namespace OpenTap
{
    
    internal class MixinBuilderUi : ValidatingObject, IDisplayAnnotation
    {
        
        public IMixinBuilder[] Items { get; }

        [PluginTypeSelector(ObjectSourceProperty = nameof(Items))]
        [Display("Mixin", Order: -10001)]
        public IMixinBuilder SelectedType
        {
            get => SelectedItem;
            set => SelectedItem = value;
        }

        public IMixinBuilder SelectedItem { get; private set; }
        public enum OkCancel { Ok, Cancel }

        [Submit]
        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        public OkCancel Submit { get; set; } = OkCancel.Ok;
        
        public MixinBuilderUi(IMixinBuilder[] items, IMixinBuilder selected = null)
        {
            Items = items;
            SelectedItem = selected;
            if (SelectedItem == null)
            {
                SelectedItem = Items.FirstOrDefault();
            }
            
            { // redirect validation rules.
                foreach (var mixinBuilder in items)
                {
                    if (mixinBuilder is IValidatingObject val)
                    {
                        var type = TypeData.GetTypeData(mixinBuilder);

                        foreach (var rule in val.Rules)
                        {
                            var member = type.GetMember(rule.PropertyName);
                            if (member == null) continue;

                            
                            var transformedName = MixinBuilderUiTypeData.GetTransformedName(member);
                            Rules.Add(() => rule.IsValid(), () => rule.ErrorMessage, transformedName);
                        }
                    }
                }
            }
        }
        
        [Browsable(false)]
        public bool AddMode { get; set; }
        
        string IDisplayAnnotation.Description => AddMode ? "Add a new mixin." : "Configure a mixin.";
        string[] IDisplayAnnotation.Group => Array.Empty<string>();
        string IDisplayAnnotation.Name => AddMode ? "Add Mixin" : $"Modify Mixin '{InitialMixinName ?? string.Empty}'";
        double IDisplayAnnotation.Order => 0.0;
        bool IDisplayAnnotation.Collapsed => false;
        [Browsable(false)]
        public string InitialMixinName
        {
            get;
            set;
        }
    }
}