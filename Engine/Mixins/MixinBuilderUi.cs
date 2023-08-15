using System;
using System.Linq;

namespace OpenTap
{
    [Display("Configure Mixin", Description: "Menu for adding or modifying a mixin.")]
    internal class MixinBuilderUi
    {
        public class TypeDescriber : IDisplayAnnotation
        {
            public override string ToString() => Name;
            public string Description { get; }
            public string[] Group { get; }
            public string Name { get; }
            public double Order { get; }
            public bool Collapsed
            {
                get;
            }
            public TypeDescriber(ITypeData type)
            {
                var display = type.GetDisplayAttribute();
                Name = display.Name;
                Description = display.Description;
                Group = display.Group;
                Order = display.Order;
                Collapsed = display.Collapsed;
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