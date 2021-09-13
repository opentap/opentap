using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    internal class JsonPackageSpecifierConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {

        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            if (jObject["Name"] is null)
                throw new InvalidOperationException("Package name cannot be null!");

            string name = jObject["Name"].Value<string>();
            VersionSpecifier version = jObject["Version"] == null ? VersionSpecifier.Parse("") : VersionSpecifier.Parse(jObject["Version"].Value<string>());
            CpuArchitecture architecture = string.IsNullOrEmpty(jObject["Architecture"].ToString()) ? CpuArchitecture.Unspecified : (CpuArchitecture)Enum.Parse(typeof(CpuArchitecture), jObject["Architecture"].Value<string>());
            string os = jObject["OS"] == null ? null : jObject["OS"].Value<string>();

            return new PackageSpecifier(name, version, architecture, os);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PackageSpecifier);
        }
    }
}
