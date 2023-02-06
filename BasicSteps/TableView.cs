using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTap.Plugins.BasicSteps
{
    class TableView
    {
        public bool IsPluginItems { get; set; }
        string[] Headers;

        AnnotationCollection[] items;
        AnnotationCollection annotations;

        bool MultiColumns = false;

        TapSerializer serializer = new TapSerializer();

        object stringToObject(string str, ITypeData type)
        {
            var firstChar = str[0];
            if (char.IsDigit(firstChar))
                str = str.Split(' ')[0];

            if (StringConvertProvider.TryFromString(str, type, null, out object result))
                return result;
            try
            {
                return serializer.DeserializeFromString(str, type);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="values"></param>
        /// <param name="types"></param>
        /// <param name="breakOnResize"></param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        public bool SetMatrix(string[][] values, ITypeData[] types, bool breakOnResize)
        {
            var col = annotations.Get<ICollectionAnnotation>();
            var len = values.Length - 1;
            var arr = col.AnnotatedElements.ToList();
            bool elementsChanged = false;
            if (arr.Count > len)
            {
                arr.RemoveRange(len, arr.Count - len);
                elementsChanged = true;
            }
            while (arr.Count < len)
            {
                if (types != null && types[arr.Count + 1] != null && types[arr.Count + 1].CanCreateInstance)
                {
                    var obj = types[arr.Count + 1].CreateInstance(Array.Empty<object>());
                    arr.Add(AnnotationCollection.Annotate(obj));
                }
                else
                {
                    var newElem = col.NewElement();
                    if (newElem.Get<IObjectValueAnnotation>().Value == null)
                    {
                        // if the value is null and the row type is so complex that an instance cannot be created, throw an exception.
                        throw new InvalidDataException("Unable to detect element types.");
                    }

                    arr.Add(newElem);
                }
                elementsChanged = true;
            }

            if (elementsChanged)
            {
                col.AnnotatedElements = arr;

                annotations.Write();
                annotations.Read();
                if (breakOnResize)
                    return true;
            }

            items = col.AnnotatedElements.ToArray();
            var headers = values[0].Select(x => x?.Trim()).ToArray();

            int[] headers1to2 = new int[Headers.Length];

            {  // imported matrix might have different header order
               // than the TableView. 'headers1to2' contains the index for
               // 'Headers' that corresponds to the same column(by name) in 'headers'.

                for (int i = 0; i < Headers.Length; i++)
                {
                    headers1to2[i] = -1;
                    for (int j = 0; j < headers.Length; j++)
                    {
                        if (string.Compare(Headers[i], headers[j]) == 0)
                        {
                            headers1to2[i] = j;
                            break;
                        }
                    }

                    if (headers1to2[i] != -1) continue;

                    for (int j = 0; j < headers.Length; j++)
                    {
                        if (string.Compare(Headers[i], headers[j], true) == 0)
                        {
                            headers1to2[i] = j;
                        }
                    }
                }
            }

            var unfoundHeaders = headers.Where((x, i) => headers1to2.Contains(i) == false);
            if (unfoundHeaders.Any(x => x != null))
            {   // throw an exception if the imported data contains headers not existing in the table.
                throw new InvalidDataException($"Data contains unrecognizable headers: {string.Join(", ", unfoundHeaders)}");
            }

            for (int j = 1; j < values.Length; j++)
            {
                var item = items[j - 1];
                if (MultiColumns)
                {
                    var object2 = item.Get<IMembersAnnotation>().Members;

                    foreach (var mem in object2)
                    {
                        var member = mem.Get<IMemberAnnotation>()?.Member;
                        if (member == null)
                            continue;

                        int index2 = -1;
                        int idx = 0;
                        foreach (var header in Headers)
                        {
                            if (header == member.Name || member.GetDisplayAttribute().GetFullName() == header)
                            {
                                index2 = idx;
                                break;
                            }
                            idx++;
                        }

                        if (index2 == -1) continue;

                        int index3 = headers1to2[index2];
                        if (index3 == -1) continue;

                        var val = mem.Get<IObjectValueAnnotation>();

                        var newval = values[j][index3];
                        if (newval == null) continue;
                        var type = mem.Get<IReflectionAnnotation>().ReflectionInfo;
                        var result = stringToObject(newval, type);
                        if (result != null)
                        {
                            val.Value = result;
                            continue;
                        }

                        if (val is IOwnedAnnotation owned)
                        {
                            owned.Write(mem.Source);
                            mem.Read();
                        }
                        else
                        {
                            mem.Write();
                        }
                    }
                }
                else
                {
                    var newval = values[j][0];
                    if (newval == null) continue;
                    var type = item.Get<IReflectionAnnotation>().ReflectionInfo;
                    var val = item.Get<IObjectValueAnnotation>();
                    var result = stringToObject(newval, type);
                    if (result != null)
                        val.Value = result;
                    else
                        continue;
                    if (val is IOwnedAnnotation owned)
                    {
                        owned.Write(annotations.Source);
                    }
                    else
                    {
                        item.Write();
                    }
                }
                items[j - 1].Write();
            }

            annotations.Write();
            annotations.Read();
            return false;
        }
        public TableView(AnnotationCollection _annotations) : this(_annotations, null)
        {

        }

        public TableView(AnnotationCollection _annotations, ITypeData[] types)
        {
            this.annotations = _annotations;
            var col = annotations.Get<ICollectionAnnotation>();
            if (col == null)
                throw new Exception("Can only work on collection data");
            items = col.AnnotatedElements.ToArray();
            if (items.Length == 0)
            {
                if (types != null)
                {
                    if (types[0] == null)
                        throw new Exception("Unable to create new elements");
                    var obj = types[0].CreateInstance(Array.Empty<object>());
                    items = new[] { AnnotationCollection.Annotate(obj) };
                }
                else
                {
                    items = new[] { col.NewElement() };
                }
                col.AnnotatedElements = items;
                if (items[0] == null)
                    return;
            }
            MultiColumns = items.Any(x => x.Get<IMembersAnnotation>() != null);

            if (MultiColumns)
            {
                HashSet<string> names = new HashSet<string>();
                Dictionary<string, ITypeData> typelookup = new Dictionary<string, ITypeData>();
                Dictionary<string, double> orders = new Dictionary<string, double>();
                Dictionary<string, IMemberData> membersLookup = new Dictionary<string, IMemberData>();

                foreach (var mcol in items)
                {
                    var aggregate = mcol.Get<IMembersAnnotation>();
                    if (aggregate != null)
                    {
                        foreach (var a in aggregate.Members)
                        {
                            var disp = a.Get<DisplayAttribute>();
                            var member = a.Get<IMemberAnnotation>()?.Member;
                            if (member == null)
                                continue;
                            if (disp == null) continue;
                            var name = disp.GetFullName();
                            names.Add(name);
                            typelookup[name] = a.Get<IReflectionAnnotation>().ReflectionInfo;
                            orders[name] = disp.Order;
                            membersLookup[name] = member;
                        }
                    }
                }

                Headers = new string[names.Count];
                IMemberData[] members = new IMemberData[names.Count];
                int index = 0;
                foreach (var name in names.OrderBy(x => x).OrderBy(x => orders[x]).ToArray())
                {
                    Headers[index] = name;
                    members[index] = membersLookup[name];
                    index++;
                }
            }
            else
            {
                Headers = new string[] { "Column" };
            }
        }
    }
}
