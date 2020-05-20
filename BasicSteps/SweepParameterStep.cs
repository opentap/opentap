using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
    [Display("Sweep Parameter", "Table based loop that sweeps the value of its parameters based on a set of values.", "Flow Control")]
    public class SweepParameterStep : LoopTestStep, ISelectedParameters
    {
        SweepRowCollection sweepValues = new SweepRowCollection();
        [DeserializeOrder(1)] // this should be deserialized as the last thing.
        [Display("Sweep Values", "A table of values to be swept for the selected parameters.", "Sweep")]
        public SweepRowCollection SweepValues 
        { 
            get => sweepValues;
            set
            {
                sweepValues = value;
                sweepValues.Loop = this;
            }
        }


        public IEnumerable<IMemberData> SweepProperties =>
            TypeData.GetTypeData(this).GetMembers().OfType<IParameterMemberData>().Where(x =>
                x.HasAttribute<UnsweepableAttribute>() == false && x.Writable && x.Readable);

        public IEnumerable<string> SweepNames =>
            SweepProperties.Select(x => x.Name);
        
        readonly NotifyChangedList<string> selectedProperties = new NotifyChangedList<string>();
        
        [Browsable(false)]
        public Dictionary<string, bool> Selected { get; set; } = new Dictionary<string, bool>();
        void updateSelected()
        {
            foreach (var prop in SweepProperties)
            {
                if (Selected.ContainsKey(prop.Name) == false)
                    Selected[prop.Name] = true;
            }
            foreach (var item in Selected.ToArray())
            {
                if (item.Value)
                {
                    if (selectedProperties.Contains(item.Key) == false)
                        selectedProperties.Add(item.Key);
                }
                else
                {
                    if (selectedProperties.Contains(item.Key))
                        selectedProperties.Remove(item.Key);
                }
            }
        }

        void onListChanged(IList<string> list)
        {
            foreach (var item in Selected.Keys.ToArray())
            {
                Selected[item] = list.Contains(item);
            }
        }
        [AvailableValues(nameof(SweepNames))]
        
        [XmlIgnore]
        [Browsable(true)]
        [Display("Parameters", "These are the parameters that should be swept", "Sweep")]
        public IList<string> SelectedParameters {
            get
            {
                updateSelected();
                selectedProperties.ChangedCallback = onListChanged;
                return selectedProperties;
            }
            set
            {
                updateSelected();
                onListChanged(value);
            } 
        }

        
        public SweepParameterStep()
        {
            SweepValues.Loop = this;
            SweepValues.Add(new SweepRow());
            Name = "Sweep {Parameters}";
        }

        int iteration;
        
        [Output]
        [Display("Iteration", "Shows the iteration of the sweep that is currently running or about to run.", "Sweep", Order: 3)]
        public string IterationInfo => string.Format("{0} of {1}", iteration + 1, SweepValues.Count(x => x.Enabled));

        
        public override void Run()
        {
            base.Run();
            iteration = 0;
            var sets = SweepProperties.ToArray();
            var originalValues = sets.Select(set => set.GetValue(this)).ToArray();

            var disps = SweepProperties.Select(x => x.GetDisplayAttribute()).ToList();
            string names = string.Join(", ", disps.Select(x => x.Name));
            
            if (disps.Count > 1)
                names = string.Format("{{{0}}}", names);
            var rowType = SweepValues.Select(x => TypeData.GetTypeData(x)).FirstOrDefault();
            foreach (var Value in SweepValues)
            {
                if (Value.Enabled == false) continue;
                var AdditionalParams = new ResultParameters();

                
                foreach (var set in sets)
                {
                    var mem = rowType.GetMember(set.Name);
                    var val = StringConvertProvider.GetString(mem.GetValue(Value), CultureInfo.InvariantCulture);
                    var disp = mem.GetDisplayAttribute();
                    AdditionalParams.Add(new ResultParameter(disp.Group.FirstOrDefault() ?? "", disp.Name, val));

                    try
                    {
                        var value = StringConvertProvider.FromString(val, set.TypeDescriptor, this, CultureInfo.InvariantCulture);
                        set.SetValue(this, value);
                    }
                    catch (TargetInvocationException ex)
                    {
                        Log.Error("Unable to set '{0}' to value '{2}': {1}", set.GetDisplayAttribute().Name, ex.InnerException.Message, Value);
                        Log.Debug(ex.InnerException);
                    }
                }

                iteration += 1;
                // Notify that values might have changes
                OnPropertyChanged("");
                
                 Log.Info("Running child steps with {0} = {1} ", names, Value);

                var runs = RunChildSteps(AdditionalParams, BreakLoopRequested).ToList();
                if (BreakLoopRequested.IsCancellationRequested) break;
                runs.ForEach(r => r.WaitForCompletion());
            }
            for (int i = 0; i < sets.Length; i++)
                sets[i].SetValue(this, originalValues[i]);
        } 
    }
}
