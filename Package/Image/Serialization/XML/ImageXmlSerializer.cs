using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    internal static class ImageXmlSerializer
    {
        internal static ImageSpecifier DeserializeImageSpecifier(string value)
        {
            TapSerializer tapSerializer = new TapSerializer();
            tapSerializer.AddSerializers(new List<ITapSerializerPlugin>() { new PackageSpecifierSerializerPlugin() });
            return tapSerializer.DeserializeFromString(value, TypeData.FromType(typeof(ImageSpecifier))) as ImageSpecifier;
        }
    }
}
