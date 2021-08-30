using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap
{
    public class ViaPointReference : ViaPoint
    {
        [Display("Device")]
        public IInstrument DeviceReference
        {
            get => (IInstrument)Device;
            set => Device = value;
        }

        public IEnumerable<string> AvailablePointNames => Device.GetConstProperties<ViaPoint>().Select(p => p.Name);

        [Display("Position")]
        [AvailableValues("AvailablePointNames")]
        public string PointName
        {
            get => Name;
            set => Name = value;
        }

        public ViaPointReference()
        {
        }
    }

    public class ViaPointTypeData : ITypeData
    {
        private ITypeData x;

        public ViaPointTypeData(ITypeData x)
        {
            this.x = x;
        }

        public ITypeData BaseType => TypeData.FromType(typeof(ViaPoint));

        public bool CanCreateInstance => throw new NotImplementedException();

        public IEnumerable<object> Attributes => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public object CreateInstance(object[] arguments)
        {
            throw new NotImplementedException();
        }

        public IMemberData GetMember(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IMemberData> GetMembers()
        {
            throw new NotImplementedException();
        }
    }

    //internal interface IDynamicMembers
    //{
    //    IEnumerable<DynamicMemberDefinition> GetDynamicMembers();
    //}

    //public class DynamicMemberDefinition
    //{
    //    public IEnumerable<object> Attributes { get; set; } = Array.Empty<object>();
    //    public string Name { get; set; }
    //    public ITypeData TypeDescriptor { get; set; }
    //    public ITypeData DeclaringType { get; set; }
    //    public bool IsWritable { get; set; }
    //    public bool IsReadable { get; set; }
    //}
}
