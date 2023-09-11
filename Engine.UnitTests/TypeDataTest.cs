using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;

namespace OpenTap.UnitTests
{
    public class TypeDataTest
    {
        public class IntObject
        {
            public int SomeInt { get; set; }
        }

        [Browsable(false)]
        class Class1 { }
        class Class2 : Class1 { }
        [Test]
        public void InheritedAttributesTest()
        {
            var type1 = TypeData.FromType(typeof(Class1));
            var type2 = TypeData.FromType(typeof(Class2));
            Assert.IsEmpty(type2.Attributes);
            Assert.IsTrue(type1.HasAttribute<BrowsableAttribute>());
            Assert.IsTrue(type2.BaseType.HasAttribute<BrowsableAttribute>());
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
        public void ValueTypeCanCreateInstanceTest()
        {
            var types = new[]
            {
                typeof(int),
                typeof(double),
                typeof(DateTime),
                typeof(TimeSpan),
                typeof(KeyValuePair<string, string>),
            };
            
            foreach (var type in types)
            {
                var td = TypeData.FromType(type);
                Assert.AreEqual(type.IsValueType, td.IsValueType);
                Assert.IsTrue(td.CanCreateInstance, $"Expected type {type.FullName} to be constructable.");
                Assert.AreEqual(Activator.CreateInstance(type), td.CreateInstance());
            }
        }

        [Test]
        public void DynamicTypeBrowsableFalse()
        {
            // Create an empty dynamic type which is public.
            var assemblyName = "TestAssembly";
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("TestModule");
            TypeBuilder typeBuilder = moduleBuilder.DefineType("TestType",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(object),Array.Empty<Type>());
            var type = typeBuilder.CreateTypeInfo();
            var td = TypeData.FromType(type);
            
            // A problem caused these kinds of type data to have IsBrowsable set to false, even though it does not 
            // even have a BrowsableAttribute assigned.
            Assert.IsTrue(td.IsBrowsable);

            foreach (var browsable in new[] { true, false })
            {
                TypeBuilder typeBuilder2 = moduleBuilder.DefineType("TestType2_" + browsable,
                    TypeAttributes.Public | TypeAttributes.Class,
                    typeof(object), Array.Empty<Type>());
                CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder(
                    typeof(BrowsableAttribute).GetConstructor(new[] { typeof(bool) }), new object[] { browsable });
                typeBuilder2.SetCustomAttribute(attrBuilder);

                var td2 = TypeData.FromType(typeBuilder2.CreateTypeInfo());
                Assert.AreEqual(browsable, td2.IsBrowsable);
            }

        }
    }
}
