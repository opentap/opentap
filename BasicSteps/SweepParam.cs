using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Plugins.BasicSteps
{
    public class SweepParam
    {
        internal SweepLoop Step;
        public string Name => Member?.Name;

        object[] _values = Array.Empty<object>();
        public object[] Values { get => _values; set => _values = value; }

        object _defaultValue;
        [XmlIgnore]
        public object DefaultValue
        {
            get => cloneObject(_defaultValue);
            set => _defaultValue = value;
        }

        [XmlIgnore]
        public ITypeData Type => Member?.TypeDescriptor; 

        public string[] MemberNames { get; set; }

        internal static string GetMemberName(IMemberData member) => member.DeclaringType.Name + "." + member.Name;
        

        IMemberData[] members = Array.Empty<IMemberData>();
        [XmlIgnore]
        public IMemberData[] Members
        {
            get => members;
            set
            {
                members = value;
                MemberNames = value.Select(GetMemberName).ToArray();
                Step?.parametersChanged();
            }
        }
        
        /// <summary>
        /// The property from where attributes are pulled.
        /// </summary>
        
        public IMemberData Member => Members?.FirstOrDefault();

        /// <summary>
        /// Default constructor. Used by serializer.
        /// </summary>
        public SweepParam()
        {
        }

        /// <summary>
        /// Initializes a new instance of the SweepParam class. For use when programatically creating a <see cref="SweepLoop"/>
        /// </summary>
        /// <param name="prop">Property to sweep. This should be one of the properties on one of the childsteps of the <see cref="SweepLoop"/>.</param>
        public SweepParam(IEnumerable<IMemberData> props) : this()
        {
            Members = props.Distinct().ToArray();
            if (Members.Length == 0)
                throw new ArgumentException("Must contain at least one member", nameof(props));

            if (!Members.All(p => Equals(p.TypeDescriptor, Type)))
                throw new ArgumentException("All members must be of the same type", nameof(props));
        }

        public SweepParam(IEnumerable<IMemberData> members, params object[] values):this(members)
        {
            Resize(values.Length);
            for(int i = 0; i < values.Length; i++)
            {
                Values[i] = values[i];
            }
        }

        public override string ToString()
        {
            return string.Format("{0} : {1}", Name, Type.Name);
        }

        TapSerializer serializer = null;
        object cloneObject(object newValue)
        {
            if (StringConvertProvider.TryGetString(newValue, out string str))
            {
                if (StringConvertProvider.TryFromString(str, TypeData.GetTypeData(newValue), this.Step, out object result))
                {
                    newValue = result;
                }
            }
            else
            {

                string serialized = null;

                serializer = new TapSerializer();
                try
                {

                    serialized = serializer.SerializeToString(newValue);
                    newValue = serializer.DeserializeFromString(serialized, TypeData.GetTypeData(newValue));
                }
                catch
                {

                }

            }
            return newValue;
        }
        public void Resize(int newCount)
        {
            int oldCount = Values.Length;
            if (oldCount == newCount)
                return;

            var oldValues = Values;
            Array.Resize(ref _values, newCount);
            for (int i = oldCount; i < newCount; i++)
            {
                object newValue = null; 
                if (i == 0)
                    newValue = DefaultValue;
                else
                    newValue = _values.GetValue(i - 1);
                newValue = cloneObject(newValue);
                _values.SetValue(newValue, i);
            }
            var copyAmount = Math.Min(newCount, oldValues.Length);
            Array.Copy(oldValues, Values, copyAmount);
            Step?.parametersChanged();
        }
    }
}