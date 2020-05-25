using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Plugins.BasicSteps
{
    interface ISelectedParameters
    {
        IList<string> SelectedParameters { get; }
    }
    public abstract class SweepParameterStepBase : LoopTestStep, ISelectedParameters
    {
        public IEnumerable<IMemberData> SweepProperties => TypeData.GetTypeData(this)
                .GetMembers().OfType<IParameterMemberData>()
                .Where(x => x.HasAttribute<UnsweepableAttribute>() == false && x.Writable && x.Readable);

        public IEnumerable<string> SweepNames =>
            SweepProperties.Select(x => x.Name);
        
        readonly NotifyChangedList<string> selectedProperties = new NotifyChangedList<string>();
        
        [Browsable(false)]
        public Dictionary<string, bool> Selected { get; set; } = new Dictionary<string, bool>();
        void updateSelected()
        {
            var sweepProperties = SweepProperties.Select(x=>x.Name).ToArray();
            foreach (var prop in sweepProperties)
            {
                if (Selected.ContainsKey(prop) == false)
                    Selected[prop] = true;
            }
            foreach (var item in Selected.ToArray())
            {
                if (item.Value && sweepProperties.Contains(item.Key))
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
        [HideOnMultiSelectAttribute] //TODO: Add support for multi-selecting this property.
        [Unsweepable]
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

        public SweepParameterStepBase()
        {
            Rules.Add(() => SelectedParameters.Count > 0, "No parameters selected to sweep", nameof(SelectedParameters));
        }
    }
}