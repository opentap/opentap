using System.Linq;

namespace OpenTap
{
    public class MixinBuilderUi
    {
        public IMixinBuilder[] Items { get; set; }
        [AvailableValues(nameof(Items))]
        public IMixinBuilder SelectedItem { get; set; }
        public MixinBuilderUi(IMixinBuilder[] items)
        {
            this.Items = items;
            SelectedItem = items.FirstOrDefault();
        }
    }
}