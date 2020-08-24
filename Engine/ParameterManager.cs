using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace OpenTap
{
    static class ParameterManager
    {
        public class ScopeMember
        {
            public static IEnumerable<ScopeMember> GetScopeMembers(IMemberData member)
            {
                if (member is ParameterMemberData fwd)
                    return fwd.ParameterizedMembers
                        .Select(x => new ScopeMember((ITestStep)x.Source, x.Member));
                return Enumerable.Empty<ScopeMember>();
            }

            public ITestStepParent Scope { get; }
            public IMemberData Member { get; }
            
            public ScopeMember(ITestStepParent scope, IMemberData member)
            {
                Scope = scope;
                Member = member;
            }

            string scopeName
            {
                get
                {
                    switch (Scope)
                    {
                        case ITestStep step:
                            return step.GetFormattedName();
                        case TestPlan _: return "Test plan";
                        case object x: return x.ToString();
                        default: return "null";
                    }
                }
            }

            public override string ToString() => $"'{Member.GetDisplayAttribute().Name}' from '{scopeName}'";

            static IEnumerable<ITestStepParent> getScopes(ITestStepParent step)
            {
                while (step != null)
                {
                    yield return step;
                    step = step.Parent;
                }
            }

            static ITestStepParent[] getCommonParents(ITestStepParent[] array)
            {
                if (array.Length == 0) return Array.Empty<ITestStepParent>();
                var firstArray = getScopes(array[0].Parent).Reverse().ToArray();
                int cnt = firstArray.Length;
                for (int i = 1; i < array.Length; i++)
                {
                    var secondArray = getScopes(array[i].Parent).Reverse().ToArray();
                    cnt = Math.Min(cnt, secondArray.Length);
                    int j = 0;
                    foreach (var item in secondArray)
                    {
                        if (item != firstArray[j])
                        {
                            cnt = j; 
                            break;
                        }
                        j += 1;
                        if (j == cnt)
                            break;
                    }
                }
            
                Array.Resize(ref firstArray, cnt);
                Array.Reverse(firstArray);
                return firstArray;
            }
            
            public static IEnumerable<object> GetScopes(object source)
            {
                switch (source)
                {
                    case ITestStepParent[] array:
                        return getCommonParents(array);
                    case ITestStepParent step:
                        return getScopes(step.Parent);
                    default: return Array.Empty<object>();   
                }
            }

            
            public static (object Scope, IMemberData member) GetScope(object[] owners, IMemberData member)
            {
                foreach (var scope in GetScopes(owners))
                {
                    if (scope is ITestStep parent)
                    {
                        var forwardedMembers = TypeData.GetTypeData(scope)
                            .GetMembers().OfType<IParameterMemberData>();
                        var f = forwardedMembers.FirstOrDefault(x => owners.All(y => x.ParameterizedMembers.Contains((y, member))));
                        if(f != null)
                            return (parent, f);
                    }
                    else if (scope is TestPlan plan)
                    {
                        if(owners.All(step => plan.ExternalParameters.Find((ITestStep)step, member) != null))
                            return (plan, null);
                    }
                }

                return (null, null);
            }
        }

        [HelpLink("EditorHelp.chm::/CreatingATestPlan/Scoped Parameters/Readme.html")]
        public class NamingQuestion : ValidatingObject
        {
            public struct ScopeItem
            {
                public ITestStepParent Object { get; set; }
                public override string ToString()
                {
                    if (Object is ITestStep step)
                        return "Test Step '" + step.GetFormattedName() +"'";
                    if (Object is TestPlan plan)
                        return "Test Plan '" + plan.Name +"'";
                    return null;
                }
            }
            
            public string Name { get; internal set; }
            [Display("Name", "Name of the parameter. Groups are delimited by \\'." +
                             " This may be the same as an existing parameter name in which case they will be merged.")]
            [SuggestedValues(nameof(SuggestedNames))]
            //When merging the value type must be the same for all parameters." +
            // "\nIt cannot be the same as a non-parameter name.
            public string SelectedName
            { 
                get => selectedName;
                set
                {
                    if (value == "") return; // fixes a bug related to SuggestedValueAttribute 
                    selectedName = value;
                } 
            }
            string selectedName;
            IEnumerable<string> getMemberNames()
            {
                if (Scope.Object == null) return Enumerable.Empty<string>();
                return TypeData.GetTypeData(Scope.Object).GetMembers()//.Where(GenericGui.FilterDefault2)
                    .OfType<IParameterMemberData>()
                    .Select(x => x.Name);
            }

            public IEnumerable<string> SuggestedNames => new[] {defaultFullName}
                .Concat(getMemberNames())
                .Distinct();
            
            [Display("Scope", "The location of the parameter. This must be a parent of the step(s) containing the settings.")]
            [AvailableValues(nameof(AvailableScopes))]
            public ScopeItem Scope
            {
                get => scope;
                set
                {
                    bool useDefaultName = SelectedName == defaultFullName;
                    scope = value;
                    if (useDefaultName)
                        SelectedName = defaultFullName;
                } 
            }

            ScopeItem scope;

            IEnumerable<string> getMessage()
            {
                var selectedName = SelectedName.Trim();
                if (Scope.Object is TestPlan plan)
                {
                    if (plan.ExternalParameters.Get(SelectedName) != null)
                    {
                        yield return $"Merge with an existing external test plan parameter named '{selectedName}'.";
                    }
                    else
                    {
                        yield return $"Create an external test plan parameter named '{selectedName}'.";
                    }
                }
                if(Scope.Object is ITestStep step)
                {
                    var name = step.GetFormattedName();
                    if (TypeData.GetTypeData(step).GetMember(selectedName.Trim()) != null && step != originalScope)
                        yield return $"Merge with an existing parameter on test step '{name}'.";
                    else if(!isEdit)
                        yield return $"Create new parameter on test step '{name}'.";

                    if (isEdit)
                    {
                        var availSettingsCount = AvailableSettings.Count();
                        if (Settings.Count == 0)
                        {
                            yield return "Remove the parameter.";
                            yield break;
                        } 
                        if (Settings.Count < availSettingsCount)
                            yield return "Remove settings from being controlled by the parameter.";
                        
                        if (step == originalScope)
                        {
                            if (Equals(defaultFullName, selectedName) == false)
                                yield return $"Rename parameter to '{SelectedName}'.";
                        }
                        else
                            yield return $"Move parameter to '{step.GetFormattedName()}'.";
                    }
                }
            }

            [Display("Message", Order: 1)]
            [Layout(LayoutMode.FullRow, 3)]
            [Browsable(true)]
            public string Message => string.Join("\n", getMessage());

            public IEnumerable<object> AvailableSettings => scopeMembers;

            public bool SelectedSettingAvailable => scopeMembers != null;
            
            [AvailableValues(nameof(AvailableSettings))]
            [EnabledIf(nameof(SelectedSettingAvailable), true, HideIfDisabled = true)]
            [Display("Settings", "Select which settings are controlled by this parameter. To remove the parameter, deselect all of them.")]
            public List<ScopeMember> Settings { get; set; }
            
            public IEnumerable<ScopeItem> AvailableScopes => ScopeMember.GetScopes(source).OfType<ITestStepParent>()
                .Select(x => new ScopeItem{Object = x});

            readonly ITestStepParent[] source;

            readonly string defaultName;
            // default group (if step parameter)
            readonly string[] defaultGroup;

            public string OverrideDefaultName;
            string defaultFullName
            {
                get
                {
                    if (OverrideDefaultName != null)
                        return OverrideDefaultName;
                    if(Scope.Object is ITestStep)
                        return string.Join(" \\ ", defaultGroup.Append(defaultName).Select(x => x.Trim()));
                    return defaultName;
                }
            }

            string validateName()
            {
                var selectedName = SelectedName.Trim();
                if (Scope.Object is TestPlan plan)
                {
                    var ext = plan.ExternalParameters.Get(selectedName);
                    if (ext == null) return null; // fine
                    if(false == Equals(memberType, ext.PropertyInfos.FirstOrDefault().TypeDescriptor))
                        return "Value types must match to support merging parameters";
                    
                    return null;
                }

                if(Scope.Object is ITestStep step)
                {
                    var type = TypeData.GetTypeData(step);
                    var currentMember = type.GetMember(selectedName);
                    if (currentMember == null) return null;
                    if (currentMember is ParameterMemberData)
                    {
                        if (false == Equals(currentMember.TypeDescriptor, memberType))
                            return "Value types must match to support merging parameters.";

                        return null;
                    }
                    return "Selected name is already used for a setting.";
                }

                return null;
            }

            ITypeData memberType;

            ScopeMember[] scopeMembers;

            bool isEdit => scopeMembers != null;
            object originalScope;
            public NamingQuestion(ITestStepParent[] source, IMemberData member, ScopeMember[] scopeMembers = null, object originalScope = null)
            {
                Rules.Add(() => validateName() == null, validateName, nameof(SelectedName));
                memberType = member.TypeDescriptor;
                this.source = source;
                this.scopeMembers = scopeMembers;
                this.originalScope = originalScope; 
                Settings = scopeMembers?.ToList();
                var display = member.GetDisplayAttribute();
                Name = "Parameterize '" + display.Name + "'";
                
                if (display.Group.FirstOrDefault() == "Parameters")
                    defaultGroup = display.Group;
                else
                    defaultGroup = new[] {"Parameters"}.Concat(display.Group).ToArray();

                defaultName = display.Name;
                
                Scope = AvailableScopes.FirstOrDefault();
                SelectedName = defaultFullName;
            }
            
            [Submit]
            [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
            public OkCancel Response { get; set; }
        }
        public enum OkCancel
        {
            [Display("OK", "Accept the selected configuration.")]
            Ok = 1,
            [Display("Cancel", "Discard changes to the test plan.")]
            Cancel = 2
        }

        static TraceSource log = Log.CreateSource("Parameter");

        public static void CreateParameter(ITestStepMenuModel s, ITestStepParent preselectedScope, bool showDialog)
        {
            var source = s.Source;

            var parameterUserRequest = new NamingQuestion(source, s.Member);
            if (preselectedScope != null)
                parameterUserRequest.Scope = new NamingQuestion.ScopeItem {Object = preselectedScope};

            if (showDialog)
                UserInput.Request(parameterUserRequest, modal: true);
            parameterUserRequest.SelectedName = parameterUserRequest.SelectedName.Trim();
            if (parameterUserRequest.Response == OkCancel.Cancel)
                return;
            var err = parameterUserRequest.Error;
            if (string.IsNullOrWhiteSpace(err) == false)
            {
                log.Error("{0}", err);
                return;
            }

            var scope = parameterUserRequest.Scope.Object;
            var currentMember = TypeData.GetTypeData(scope).GetMember(parameterUserRequest.SelectedName);
            object currentValue = currentMember?.GetValue(scope);

            foreach (var src in source)
            {
                // Try to fetch the member, multi select might make some members hide each-other non-perfectly.
                var member = TypeData.GetTypeData(src).GetMember(s.Member.Name) ?? s.Member;
                var newMember = member.Parameterize(scope, src, parameterUserRequest.SelectedName);
                if (currentValue != null)
                    newMember.SetValue(scope, currentValue);
            }
        }
        
        public static void EditParameter(ITestStepMenuModel ui)
        {
            var member = (ParameterMemberData)ui.Member; 
            var scopeMembers = member.ParameterizedMembers.Select(x => new ScopeMember((ITestStepParent)x.Source, x.Member)).ToArray();
            var parameterUserRequest = new NamingQuestion(scopeMembers.Select(x => (ITestStepParent)x.Scope).ToArray(), ui.Member, scopeMembers, originalScope: ui.Source.First())
            {
                Name = $"Edit Parameter '{ui.Member.GetDisplayAttribute().Name}'"
            };
            parameterUserRequest.Scope = new NamingQuestion.ScopeItem { Object = ui.Source.First() };
            parameterUserRequest.SelectedName = ui.Member.Name;
            parameterUserRequest.OverrideDefaultName = ui.Member.Name;
            UserInput.Request(parameterUserRequest);
            if (parameterUserRequest.Response == OkCancel.Cancel || string.IsNullOrWhiteSpace(parameterUserRequest.Name))
                return;

            var err = parameterUserRequest.Error;
            if (string.IsNullOrWhiteSpace(err) == false)
            {
                log.Error("{0}", err);
                return; 
            }
            
            foreach (var scopeMember in scopeMembers)
                scopeMember.Member.Unparameterize((ParameterMemberData)ui.Member, scopeMember.Scope);
            
            foreach (var scopemember in parameterUserRequest.Settings)
                scopemember.Member.Parameterize(parameterUserRequest.Scope.Object, scopemember.Scope, parameterUserRequest.SelectedName);
            
            var step = ui.Source.First();
            checkParameterSanity(step);
        }

        public static bool CanParameter(ITestStepMenuModel menuItemModel) => CanParameter(menuItemModel.Member, menuItemModel.Source);
        
        public static bool CanParameter(IMemberData property, ITestStepParent[] steps )
        {
            if (property != null && property.HasAttribute<System.Xml.Serialization.XmlIgnoreAttribute>())
            {
                // XmlIgnored properties cannot be serialized, so external property does not work for them.
                return false;
            }


            if (steps.Any(x => ((x is ITestStep step && step.IsReadOnly) && (x is TestPlan == false)) || x is TestPlan || property == null || property.Readable == false ||
                               property.Writable == false))
            {
                return false;
            }

            object converted = null;
            try
            {
                var value = property.GetValue(steps.FirstOrDefault());
                // check that conversion can be done both ways before allowing to add as an external parameter.
                if (StringConvertProvider.TryGetString(value, out string str))
                    StringConvertProvider.TryFromString(str, property.TypeDescriptor, steps, out converted);
            }
            catch
            {
                converted = null;
            }

            if (converted == null && property.TypeDescriptor.IsA(typeof(string)) == false)
            {
                // No StringConvertProvider can handle this type.
                return false;
            }

            return true;
        }

        public static void RemoveParameter(ITestStepMenuModel data)
        {
            var (scope, member) = ScopeMember.GetScope(data.Source, data.Member);
            IMemberData property = data.Member;
            var items = data.Source;

            if (scope is ITestStep)
            {
                foreach (var item in items)
                    property.Unparameterize((ParameterMemberData)member, item);
            }
            else if (scope is TestPlan plan)
            {
                if (property != null)
                    foreach (var item in items.OfType<ITestStep>())
                        plan.ExternalParameters.Remove(item, property);
            }

            checkParameterSanity(scope as ITestStepParent);
        }

        public static void CheckParameterSanity(ITestStepParent step)
        {
            checkParameterSanity(step);
        }
        
        static bool checkParameterSanity(ITestStepParent step)
        {
            bool isSane = true;
            if (step == null) return isSane;
            var forwarded = TypeData.GetTypeData(step).GetMembers().OfType<ParameterMemberData>();
            foreach (var item in forwarded.ToArray())
            {
                foreach (var fwd in item.ParameterizedMembers.ToArray())
                {
                    var src = fwd.Source;
                    var mem = fwd.Member;
                    var existing = TypeData.GetTypeData(src).GetMember(mem.Name);
                    bool isParent = false;
                    bool unparented = false;
                    var subparent = (src as ITestStep)?.Parent;
                    
                    // Multiple situations possible.
                    // 1. the step is no longer a child of the parent to which it has parameterized a setting.
                    // 2. the member of a parameter no longer exists.
                    // 3. the child has been deleted from the step heirarchy.
                    if (subparent != null && subparent.ChildTestSteps.Contains(src) == false)
                        unparented = true;
                    while (subparent != null)
                    {
                        if (subparent.Parent != null && subparent.Parent.ChildTestSteps.Contains(subparent) == false)
                            unparented = true;
                        if (subparent == step)
                        {
                            isParent = true;
                        }
                        subparent = subparent.Parent;
                    }
                    if (existing == null || isParent == false || unparented)
                    {
                        mem.Unparameterize(item, src);
                        if (!isParent || unparented)
                            log.Warning("Step {0} is no longer a child step of the parameter owner.", (src as ITestStep)?.GetFormattedName() ?? src?.ToString());
                        else
                            log.Warning("Member {0} no longer exists, unparameterizing member.", mem.Name);
                        isSane = false;
                    }
                }
            }
            if(step.Parent is ITestStepParent parent)
                return checkParameterSanity(parent) & isSane;
            return isSane;
        }
        
        
        public class SettingsName : IStringReadOnlyValueAnnotation
        {
            AnnotationCollection annotation;
            public SettingsName(AnnotationCollection a) => annotation = a;

            public string Value
            {
                get
                {
                    var members = annotation.Get<IObjectValueAnnotation>()?.Value as List<ScopeMember>;
                    if (members == null) return "";
                    return string.Join(", ", members.Select(x => x.ToString()));
                }
            }
        }
    }
}
