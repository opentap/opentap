using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    internal static class ImageJsonSerializer
    {
        internal static JsonSerializerSettings JsonSettings { get; set; }

        static ImageJsonSerializer()
        {
            JsonSettings = new JsonSerializerSettings();
            JsonSettings.Converters.Add(new StringEnumConverter());
            JsonSettings.Converters.Add(new SemanticVersionConverter());
        }

        internal static string JsonSerializePackages(List<IPackageIdentifier> packageList)
        {
            return JsonConvert.SerializeObject(packageList, JsonSettings);
        }

        internal static ImageSpecifier DeserializeImageSpecifier(string value)
        {
            return JsonConvert.DeserializeObject(value, typeof(ImageSpecifier), new JsonPackageSpecifierConverter()) as ImageSpecifier;
        }
    }
}
