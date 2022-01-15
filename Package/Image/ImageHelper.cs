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
            else if (value.IsJson())
            {
                return ImageJsonSerializer.DeserializeImageSpecifier(value);

            }
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
    }

}
