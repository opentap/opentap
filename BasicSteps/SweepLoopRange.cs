//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Reflection;
using System.Xml.Serialization;
using System.Data;
using System.Text;
using System.Xml.Linq;

namespace OpenTap.Plugins.BasicSteps
{
    /// <summary> Sweep behavior for linear sweeps. </summary>
    public enum SweepBehavior
    {
        /// <summary> Linear growth function. </summary>
        [Display("Linear", Description: "Linear growth function.")]
        Linear = 0,
        /// <summary> Exponential growth function.</summary>
        [Display("Exponential", Description: "Exponential growth function.")]
        Exponential
    }

    [Display("Sweep Loop (Range)", Group: "Flow Control", Description: "Loops all of its child steps while sweeping a specified parameter/setting over a range.")]
    [AllowAnyChild]
    public class SweepLoopRange : LoopTestStep
    {

        [Display("Sweep Parameters", Order: -4, Description: "Test step parameters that should be swept. The variable must be a numeric type.")]
        [XmlIgnore]
        [Browsable(true)]
        public List<IMemberData> SweepProperties { get; set; }

        [Display("Start", Order: -2, Description: "The parameter value where the sweep will start.")]
        public decimal SweepStart { get; set; }

        [Display("Stop", Order: -1, Description: "The paramater value where the sweep will stop.")]
        public decimal SweepStop { get; set; }

        [Browsable(false)]
        public decimal SweepEnd { get { return SweepStop; } set { SweepStop = value; } }


        [Display("Step Size", Order: 1, Description: "The value to be increased or decreased between every iteration of the sweep.")]
        [EnabledIf("SweepBehavior", SweepBehavior.Linear, HideIfDisabled = true)]
        [XmlIgnore] // this is inferred from the other properties and should not be set by the serializer
        [Browsable(true)]
        public decimal SweepStep {
            get
            {
                if (SweepPoints == 0) return 0;
                if (SweepPoints == 1) return 0;
                return (SweepStop - SweepStart) / (SweepPoints - 1);
            }
            set
            {
                if (decimal.Zero == value) return;
                var newv = (uint)Math.Round((SweepStop - SweepStart) / value) + 1;
                SweepPoints = newv;
            }
        }

        [Display("Points", Order: 1, Description: "The number of points to sweep.")]
        public uint SweepPoints { get; set; }
        
        [Display("Behavior", Order: -3, Description: "Linear or exponential growth.")]
        public SweepBehavior SweepBehavior { get; set; }

        [Output]
        [Display("Current Value", "Shows the current value of the loop.", Order: 4)]
        public decimal Current { get; private set; }

        [Browsable(false)]
        [DeserializeOrder(1.0)]
        public string SweepPropertyName
        {
            get => propertyInfosToString(SweepProperties);
            set => SweepProperties = parseInfosFromString(value).ToList();
        }

        static string propertyInfosToString(IEnumerable<IMemberData> infos)
        {
            return string.Join("|", infos.Select(x => String.Format("{0};{1}", x.Name, x.DeclaringType.Name)));
        }

        List<IMemberData> parseInfosFromString(string str)
        {
            var cs = this.GetChildSteps(TestStepSearch.All).Select( x => TypeData.GetTypeData(x)).ToArray();
            var allMembers = cs.SelectMany(x => x.GetMembers().ToArray());
            Dictionary<string, IMemberData> dict = new Dictionary<string, IMemberData>();
            foreach(var x in allMembers)
            {
                var key = String.Format("{0};{1}", x.Name, x.DeclaringType.Name);
                dict[key] = x;
            }
            var items = str.Split('|');
            List<IMemberData> outlist = new List<IMemberData>();
            foreach (var item in items)
            {
                if (dict.TryGetValue(item, out IMemberData member))
                    outlist.Add(member);
            }
            return outlist;
        }

        ITypeData SweepType => SweepProperties.FirstOrDefault()?.TypeDescriptor;
        bool isRunning => StepRun != null;
        string validateSweep(decimal Value)
        {   // Mostly copied from Run
            if (SweepProperties.Count == 0) return "";
            if (isRunning) return ""; // Avoid changing the value during run when the gui asks for validation errors.
            StringBuilder sb = new StringBuilder();

            var sets = GetPropertySets(ChildTestSteps).ToList();
            var originalValues = sets.Select(set => set.GetValue()).ToArray();
            try
            {
                foreach (var set in sets)
                    set.SetValue(Value, false);
                bool first = true;
                foreach (var step in sets)
                {
                    if (string.IsNullOrWhiteSpace(step.Obj.Error))
                        continue;
                    if (first)
                        first = false;
                    else
                        sb.AppendLine();
                    ITestStep step2 = step.Obj as ITestStep;
                    if (step2 != null)
                        sb.AppendFormat("Step '{0}' : {1}", step2.Name, step2.Error);
                    else
                        sb.AppendFormat("Object '{0}' : {1}", step.Obj, step.Obj.Error);
                }
                return sb.ToString();
            }
            catch (TargetInvocationException e)
            {
                return e.InnerException.Message;
            }
            finally
            {
                for (int i = 0; i < sets.Count; i++)
                    sets[i].SetValue(originalValues[i], false);
            }
        }

        public SweepLoopRange()
        {
            SweepProperties = new List<IMemberData>();

            SweepStart = 1;
            SweepStop = 100;
            SweepPoints = 100;

            Rules.Add(() => string.IsNullOrWhiteSpace(validateSweep(SweepStart)), () => validateSweep(SweepStart), "SweepStart");
            Rules.Add(() => string.IsNullOrWhiteSpace(validateSweep(SweepStop)), () => validateSweep(SweepStop), "SweepEnd");

            Rules.Add(() => ((SweepBehavior != SweepBehavior.Exponential)) || (SweepStart != 0), "Sweep start value must be non-zero.", "SweepStart");
            Rules.Add(() => ((SweepBehavior != SweepBehavior.Exponential)) || (SweepStop != 0), "Sweep end value must be non-zero.", "SweepEnd");
            Rules.Add(() => SweepPoints > 1, "Sweep points must be bigger than 1.", "SweepPoints");
            
            Rules.Add(() => ((SweepBehavior != SweepBehavior.Exponential)) || (Math.Sign(SweepStop) == Math.Sign(SweepStart)), "Sweep start and end value must have the same sign.", "SweepEnd", "SweepStart");
        }

        /// <summary> Obj/Property mapping </summary>
        struct ObjPropSet
        {
            public IValidatingObject Obj;
            public IMemberData Prop;
            public void SetValue(object value, bool invokePropertyChanged = true)
            {

                if (Prop.TypeDescriptor.DescendsTo(value.GetType()))
                {
                    Prop.SetValue(Obj, value);
                }
                else if(Prop.TypeDescriptor is TypeData cst)
                {
                    Prop.SetValue(Obj, Convert.ChangeType(value, cst.Type));
                }
                else
                {
                    return;
                }
                if (invokePropertyChanged)
                    Obj.OnPropertyChanged(Prop.Name);
            }
            public object GetValue()
            {
                return Prop.GetValue(Obj);
            }
        }

        IEnumerable<ObjPropSet> GetPropertySets(TestStepList childTestStep)
        {
            foreach (var step in childTestStep)
            {
                foreach (var prop in SweepProperties)
                {
                    IMemberData paramProp = TypeData.GetTypeData(step).GetMember(prop.Name);
                    if (paramProp != null && (paramProp.TypeDescriptor == prop.TypeDescriptor && paramProp.GetDisplayAttribute().GetFullName() == prop.GetDisplayAttribute().GetFullName()))
                        yield return new ObjPropSet { Obj = step, Prop = paramProp };
                    if (step is TestPlanReference) continue; //TODO: keep this hack?
                    foreach (var set in GetPropertySets(step.ChildTestSteps))
                    {
                        yield return set;
                    }
                }
            }
        }


        public static IEnumerable<decimal> LinearRange(decimal start,  decimal end, int points)
        {
            if (points == 0) yield break;
            decimal Value = start;
            for(int i = 0; i < points - 1; i++)
            {
                yield return (start * (points - 1- i) + end * (i)) / (points - 1);
            }
            yield return end;
        }

        public static IEnumerable<decimal> ExponentialRange(decimal start, decimal end, int points)
        {
            if (start == 0)
                throw new ArgumentException("Start value must be different than zero.");
            if (end == 0)
                throw new ArgumentException("End value must be different than zero.");
            if (Math.Sign(start) != Math.Sign(end))
                throw new ArgumentException("Start and end value must have the same sign.");
            
            var logs = Math.Log10((double) start);
            var loge = Math.Log10((double) end);
            return LinearRange((decimal)logs, (decimal)loge, points).Select(x => (decimal)Math.Pow(10, (double)x));
        }
        

        public override void Run()
        {
            base.Run();

            var sets = GetPropertySets(ChildTestSteps).ToList();
            var originalValues = sets.Select(set => set.GetValue()).ToArray();


            IEnumerable<decimal> range = LinearRange(SweepStart, SweepStop, (int)SweepPoints);

            if (SweepBehavior == SweepBehavior.Exponential)
                range = ExponentialRange(SweepStart, SweepStop, (int)SweepPoints);

            var disps = SweepProperties.Select(x => x.GetDisplayAttribute()).ToList();
            string names = string.Join(", ", disps.Select(x => x.Name));
            
            if (disps.Count > 1)
                names = string.Format("{{{0}}}", names);

            foreach (var Value in range)
            {
                Current = Value;
                OnPropertyChanged("Current");
                foreach (var set in sets)
                {
                    try
                    {
                        set.SetValue(Value);
                    }
                    catch (TargetInvocationException ex)
                    {
                        Log.Error("Unable to set '{0}' to value '{2}': {1}", set.Prop.GetDisplayAttribute().Name, ex.InnerException.Message, Value);
                        Log.Debug(ex.InnerException);
                    }
                }

                var AdditionalParams = new ResultParameters();
                
                foreach (var disp in disps)
                    AdditionalParams.Add(new ResultParameter(disp.Group.FirstOrDefault() ?? "", disp.Name, Value));

                Log.Info("Running child steps with {0} = {1} ", names, Value);

                var runs = RunChildSteps(AdditionalParams, BreakLoopRequested).ToList();
                if (BreakLoopRequested.IsCancellationRequested) break;
                runs.ForEach(r => r.WaitForCompletion());
            }
            for (int i = 0; i < sets.Count; i++)
                sets[i].SetValue(originalValues[i]);
        }
    }

    public class LegacySweepLoader : TapSerializerPlugin
    {
        public override double Order { get { return 4; } }

        HashSet<XElement> proccessingNodes = new HashSet<XElement>();

        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            var typeAttribute = node.Attribute("type");
            if(typeAttribute != null && (typeAttribute.Value == "OpenTap.Plugins.BasicSteps.LinearSweepLoop" || typeAttribute.Value == "OpenTap.Plugins.BasicSteps.SweepLoopRange") )
            {
                if (!proccessingNodes.Add(node))
                    return false;
                try
                {
                    typeAttribute.SetValue(typeof(SweepLoopRange).FullName);
                    //Rename old SweepEnd setting to its new (8x) name "SweepStop"
                    var newN = node.Element("SweepStop");
                    var oldN = node.Element("SweepEnd");
                    if (oldN != null)
                    {
                        oldN.Name = "SweepStop";

                        if (newN != null)
                        {
                            if (oldN.Attribute("external") != null && newN.Attribute("external") == null)
                                newN.SetAttributeValue("external", oldN.Attribute("external").Value); // move the attribute from the old to the new

                            oldN.Remove();
                        }
                    }
                    return Serializer.Deserialize(node, setter, typeof(SweepLoopRange));
                }
                finally
                {
                    proccessingNodes.Remove(node);
                }
            }
            return false;
        }

        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            return false;
        }
    }
}
