// Sometimes you need to introduce a breaking change in a plugin library.
// Other times you find that two steps does the same, and you want to consolidate the two.
// In any case, you might prefer that the old test plans are still able to load.
// this example shows how to use a ITapSerializerPlugin to migrate and load old test plans in with new plugins.

using System;
using System.Xml.Linq;
using OpenTap;

namespace PluginDevelopment.Advanced_Examples
{
    [Display("Old Delay Step", "Demonstrates an old version of test step which is wanted to migrate to a new version.",
        Groups: new[] { "Examples", "Plugin Development", "Advanced Examples" })]
    public class OldDelayStep : TestStep
    {
        public int DelayMs { get; set; }
        public override void Run()
        {
            TapThread.Sleep(DelayMs);
        }
    }

    [Display("New Delay Step", "Demonstrates an new version of test step which is wanted to migrate." +
                               " To test this, try creating an 'Old Delay Step', save and load. The New Delay Step should have been created.",
        Groups: new[] { "Examples", "Plugin Development", "Advanced Examples" })]
    public class NewDelayStep : TestStep
    {
        public double DelaySec { get; set; }
        public override void Run()
        {
            TapThread.Sleep(TimeSpan.FromSeconds(DelaySec));
        }
    }
     
    public class BreakingChangeFixupSerializer : ITapSerializerPlugin
    {
        static XName steps = "Steps";
        static XName typeattr = "type";
        static XName parameterattr = "Parameter";
        static XName scopeAttr = "Scope";
        static XName delayMsElem = "DelayMs";
        static XName delaySecElem = "DelaySec";

        static XName postScaleAttr = "post-scale";
        // the type we want to replace.
        static string searchType = "PluginDevelopment.Advanced_Examples.OldDelayStep";
        static string replaceType = "PluginDevelopment.Advanced_Examples.NewDelayStep";

        static readonly TraceSource log = Log.CreateSource("Fix Serializer");
        void iterateAndReplaceTypesInXml(XElement node, XElement testPlanNode)
        {
            if (node.Attribute(typeattr) is XAttribute typeAttribute && typeAttribute.Value == searchType)
            {
                // we discovered the type
                typeAttribute.Value = replaceType;
                var valueElem = node.Element(delayMsElem);
                valueElem.Name = delaySecElem; // change the name of the node to DelaySec
                valueElem.Add(new XAttribute(postScaleAttr, 0.001));

                if (valueElem.Attribute(parameterattr) is XAttribute parameterAttribute &&
                    valueElem.Attribute(scopeAttr) == null)
                { 
                    // the property is a test plan parameter. This will give issues with pre-scaling.
                    // in this case it could simply be removed from the test plan node.
                    // but it will give issues if other test steps has properties  that are merged with the same parameter.
                    
                    testPlanNode.Element(parameterAttribute.Value)?.Remove();
                    // but also print a warning.
                    log.Warning("fixing member that is being used in the test plan parameter '{0}'. Please verify the value of this parameter.", parameterAttribute.Value);
                }
                
                
            }
            foreach(var subnode in node.Elements())
                iterateAndReplaceTypesInXml(subnode, testPlanNode);
        }
        
        public bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            // 1. 'Iterate and Replace' if the node is the test plan node, we want to find all test step sub-nodes and replace the types.
            if(t.DescendsTo(typeof(TestPlan)))
                iterateAndReplaceTypesInXml(node, node);
            else if (node.Attribute(postScaleAttr) is XAttribute attribute)
            {
                // 2. do post-scaling.
                // when the old step was detected the property was marked with this post-scale attribute to show that it should scale it after deserialization.
                // this is because the old step used milliseconds, but the new one uses seconds.
                
                attribute.Remove(); // remove the attribute to avoid hitting this again.
                
                // this new setter applies the post-scaling that was added to the attributes during 'iterate and replace'.
                Action<object> newSetter = x => setter(double.Parse(x.ToString()) * double.Parse(attribute.Value));
                
                // call the deserializer to actually deserialize the property, but direct it to use the new setter instead of the original.
                return TapSerializer.GetCurrentSerializer().Deserialize(node, newSetter, t);
            }

            return false;
        }

        // Do nothing during serialization.
        public bool Serialize(XElement node, object obj, ITypeData expectedType) => false; 

        // The right order can be hard to determine, but many different values works!.
        // the best is probably to run some code to check which other serializers is already installed (see below).
        // this specific serializer should be run early in the chain.
        // if a serializer has high order it will get called early.
        public double Order => 5;

        // consider calling this code during debugging to list the existing serializers.
        void printExistingSerializerPlugins()
        {
            log.Info("Deserializers:");
            foreach (var type in TypeData.GetDerivedTypes<ITapSerializerPlugin>())
            {
                if (type.CanCreateInstance == false) continue;
                try
                {
                    var plugin = (ITapSerializerPlugin)type.CreateInstance(Array.Empty<object>());
                    log.Info("{0} Order: {1}", type.Name, plugin.Order);
                }
                catch
                {
                    
                }
            }
        }
    }
}