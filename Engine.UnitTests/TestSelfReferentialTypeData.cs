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
        public static SessionLocal<bool> Enabled = new SessionLocal<bool>();
        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            if (!Enabled.Value) return null;
            Installation.Current.GetPackages();
            return stack.GetTypeData(identifier);
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            if (!Enabled.Value) return null;
            Installation.Current.GetPackages();
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
                    SelfReferentialTypeDataProvider.Enabled.Value = true;
                    var errors = new List<Event>();
                    var l = new EventTraceListener();
                    Log.AddListener(l);
                    l.MessageLogged += (msg) => errors.AddRange(msg.Where(m => m.EventType == (int)LogEventType.Error));
                    // Trigger typedata search
                    Installation.Current.GetPackages();
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