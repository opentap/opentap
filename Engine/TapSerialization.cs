//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using OpenTap.Plugins;

namespace OpenTap
{

    /// <summary> Enables callback from the OpenTAP deserializer after deserialization. </summary>
    public interface IDeserializedCallback
    {
        /// <summary>
        /// Called when the object has been deserialized.
        /// </summary>
        void OnDeserialized();
    }

    /// <summary>
    /// Can be used to control the order in which members are deserialized.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DeserializeOrderAttribute : Attribute
    {
        /// <summary> The order in which the member will be deserialized. Higher order, means it will be deserialized later. Minimum value is 0, which is also the default order of not attributed members.</summary>
        public double Order { get; }

        /// <summary>
        /// Can be used to control the order in which members are deserialized.
        /// </summary>
        /// <param name="order">The order in which the member will be deserialized. Higher order, means it will be deserialized later. Minimum value is 0, which is also the default order of not attributed members.</param>
        public DeserializeOrderAttribute(double order)
        {
            Order = order;
        }
    }

    /// <summary>
    /// Serializing/deserializing OpenTAP objects. This class mostly just orchestrates a number of serializer plugins. <see cref="MacroString"/>
    /// </summary>
    public class TapSerializer
    {
        /// <summary>
        /// Default settings for XmlWriter.
        /// </summary>
        public static readonly XmlWriterSettings DefaultWriterSettings =
            new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };

        /// <summary>
        /// Default settings for XmlReaders.
        /// </summary>
        public static readonly XmlReaderSettings DefaultReaderSettings =
            new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true };

        

        /// <summary>
        /// Pushes a message to the list of errors for things that happened during load.
        /// </summary>
        /// <param name="element">The element that generated the error. </param>
        /// <param name="message"></param>
        public void PushError(XElement element, string message)
        {
            var lineinfo = element as IXmlLineInfo;
            if (lineinfo.HasLineInfo())
            {
                errors.Add(string.Format("XML Line {0}: {1}", lineinfo.LineNumber, message));
            }
            else
            {
                errors.Add(message);
            }
            
        }

        /// <summary>
        /// Deserializes an object from a XDocument.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="type"></param>
        /// <param name="autoFlush"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public object Deserialize(XDocument document, ITypeData type = null, bool autoFlush = true, string path = null)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            var node1 = document.Elements().First();
            object serialized = null;
            this.ReadPath = path;
            var prevSer = currentSerializer.Value;
            currentSerializer.Value = this;
            try
            {
                Deserialize(node1, x => serialized = x, type);
            }
            finally
            {
                currentSerializer.Value = prevSer;
            }
            if (autoFlush)
                Flush();
            return serialized;
        }

        /// <summary>
        /// Needed by defered loading. Only required to be called if autoFlush is set to false during deserialization.
        /// </summary>
        public void Flush()
        {
            while (deferedLoads.Count > 0)
                deferedLoads.Dequeue()();
        }

        /// <summary>
        /// Deserializes an object from a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="flush"></param>
        /// <param name="type"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public object Deserialize(Stream stream, bool flush = true, ITypeData type = null, string path = null)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            object obj = Deserialize(XDocument.Load(stream, LoadOptions.SetLineInfo), type: type, autoFlush: flush, path: path);
            var rs = GetSerializer<OpenTap.Plugins.ResourceSerializer>();
            if (rs.TestPlanChanged)
            {
                log.Warning("Test Plan changed due to resources missing from Bench settings.");
                log.Warning("Please review these changes before saving or running the Test Plan.");
            }
            return obj;
        }

        /// <summary>
        /// Deserializes an object from an xml text string.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="type"></param>
        /// <param name="flush"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public object DeserializeFromString(string text, ITypeData type = null, bool flush = true, string path = null)
        {
            if (text == null)
                throw new ArgumentNullException("text");
            using (var reader = new StringReader(text))
                return Deserialize(XDocument.Load(reader, LoadOptions.SetLineInfo), type: type, autoFlush: flush, path: path);
        }

        /// <summary>
        /// Deserializes an object from a XML file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="type"></param>
        /// <param name="flush"></param>
        /// <returns></returns>
        public object DeserializeFromFile(string file, ITypeData type = null, bool flush = true)
        {
            if (file == null)
                throw new ArgumentNullException("file");
            return Deserialize(XDocument.Load(file, LoadOptions.SetLineInfo), type: type, autoFlush: flush, path: file);
        }

        List<ITapSerializerPlugin> serializers = new List<ITapSerializerPlugin>();
        readonly Stack<object> activeSerializers = new Stack<object>(32);
        /// <summary>
        /// The stack of serializers. Changes during serialization depending on the order of serializers used.
        /// </summary>
        public IEnumerable<ITapSerializerPlugin> SerializerStack => activeSerializers.OfType<ITapSerializerPlugin>();

        /// <summary>
        /// True if errors should be ignored.
        /// </summary>
        public bool IgnoreErrors { get; set; } = false;
        
        /// <summary>
        /// Gets a serializer from the stack of active serializers. Returns null if there is no serializer of that type on the stack.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetSerializer<T>() where T : ITapSerializerPlugin
        {
            return serializers.OfType<T>().FirstOrDefault();
        }

        /// <summary> Adds new serializers to the serializer. Will insert them based on the order property. </summary>
        /// <param name="_serializers"></param>
        public void AddSerializers(IEnumerable<ITapSerializerPlugin> _serializers)
        {
            serializers.AddRange(_serializers);
            serializers.Sort((x, y) => -x.Order.CompareTo(y.Order));
        }

        static System.Threading.ThreadLocal<TapSerializer> currentSerializer = new System.Threading.ThreadLocal<TapSerializer>();
        
        /// <summary> The serializer currently serializing/deserializing an object.</summary>
        public static TapSerializer GetCurrentSerializer() => currentSerializer.Value;
        /// <summary>
        /// Creates a new serializer instance.
        /// </summary>
        public TapSerializer()
        {
            var previousValue = currentSerializer.Value;
            currentSerializer.Value = this;

            var plugins = PluginManager.GetPlugins<ITapSerializerPlugin>();
            AddSerializers(plugins.Select(x =>
            {
                try
                {
                    return (ITapSerializerPlugin)Activator.CreateInstance(x);
                }
                catch
                {
                    return null;
                }
            }).Where(x => x!=null));

            currentSerializer.Value = previousValue;
        }

        Queue<Action> deferedLoads = new Queue<Action>();

        /// <summary>
        /// Pushes a deferred load action onto a queue of deferred loads.  
        /// </summary>
        /// <param name="deferedLoad"></param>
        public void DeferLoad(Action deferedLoad)
        {
            if (deferedLoad == null)
                throw new ArgumentNullException("deferedLoad");
            deferedLoads.Enqueue(deferedLoad);
        }

        List<string> errors = new List<string>();

        /// <summary> Get the errors associated with deserialization. </summary>
        public IEnumerable<string> Errors
        {
            get { return errors.AsEnumerable(); }
        }

        static TraceSource log = Log.CreateSource("Serializer");

        /// <summary>
        /// Deserializes an object from an XElement. Calls the setter action with the result. returns true on success. Optionally, the type can be added.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="setter"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool Deserialize(XElement element, Action<object> setter, Type t = null)
        {
            return Deserialize(element, setter, t != null ? TypeData.FromType(t) : null);
        }

        /// <summary>
        /// Deserializes an object from XML.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="setter"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool Deserialize(XElement element, Action<object> setter, ITypeData t)
        {
            var typeattribute = element.Attribute("type");
            if (typeattribute != null)
            {   // if a specific type is given by the element use that.
                // If it cannot be found fall back on previous value.
                // This can happen if LocateType cannot find it, eg. private type.
                var t2 = TypeData.GetTypeData(typeattribute.Value);
                
                if (t2 != null)
                {
                    t = t2;
                }
                else
                {
                    var message = string.Format("Unable to locate type '{0}'. Are you missing a plugin?", typeattribute.Value);
                    log.Warning(element, message);
                    PushError(element, message);
                    if (t == null)
                        return false;
                }
            }
            if (t == null) t = TypeData.FromType(typeof(object));
            foreach (var serializer in serializers)
            {
                try
                {
                    activeSerializers.Push(serializer);
                    if (serializer is ITapSerializerPlugin ser2)
                    {
                        if (ser2.Deserialize(element, t, setter))
                            return true;
                    }
                }
                finally
                {
                    activeSerializers.Pop();
                }
            }


            return false;
        }

        /// <summary>
        /// Serialize an object to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="obj"></param>
        public void Serialize(Stream stream, object obj)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            using (var writer = XmlWriter.Create(stream, DefaultWriterSettings))
                this.Serialize(writer, obj);
        }

        /// <summary>
        /// Serializes an object to a XML writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="obj"></param>
        public void Serialize(XmlWriter writer, object obj)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");
            XDocument doc = new XDocument();
            XElement elem = new XElement("root");
            if(obj != null)
                elem.Name = TapSerializer.TypeToXmlString(obj.GetType());
            Serialize(elem, obj);
            doc.Add(elem);
            doc.WriteTo(writer);
        }

        /// <summary>
        /// Serializes an object to a string.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>The serialized object as an XML string.</returns>
        public string SerializeToString(object obj)
        {
            using (var memoryStream = new MemoryStream())
            {
                this.Serialize(memoryStream, obj);
                memoryStream.Position = 0;
                return DefaultWriterSettings.Encoding.GetString(memoryStream.ToArray());
            }
        }
        

        /// <summary>
        /// Serializes an object to XML.
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="obj"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        public bool Serialize(XElement elem, object obj, ITypeData expectedType = null)
        {
            ITypeData type = null;
            if(obj != null)
            {
                type = TypeData.GetTypeData(obj);
            }
            if (Object.Equals(type, expectedType) == false && type != null)
                elem.SetAttributeValue("type", type.Name);

            foreach (var serializer in serializers)
            {
                try
                {
                    activeSerializers.Push(serializer);
                    if(serializer is ITapSerializerPlugin ser)
                    {
                        if (ser.Serialize(elem, obj, type))
                            return true;
                    }
                }
                finally
                {
                    activeSerializers.Pop();
                }
            }
            return false;

        }

        internal static string MakeValidXmlName(string name)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (name.Length == 0) throw new ArgumentException("name length cannot be 0.", "name");

            if (XmlConvert.IsStartNCNameChar(name[0]) && name.All(XmlConvert.IsNCNameChar))
                return name;

            StringBuilder sb = new StringBuilder(name.Length);
            sb.Append(XmlConvert.IsStartNCNameChar(name[0]) ? name[0] : '_');
            for(int i = 1; i < name.Length; i++)
            {
                var c = name[i];
                if (XmlConvert.IsNCNameChar(c))
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            
            System.Diagnostics.Debug.Assert(sb.Length > 0);
            return sb.ToString();
        }
        
        /// <summary>
        /// Convert a type to a string supported by XML.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string TypeToXmlString(Type type)
        {
            var attr = type.GetCustomAttributes<XmlTypeAttribute>().FirstOrDefault();
            if(attr != null)
            {
                return attr.TypeName;
            }

            var sb = new StringBuilder();
            loopNext:
            if (type.IsArray)
            {
                sb.Append("ArrayOf");
                type = type.GetElementType();
                goto loopNext;
            }
            else if (type.IsGenericType)
            {
                sb.Append(type.GetGenericTypeDefinition().Name.Split('`').First() + "Of");
                type = type.GetGenericArguments()[0];
                goto loopNext;
            }
            
            sb.Append(type.Name);
            
            return MakeValidXmlName(sb.ToString());
        }
        
        /// <summary>
        /// Clones an object using the serializer. Skips generating and parsing XML text, so it is faster than a full serialize/deserialize.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public object Clone(object obj)
        {
            if (obj == null) return null;
            XDocument doc = new XDocument();
            XElement elem = new XElement("root");
            Serialize(elem, obj);
            doc.Add(elem);
            return Deserialize(doc);
        }

        /// <summary> for mapping object to serializer. </summary>
        static System.Runtime.CompilerServices.ConditionalWeakTable<object, TapSerializer> serializerSteps = 
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, TapSerializer>();
        internal void Register(object step)
        {
            serializerSteps.Add(step, this);
        }

        /// <summary> Returns the serializer for a given object. null if the object is or has not been deserialized.</summary>
        public static TapSerializer GetObjectDeserializer(object @object)
        {
            TapSerializer serializer = null;
            serializerSteps.TryGetValue(@object, out serializer);
            return serializer ?? GetCurrentSerializer();
        }

        /// <summary> The path where the current file is being loaded from. This might be null in cases where it's being loaded from a stream.</summary>
        public string ReadPath { get; private set; }
    }
    
    internal static class StringCompressor
    {
        /// <summary>
        /// Compresses the string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns></returns>
        public static string CompressString(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length);
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
            return Convert.ToBase64String(gZipBuffer);
        }

        /// <summary>
        /// Decompresses the string.
        /// </summary>
        /// <param name="compressedText">The compressed text.</param>
        /// <returns></returns>
        public static string DecompressString(string compressedText)
        {
            byte[] gZipBuffer = Convert.FromBase64String(compressedText);
            using (var memoryStream = new MemoryStream())
            {
                int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                var buffer = new byte[dataLength];

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(buffer, 0, buffer.Length);
                }

                return Encoding.UTF8.GetString(buffer);
            }
        }
    }
}
