using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        [Test]
        public void DynamicTypeBrowsableFalse()
        {
            // Create an empty dynamic type which is public.
            var domain = AppDomain.CurrentDomain;
            var assemblyName = "TestAssembly";
            var assemblyBuilder = domain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("TestModule");
            TypeBuilder typeBuilder = moduleBuilder.DefineType("TestType",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(object),Array.Empty<Type>());
            var type = typeBuilder.CreateType();
            var td = TypeData.FromType(type);
            
            // A problem caused these kinds of type data to have IsBrowsable set to false, even though it does not 
            // even have a BrowsableAttribute assigned.
            Assert.IsTrue(td.IsBrowsable);
            
            TypeBuilder typeBuilder2 = moduleBuilder.DefineType("TestType2",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(object),Array.Empty<Type>());
            CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder(
                typeof(BrowsableAttribute).GetConstructor(new Type[] { typeof(bool) }), new object[] { false });
            typeBuilder2.SetCustomAttribute(attrBuilder);

            var td2 = TypeData.FromType(typeBuilder2.CreateType());
            Assert.IsFalse(td2.IsBrowsable);

        }
    }
}
