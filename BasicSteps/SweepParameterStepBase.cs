using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Plugins.BasicSteps
{

    interface ISelectedParameters
    {
        IList<ParameterMemberData> SelectedParameters { get; }
    }

    public class MemberDataName
    {
        public string Group;
        public string Name;

        public MemberDataName(string name)
        {
            var split = name.Split('\\').Select(x => x.Trim())
                .Where(x => string.IsNullOrWhiteSpace(x) == false).ToArray();
            Name = split.LastOrDefault();
            Group = string.Join(" \\ ", split.Take(split.Length - 1));
        }

        public override bool Equals(object obj)
        {
            if (obj is MemberDataName other)
                return other.Name == Name && other.Group == Group;
            return false;
        }

        public override int GetHashCode() =>
            (Group?.GetHashCode() ?? 312321) * 73106 + (Name?.GetHashCode() ?? 60362190) * -32032;

        public override string ToString() => Name;
    }
    
    public abstract class SweepParameterStepBase : LoopTestStep, ISelectedParameters
    {
        internal IEnumerable<ParameterMemberData> SweepProperties => TypeData.GetTypeData(this)
            .GetMembers().OfType<ParameterMemberData>()
            .Where(x => x.HasAttribute<UnsweepableAttribute>() == false && x.Writable && x.Readable);

        internal IEnumerable<ParameterMemberData> SelectedMembers =>
            SweepProperties.Where(x => Selected.ContainsKey(x.Name) && Selected[x.Name]);

        public IEnumerable<ParameterMemberData> AvailableParameters => SweepProperties;
        
        readonly NotifyChangedList<ParameterMemberData> selectedProperties = new NotifyChangedList<ParameterMemberData>();
        
        [Browsable(false)]
        public Dictionary<string, bool> Selected { get; set; } = new Dictionary<string, bool>();
        void updateSelected(bool destructive = false)
        {
            var sweepProperties = SweepProperties.ToHashSet();
            var sweepProps2 = sweepProperties.ToDictionary(x => x.Name);
            foreach (var prop in sweepProperties)
            {
                if (Selected.ContainsKey(prop.Name) == false)
                    Selected[prop.Name] = true;
            }
            foreach (var item in Selected.ToArray())
            {
                ParameterMemberData prop;
                sweepProps2.TryGetValue(item.Key, out prop);
                if (item.Value && prop != null)
                {
                    
                    if (selectedProperties.Contains(prop) == false)
                        selectedProperties.Add(prop);
                }
                else
                {
                    selectedProperties.RemoveIf(x => x.Name == item.Key);
                }

                if (destructive && prop == null)
                {
                    Selected.Remove(item.Key);
                }
            }
        }

        void onListChanged(IList<ParameterMemberData> list)
        {
            foreach (var item in Selected.Keys.ToArray())
            {
                Selected[item] = list.Any(x => x.Name == item);
            }
        }
        
        [AvailableValues(nameof(AvailableParameters))]
        [XmlIgnore]
        [Browsable(true)]
        [HideOnMultiSelectAttribute] //TODO: Add support for multi-selecting this property.
        [Unsweepable]
        [Display("Parameters", "These are the parameters that should be swept", "Sweep")]
        public IList<ParameterMemberData> SelectedParameters {
            get
            {
                updateSelected(true);
                selectedProperties.ChangedCallback = onListChanged;
                return selectedProperties;
            }
            set
            {
                updateSelected();
                onListChanged(value);
            } 
        }

        public string Parameters
        {
            get
            {
                var names = SelectedParameters.Select(x => x.GetDisplayAttribute().Name);
                return string.Join(", ", names);
            }
        }

        public SweepParameterStepBase()
        {
            Rules.Add(() => SelectedParameters.Count > 0, "No parameters selected to sweep", nameof(SelectedParameters));
        }
    }
}