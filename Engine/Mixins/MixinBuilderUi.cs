using System.Linq;

namespace OpenTap
{
    internal class MixinBuilderUi
    {
        public IMixinBuilder[] Items { get; set; }
        [AvailableValues(nameof(Items))]
        public IMixinBuilder SelectedItem { get; set; }

        public enum OkCancel
        {
            Ok, Cancel
        }

        [Submit]
        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        public OkCancel Submit { get; set; } = OkCancel.Ok;
        
        
        public MixinBuilderUi(IMixinBuilder[] items)
        {
            this.Items = items;
            SelectedItem = items.FirstOrDefault();
        }
    }
}