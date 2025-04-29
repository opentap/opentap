using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace OpenTap.Package;

internal class PluginFileSerializerPlugin : TapSerializerPlugin
{
    private bool Deserialize2(XElement node, ITypeData t, Action<object> setter)
    {
        if (!(node.Name.LocalName == "Plugin" && t.IsA(typeof(PluginFile))))
            return false;
        var plugin = new PluginFile();
        foreach (var attr in node.Attributes())
        {
            switch (attr.Name.LocalName)
            {
                case "Type": plugin.Type = attr.Value; break;
                case "BaseType": plugin.BaseType = attr.Value; break;
            }
        }

        var manufacturersModels = new Dictionary<string, List<string>>();
        foreach (var elm in node.Elements())
        {
            switch (elm.Name.LocalName)
            {
                case "Name": plugin.Name = elm.Value; break;
                case "Order": plugin.Order = double.Parse(elm.Value); break;
                case "Browsable": plugin.Browsable = bool.Parse(elm.Value); break;
                case "Description": plugin.Description = elm.Value; break;
                case "Collapsed": plugin.Collapsed = bool.Parse(elm.Value); break;
                case "Groups":
                    if (elm.HasElements)
                        Serializer.Deserialize(elm, o => plugin.Groups = o as string[], typeof(string[]));
                    else plugin.Groups = [];
                    break;
                case "Manufacturer":
                    var manufacturerName = elm.Attribute("Name").Value;
                    var models = elm.Elements().Select(x => x.Value).ToArray();
                    var lst = manufacturersModels.GetOrCreateValue(manufacturerName, _ => new());
                    lst.AddRange(models);
                    break;
            } 
        }
        plugin.SupportedModels =
            manufacturersModels.Select(kvp => new SupportedModelsAttribute(kvp.Key, kvp.Value.ToArray())).ToArray();

        setter.Invoke(plugin);
        return true; 
    }
    public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
    {
        // This is a workaround for a bug in older versions of OpenTAP which causes issues in upgrade scenarios.
        // When updating OpenTAP itself while running isolated, older versions of OpenTAP will detect that
        // the new version contains a new serializer plugin, which it will attempt to load. However,
        // this new serializer plugin depends on a newer version of `OpenTap.dll`, so using it will cause type load exceptions at runtime.
        try
        {
            return Deserialize2(node, t, setter);
        }
        catch (TypeLoadException)
        {
            // Fall back to the previous serializer logic. This should not cause issues since it will only happen
            // when this dll was loaded by an older version of OpenTAP
            return false;
        }
    }
    
    public static string SerializeDoubleWithRoundtrip(double d)
    {
        // It was decided to use R instead of G17 for readability, although G17 is slightly faster.
        // however, there is a bug in "R" formatting on some .NET versions, that means that 
        // roundtrip actually does not work.
        // See section "Note to Callers:" at https://msdn.microsoft.com/en-us/library/kfsatb94(v=vs.110).aspx
        // so here we format and then parse back to see if it can actually roundtrip.
        // if not, we format with G17.
        var d_str = d.ToString("R", CultureInfo.InvariantCulture);
        var d_re = double.Parse(d_str, CultureInfo.InvariantCulture);
        if (d_re != d) 
            // round trip not possible with R, use G17 instead.
            d_str = d.ToString("G17", CultureInfo.InvariantCulture);
        return d_str;
    }

    public override bool Serialize(XElement node, object obj, ITypeData expectedType)
    {
        if (expectedType.IsA(typeof(PluginFile)) == false) return false;
        foreach (IMemberData prop in expectedType.GetMembers().Where(s => !s.HasAttribute<XmlIgnoreAttribute>()))
        {
            object val = prop.GetValue(obj);
            string name = prop.Name;
            var defaultValueAttr = prop.GetAttribute<DefaultValueAttribute>();
            if (defaultValueAttr != null)
            {
                if (Object.Equals(defaultValueAttr.Value, val))
                    continue;
                if (defaultValueAttr.Value == null)
                {
                    if (val is IEnumerable enu && enu.IsEnumerableEmpty()) // the value is an empty IEnumerable
                    {
                        continue; // We take an empty IEnumerable to be the same as null
                    }
                }
            }

            switch (name)
            {
                case nameof(PluginFile.Type):
                case nameof(PluginFile.BaseType):
                    node.SetAttributeValue(name, val);
                    break;
                case nameof(PluginFile.Order):
                    node.Add(new XElement(name)
                    {
                        Value = SerializeDoubleWithRoundtrip((double)val),
                    });
                    break;
                case nameof(PluginFile.Name):
                case nameof(PluginFile.Browsable):
                case nameof(PluginFile.Description):
                case nameof(PluginFile.Collapsed):
                    node.Add(new XElement(name)
                    {
                        Value = val?.ToString() ?? ""
                    });
                    break;
                case nameof(PluginFile.Groups):
                    var lst = new XElement("Groups");
                    if (val is string[] strs)
                    {
                        foreach (var str in strs)
                        {
                            lst.Add(new XElement("String")
                            {
                                Value = str
                            });
                        }
                    }
                    node.Add(lst);
                    break;
                case nameof(PluginFile.SupportedModels):
                    if (val is SupportedModelsAttribute[] attrs)
                    {
                        var grp = attrs.GroupBy(x => x.Manufacturer);
                        foreach (var g in grp)
                        {
                            var man = new XElement("Manufacturer");
                            man.SetAttributeValue("Name", g.Key);
                            foreach (var model in g.SelectMany(x => x.Models))
                            {
                                var elm = new XElement("Model")
                                {
                                    Value = model
                                };
                                man.Add(elm);
                            }

                            node.Add(man);
                        }
                    }

                    break;
            }
        }

        return true;
    }
}