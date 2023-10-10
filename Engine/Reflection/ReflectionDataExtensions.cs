//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OpenTap
{
    /// <summary> Helpers for work with ITypeInfo objects. </summary>
    public static class ReflectionDataExtensions
    {
        /// <summary>
        /// Creates an instance of this type using the default constructor.
        /// </summary>
        public static object CreateInstance(this ITypeData type)
        {
            return type?.CreateInstance(Array.Empty<object>());
        }

        /// <summary> returns true if 'type' is a descendant of 'basetype'. </summary>
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool DescendsTo(this ITypeData type, ITypeData basetype)
        {
            if (basetype is TypeData basetype2)
            {
                return DescendsTo(type, basetype2.Type);
            }
            while (type != null)
            {    
                if (object.Equals(type, basetype))
                    return true;
                type = type.BaseType;
            }
            return false;
        }
        /// <summary> returns tru if 'type' is a descendant of 'basetype'. </summary>
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool DescendsTo(this ITypeData type, Type basetype)
        {
            while (type != null)
            {
                if (type is TypeData cst)
                {
                    return cst.Type.DescendsTo(basetype);
                }

                type = type.BaseType;
            }
            return false;
        }

        internal static bool HasAttributeInherited<T>(this IReflectionData mem) where T : class
        {
            if (mem is ITypeData td)
            {
                while (td != null)
                {
                    if (td.HasAttribute<T>()) return true;
                    td = td.BaseType;
                }

                return false;
            }

            throw new ArgumentException("Inherited attributes can only be read from type data instances.");
        }
        
        internal static IEnumerable<T> GetAttributesInherited<T>(this IReflectionData mem) where T : class
        {
            if (mem is ITypeData td)
            {
                IEnumerable<T> result = null;
                while (td != null)
                {
                    var r = td.Attributes?.OfType<T>();
                    if (r?.Any() == true)
                    {
                        if(result != null)
                            result = result.Concat(r);
                        else result = r;
                    }

                    td = td.BaseType;
                }

                return result ?? Array.Empty<T>();
            }

            throw new ArgumentException("Inherited attributes can only be read from type data instances.");
        }

        /// <summary>
        /// Returns true if a reflection info has an attribute of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mem"></param>
        /// <returns></returns>
        static public bool HasAttribute<T>(this IReflectionData mem) where T: class
        {
            return mem.GetAttribute<T>() != null;
        }

        /// <summary> Gets the attribute of type T from mem. </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static T GetAttribute<T>(this IReflectionData mem)
        {
            if(typeof(T) == typeof(DisplayAttribute) && mem is TypeData td)
            {
                return (T)((object)td.Display);
            }

            var attributes = mem.Attributes;
            if (attributes is object[] array)
            {
                // performance optimization: faster iterations if we know its an array.
                foreach (var thing in array)
                    if (thing is T x)
                        return x;
            }
            else
            {
                foreach (var thing in attributes)
                    if (thing is T x)
                        return x;
            }

            return default;
        }

        internal static bool IsBrowsable(this IReflectionData mem)
        {
            if (mem is TypeData td)
            {
                return td.IsBrowsable;
            }
            var attr = mem.GetAttribute<BrowsableAttribute>();
            if (attr is null)
                return true;
            return attr.Browsable;
        }

        /// <summary> Gets all the attributes of type T.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static IEnumerable<T> GetAttributes<T>(this IReflectionData mem)
        {
            var attrs = mem.Attributes;
            bool once = false;
            T first = default;
            if (attrs is object[] attrsArray)
            {
                // performance optimization: faster iterations if we know its an array.
                foreach (var elem in attrsArray)
                {
                    if (elem is T x)
                    {
                        if (once) // multiple instances of T, lets just use OfType.
                            return attrsArray.OfType<T>();
                        once = true;
                        first = x;
                    }
                }
            }
            else
            {
                foreach (var elem in attrs)
                {
                    if (elem is T x)
                    {
                        if (once) // multiple instances of T, lets just use OfType. 
                            return attrs.OfType<T>();
                        once = true;
                        first = x;
                    }
                }
            }

            if (once) return new [] { first };
            return Array.Empty<T>();
        }

        /// <summary> Gets the display attribute of mem. </summary>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static DisplayAttribute GetDisplayAttribute(this IReflectionData mem)
        {
            DisplayAttribute attr;
            if (mem is TypeData td)
                attr = td.Display;
            else
                attr = mem.GetAttribute<DisplayAttribute>();
            if (attr != null) return attr;
            // auto-generate a display attribute.
            return new DefaultDisplayAttribute(mem);
        }

        /// <summary>Gets the help link of 'member'</summary>
        /// <param name="member"></param>
        /// <returns></returns>
        internal static HelpLinkAttribute GetHelpLink(this IReflectionData member)
        {
            var attr = member.GetAttribute<HelpLinkAttribute>();
            if (attr != null)
                return attr;
            if (member is IMemberData meminfo)// Recursively look for class level help.
                return meminfo.DeclaringType.GetHelpLink();
            return null;
        }
    }
}
