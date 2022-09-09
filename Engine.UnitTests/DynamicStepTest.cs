using System;
using System.Xml.Serialization;

namespace OpenTap.Engine.UnitTests
{
    public class DynamicStepTest : TestStep, IDynamicStep
    {
        public string NewData { get; set; }

        [XmlIgnore]
        public string NewData2 { get; set; }


        public DynamicStepTest()
        {
            NewData = "Hello";
        }

        public DynamicStepTest(string data) : this()
        {
            NewData2 = NewData + data;
        }


        public Type GetStepFactoryType()
        {
            return typeof(DynamicStepTest);
        }

        public ITestStep GetStep()
        {
            return new DynamicStepTest(NewData);
        }

        public override void Run()
        {
            throw new NotImplementedException();
        }
    }
}