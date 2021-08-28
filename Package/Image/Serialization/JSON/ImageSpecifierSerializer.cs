using OpenTap.Package;
using System;
using System.Xml.Linq;

namespace OpenTap.Package
{
    internal class ImageSpecifierSerializer : ITapSerializerPlugin
    {
        public double Order => 0;

        public bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if (t is TypeData typeData)
            {
                if (typeData.Type == typeof(PackageSpecifier))
                {
                    if(setter.Target is PackageSpecifier)
                    {

                    }
                    return true;
                }
            }
            return false;
        }

        public bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            throw new NotImplementedException();
        }
    }
}