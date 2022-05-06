using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenTap.Diagnostic;
using OpenTap.Package;

namespace OpenTap.Engine.UnitTests
{
    public class SelfReferentialTypeDataProvider : IStackedTypeDataProvider
    {
        private static int fn(int a)
        {
            TypeData.GetTypeData(a);
            return a;
        }
        
        static IMemorizer<int, int> circularMemorizer = new Memorizer<int, int, int>(null, fn);
        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            circularMemorizer.Invoke(2);
            return stack.GetTypeData(identifier);
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            circularMemorizer.Invoke(2);
            return stack.GetTypeData(obj);
        }

        public double Priority { get; }
    }

    [TestFixture]
    public class TestSelfReferentialTypeData
    {
        [Test]
        public void TestTypeDataLookupTerminates()
        {
            try
            {
                using (Session.Create())
                {
                    var errors = new List<Event>();
                    var l = new EventTraceListener();
                    Log.AddListener(l);
                    l.MessageLogged += (msg) => errors.AddRange(msg.Where(m => m.EventType == (int)LogEventType.Error));
                    // Trigger typedata search
                    TypeData.GetTypeData(1);
                    CollectionAssert.IsEmpty(errors, $"Operation had errors: {string.Join(", ", errors)}");
                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"Cycle was not prevented: '{ex.Message}'");
            }
        }
    }
}