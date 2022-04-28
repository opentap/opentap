using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.UnitTests
{
    public class TypeDataTest
    {
        public class IntObject
        {
            public int SomeInt { get; set; }
        }

        [Test]
        public void SettingPropertyToInvalidType()
        {
            var obj = new IntObject();
            var typeData = TypeData.GetTypeData(obj);
            var memberData = typeData.GetMember(nameof(IntObject.SomeInt));
            Assert.DoesNotThrow(() => memberData.SetValue(obj, 123));
            // PropertyInfo.SetValue(xx, null) does not throw an exception for value types.
            // https://docs.microsoft.com/de-de/dotnet/api/system.reflection.propertyinfo.setvalue
            Assert.DoesNotThrow(() => memberData.SetValue(obj, null));
        }
    }
}
