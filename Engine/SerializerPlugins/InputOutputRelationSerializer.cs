using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace OpenTap.Plugins
{
    /// <summary>
    /// Serializes input/output relations. It does so by adding a collection of inputs to the step XML while serializing.
    /// This is done because some of the properties might be read-only or XML ignore and in these cases we might still want
    /// to serialize the relation ship between them, even though the values are not themselves serialized.
    /// </summary>
    class InputOutputRelationSerializer : TapSerializerPlugin
    {
        class InputOutputMember
        {
            [XmlAttribute("id")]
            public Guid Id { get; set; }
            [XmlAttribute("member")]
            public string Member { get; set; }       
            [XmlAttribute("target-member")]
            public string TargetMember { get; set; }
        }

        static readonly XName stepInputsName = "Step.Inputs";
        readonly HashSet<XElement> activeElements = new HashSet<XElement>();
        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            
            if (t.DescendsTo(typeof(ITestStepParent)) == false)
                return false;
            if (node.Element(stepInputsName) is XElement subelems && activeElements.Contains(node) == false)
            {
                var tps = Serializer.GetSerializer<TestPlanSerializer>();
                ITestStepParent step = null;
                var prevsetter = setter;
                setter = x => prevsetter(step = x as ITestStepParent);
                var plan = tps.Plan;
                InputOutputMember[] items = null;
                if (tps != null && Serializer.Deserialize(subelems, x => items = (InputOutputMember[]) x, TypeData.FromType(typeof(InputOutputMember[]))))
                {
                    bool ok = false;
                    activeElements.Add(node);
                    try
                    {
                        ok = Serializer.Deserialize(node, setter, t);
                    }
                    catch
                    {
                        activeElements.Remove(node);
                    }


                    void connectInputs()
                    {
                        if (step == null) return;
                        foreach (var elem in items)
                        {
                            ITestStepParent source;
                            if (elem.Id == Guid.Empty)
                            {
                                source = plan;
                            }
                            else
                            {
                                source = plan.ChildTestSteps.GetStep(elem.Id);
                            }
                            var sourceType = TypeData.GetTypeData(source);
                            var targetType = TypeData.GetTypeData(step);
                            var sourceMember = sourceType.GetMember(elem.Member);
                            var targetMember = targetType.GetMember(elem.TargetMember);
                            if(sourceMember != null && targetMember != null)
                                 InputOutputRelation.Assign(step, targetMember, source, sourceMember);
                        }
                    }
                    if(ok)
                        Serializer.DeferLoad(connectInputs);
                    return ok;
                }
            }

            return false;
        }

        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            
            if (obj is ITestStepParent step && activeElements.Contains(node) == false)
            {
                activeElements.Add(node);
                try
                {
                    var inputs = InputOutputRelation.GetRelations(step).Where(x => x.InputObject == step).ToArray();
                    if (inputs.Length == 0) return false;
                    var items = inputs.Select(x => new InputOutputMember
                        {Id = (x.OutputObject as ITestStep)?.Id ?? Guid.Empty, Member = x.OutputMember.Name, TargetMember = x.InputMember.Name}).ToArray();
                    var subnode = new XElement(stepInputsName);
                    Serializer.Serialize(subnode, items, TypeData.GetTypeData(items));
                    var ok = Serializer.Serialize(node, obj, expectedType);
                    if (ok)
                        node.Add(subnode);
                    return ok;
                }
                finally
                {
                    activeElements.Remove(node);
                }
            }

            return false;
        }
    }
}