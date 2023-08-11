using System;
using System.Linq;

namespace OpenTap
{
    [Display("Select Mixin to add", Description: "Menu for adding or modifying a mixin.")]
    internal class MixinBuilderUi
    {
        public class TypeDescriber
        {
            public override string ToString() => Name;
            public string Name { get; }
            public TypeDescriber(ITypeData type)
            {
                var display = type.GetDisplayAttribute();
                Name = display.Name;
            }
        }
        
        TypeDescriber selectedType;
        public IMixinBuilder[] Items { get; }
        public TypeDescriber[] ItemTypes { get; }

        [AvailableValues(nameof(ItemTypes))]
        [Display("Mixin", Order: -10001)]
        public TypeDescriber SelectedType
        {
            get => selectedType;
            set
            {
                selectedType = value;
                var idx = Array.IndexOf(ItemTypes, value);
                SelectedItem = Items[idx];
            }
        }

        public IMixinBuilder SelectedItem { get; private set; }
        public enum OkCancel { Ok, Cancel }

        [Submit]
        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        public OkCancel Submit { get; set; } = OkCancel.Ok;
        
        public MixinBuilderUi(IMixinBuilder[] items, IMixinBuilder selected = null)
        {
            Items = items;
            ItemTypes = items.Select(x => new TypeDescriber(TypeData.GetTypeData(x))).ToArray();
            SelectedType = ItemTypes.First();
            if (selected != null)
                SelectedType = ItemTypes[Array.IndexOf(items, selected)];
        }
    }
}