using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTap.Plugins.PluginDevelopment.Advanced_Examples
{
    // This example shows how a 'virtual' property can be attached to
    // any type. It supports serialization and showing it in a user interface
    // However, direct references to it is more complicated and requires
    // a reference to the DLL that defines. Additionally, the attached property
    // can be made internal, further restricting access to it.
    // This can be used as a useful way of limiting how it is being used.
    //
    // This requires the creation of a new ITypeDataProvider. Be careful when creating a TypeDataProvider
    // it is easy to 'brick' your installation if it throws exceptions or make it slow if your TypeDataProvider
    // is not optimized and does unnecessary work.

    // In this example, let's add a unique identifier to all instruments.
    // In the GUI there will be a Instrument ID field for all instruments.
    // In XML this will look like  <Instrument.ID>adwadwa</Instrument.ID>
    public class InstrumentIDTypeDataProvider : IStackedTypeDataProvider
    {
        // This needs to be enabled from the example component settings.
        internal static bool Enabled;

        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            if (!Enabled) return null;
            if (identifier.StartsWith(InstrumentIDTypeData.pre))
            {
                var inner = stack.GetTypeData(identifier.Substring(InstrumentIDTypeData.pre.Length));
                return InstrumentIDTypeData.FromInnerType(inner);
            }

            return null;
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            if (!Enabled) return null;
            if (obj is IInstrument)
            {
                var inner = stack.GetTypeData(obj);
                return InstrumentIDTypeData.FromInnerType(inner);
            }

            return null;
        }

        // Determined by calling PrintTypeDataProviders.
        public double Priority => 2;

        // uncomment these to see which type data providers exist
        //public InstrumentIDTypeDataProvider()
        //{
        //    if (printed) return;
        //    printed = true; // avoid recursive loop
        //    PrintTypeDataProviders();
        //}
        //static bool printed = false;

        // To get an overview of the existing type data provider, we recommend calling this:
        // This will help figure out which Priority we should choose.
        static void PrintTypeDataProviders()
        {
            TraceSource log = Log.CreateSource("TypeData");
            var items = TypeData.GetDerivedTypes<IStackedTypeDataProvider>()
                .Where(x => x.CanCreateInstance)
                .Select(x => x.CreateInstance())
                .OfType<IStackedTypeDataProvider>()
                .ToArray();
            foreach (var item in items)
            {
                log.Info("{0} : {1}", item, item.Priority);
            }
        }
    }

    /// <summary>
    /// Helper class for getting / setting the instrument ID
    /// </summary>
    public class InstrumentIDProperty
    {
        /// <summary> Gets the current ID of the instrument. </summary>
        public static string GetValue(IInstrument instrument)
        {
            return InstrumentIDTypeData.InstrumentIdMember.GetValue(instrument) as string;
        }

        /// <summary> Sets the current ID of the instrument. </summary>
        public static void SetValue(IInstrument instrument, string id)
        {
            InstrumentIDTypeData.InstrumentIdMember.SetValue(instrument, id);
        }
    }

    /// <summary>
    /// This is the extension to the existing instrument type
    /// </summary>
    class InstrumentIDTypeData : ITypeData
    {
        internal const string pre = "InstID:";
        readonly ITypeData innerType;


        /// <summary>
        /// It is very important that the type provider can resolve types very fast, therefore we store the type in this cache.
        /// </summary>
        static readonly ConditionalWeakTable<ITypeData, InstrumentIDTypeData> cache =
            new ConditionalWeakTable<ITypeData, InstrumentIDTypeData>();

        public static ITypeData FromInnerType(ITypeData innerType)
        {
            return cache.GetValue(innerType, t => new InstrumentIDTypeData(t));
        }

        InstrumentIDTypeData(ITypeData innerType)
        {
            this.innerType = innerType;
        }

        /// <summary> We only need to define the ID member once. So here it is stored as a static value.
        /// This can also help improve performance. </summary>
        public static readonly InstrumentIDMemberData InstrumentIdMember =
            new InstrumentIDMemberData(TypeData.FromType(typeof(string)), "Instrument.ID", null,
                new DisplayAttribute("Instrument ID"));

        public IEnumerable<object> Attributes => innerType.Attributes;
        public string Name => pre + innerType.Name;

        public IEnumerable<IMemberData> GetMembers()
        {
            // return all the members of the inner type and add our instrument ID menber.
            return innerType.GetMembers().Concat(new[] {InstrumentIdMember});
        }

        public IMemberData GetMember(string name)
        {
            // if name matches our instrument ID member, then we use that. Otherwise see if innerType has the member.
            if (name == InstrumentIdMember.Name) return InstrumentIdMember;
            return innerType.GetMember(name);
        }

        // Nothing special is needed to create an instance.
        public object CreateInstance(object[] arguments)
        {
            return innerType.CreateInstance(arguments);
        }

        // BaseType is a bit tricky here. We recommend using innerType because the extended type behaves
        // for all purposes like it's InnerType, except when accessing the additional members.
        public ITypeData BaseType => innerType;

        // It is best to forward inner types CanCreateInstance.
        public bool CanCreateInstance => innerType.CanCreateInstance;
    }

    class InstrumentIDMemberData : IMemberData
    {
        public InstrumentIDMemberData(ITypeData type, string name, object defaultValue, params object[] attributes)
        {
            this.Name = name;
            this.TypeDescriptor = type;
            DeclaringType = TypeData.FromType(typeof(IInstrument));
            this.defaultValue = defaultValue;
            this.Attributes = attributes;
        }

        // The default attributes, in this case just a DisplayAttribute.
        public IEnumerable<object> Attributes { get; }

        // The name of the property e.g "Instrument.ID"
        public string Name { get; }

        // This gets called whenever the value is set.
        public void SetValue(object owner, object value)
        {
            if (false == owner is IInstrument)
                throw new ArgumentException("First argument must be an instrument", nameof(owner));
            if (value is string || value == null)
            {
                // With ConditionalWeakTable, you have to remove the value before inserting a new one.
                lock (valueTable)
                {
                    valueTable.Remove(owner);
                    valueTable.Add(owner, value);
                }
            }
            else
            {
                throw new ArgumentException("Value must be a string or null.", nameof(value));
            }
        }

        // This is called when the value is gotten.
        public object GetValue(object owner)
        {
            if (owner is IInstrument)
            {
                lock (valueTable)
                    return valueTable.GetValue(owner, x => defaultValue);
            }

            throw new ArgumentException("Argument must be an instrument.");
        }

        // The declaring type, in this case we just say it is IInstrument. It does not matter much.
        public ITypeData DeclaringType { get; }

        // For example, the type 'String'.
        public ITypeData TypeDescriptor { get; }

        // The property is writable.
        public bool Writable { get; } = true;

        // The property is readable.
        public bool Readable { get; } = true;

        // this table contains the actual values of the virtual property.
        // it is a ConditionalWeakTable so that the values will automatically get cleaned up when the object is
        // garbage collected.
        readonly ConditionalWeakTable<object, object> valueTable = new ConditionalWeakTable<object, object>();

        // the default value, 'null'.
        readonly object defaultValue;
    }

    // A test step to test the property.
    [Display("Instrument ID Step", "Demonstrates how an Instrument ID can be fetched",
        Groups: new[] {"Examples", "Plugin Development", "Advanced Examples"})]
    public class InstrumentIDTestStep : TestStep
    {
        public IInstrument Instrument { get; set; }

        public override void Run()
        {
            var id = InstrumentIDProperty.GetValue(Instrument);
            Log.Info("Instrument has ID: {0}", id);
        }
    }
}