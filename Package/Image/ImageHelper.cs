using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    internal static class ImageHelper
    {
        internal static ImageSpecifier GetImageFromString(string value)
        {
            if (value.IsXml())
            {
                return ImageXmlSerializer.DeserializeImageSpecifier(value);
            }
            if (value.IsJson())
            {
                return ImageJsonSerializer.DeserializeImageSpecifier(value);
            }
            if (ParseCommaSeparated(value) is ImageSpecifier r)
                return r;
            throw new FormatException("Value could not be parsed as JSON or XML");
        }

        static bool IsJson(this string jsonData)
        {
            return jsonData.Trim().Substring(0, 1).IndexOfAny(new[] { '[', '{' }) == 0;
        }

        static bool IsXml(this string xmlData)
        {
            return xmlData.Trim().Substring(0, 1).IndexOfAny(new[] { '<' }) == 0;
        }
        static ImageSpecifier ParseCommaSeparated(this string xmlData)
        {
            var pkgStrings = xmlData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            
            var list = new List<PackageSpecifier>();
            foreach (var pkg in pkgStrings)
            {
                var pkgInfo = pkg.Trim().Split(':').Select(x => x.Trim()).ToArray();
                string pkgName = pkgInfo.FirstOrDefault();
                string pkgVersion = pkgInfo.Skip(1).FirstOrDefault() ?? "any";
                if (pkgInfo.Skip(2).Any())
                    return null;
                list.Add(new PackageSpecifier(pkgName, VersionSpecifier.Parse(pkgVersion)));
            }

            if (list.Count == 0) return null;
            return new ImageSpecifier(list);
        }
    }

}
