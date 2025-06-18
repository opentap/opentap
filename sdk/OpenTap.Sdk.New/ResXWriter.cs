using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Sdk.New;

class ResXWriter
{
    private readonly Dictionary<string, string> keys = [];
    private readonly string filename;

    public ResXWriter(string filename)
    {
        this.filename = filename;
    }

    public void AddResource(string name, double d)
    { 
        var d_str = d.ToString("R", CultureInfo.InvariantCulture);
        var d_re = double.Parse(d_str, CultureInfo.InvariantCulture);
        if (d_re != d) 
            // round trip not possible with R, use G17 instead.
            d_str = d.ToString("G17", CultureInfo.InvariantCulture);
        
        AddResource(name, d_str);
    }
    
    public void AddResource(string name, string value)
    {
        keys[name] = value;
    }

    public void Generate()
    {
        var document = new XDocument();
        var root = new XElement("root");
        document.Add(root);

        foreach (var kvp in keys.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
        {
            var ele = new XElement("data");
            ele.SetAttributeValue("name", kvp.Key);
            ele.SetAttributeValue(XNamespace.Xml + "space", "preserve"); 
            ele.Add(new XElement("value", kvp.Value));
            root.Add(ele);
        }
        
        document.Save(filename, SaveOptions.None);
    }
}