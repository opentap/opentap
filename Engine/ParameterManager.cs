using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTap
{
    static class ParameterManager
    {
        public class ScopeMember
        {
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
                return TypeData.GetTypeData(Scope.Object).GetMembers()
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

            IEnumerable<(string message, string error)> getMessage()
            {
                var selectedName = SelectedName?.Trim() ?? "";
                
                if(Scope.Object is ITestStepParent step)
                {
                    string name;
                    if (step is TestPlan plan)
                        name = $"test plan '{plan.Name}'";
                    else
                        name = $"test step '{(step as ITestStep)?.GetFormattedName()}'";
                    var existing = TypeData.GetTypeData(step).GetMember(selectedName.Trim());
                    var originalExisting = TypeData.GetTypeData(step).GetMember(defaultFullName);

                    if (existing != null && (step != originalScope || !ReferenceEquals(originalExisting, existing)))
                    {
                        var val = existing.GetValue(step);
                        var cloner = new ObjectCloner(val);
                        if (cloner.CanClone(step, memberType) == false)
                        {
                            var error = $"Cannot merge properties of this kind.";
                            yield return (error, error);
                            yield break;
                        }
                        else
                        {
                            yield return ($"Merge with an existing parameter on {name}.", null);
                        }
                    }
                    else if(!isEdit)
                        yield return ($"Create new parameter on {name}.", null);

                    if (isEdit)
                    {
                        var availSettingsCount = AvailableSettings.Count();
                        if (Settings.Count == 0)
                        {
                            yield return ("Remove the parameter.", null);
                            yield break;
                        } 
                        if (Settings.Count < availSettingsCount)
                            yield return ("Remove settings from being controlled by the parameter.", null);
                        
                        if (step == originalScope)
                        {
                            if (Equals(defaultFullName, selectedName) == false)
                                yield return ($"Rename parameter to '{SelectedName}'.", null);
                        }
                        else
                            yield return ($"Move parameter to {name}.", null);
                    }
                }
            }

            [Display("Message", Order: 1)]
            [Layout(LayoutMode.FullRow, 3)]
            [Browsable(true)]
            public string Message => string.Join("\n", getMessage().Select(x => x.message));

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
                    return string.Join(" \\ ", defaultGroup.Append(defaultName).Select(x => x.Trim()));
                }
            }

            string validateName()
            {
                if (string.IsNullOrWhiteSpace(SelectedName))
                    return "Name cannot be left empty.";
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

            public string GetError() => getMessage().FirstOrDefault(x => x.error != null).error;
            
            bool isEdit => scopeMembers != null;
            object originalScope;
            public NamingQuestion(ITestStepParent[] source, IMemberData member, ScopeMember[] scopeMembers = null, object originalScope = null)
            {
                Rules.Add(() => validateName() == null, validateName, nameof(SelectedName));
                Rules.Add(() => GetError() == null, () => getMessage().FirstOrDefault(x => x.error != null).error, nameof(Message));
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

        [ThreadStatic]
        private static bool parameterSanityCheckDelayed;
        
        public static IDisposable WithSanityCheckDelayed()
        {
            if (parameterSanityCheckDelayed)
                return Utils.WithDisposable(() => { });
            parameterSanityCheckDelayed = true;
            return Utils.WithDisposable(() => parameterSanityCheckDelayed = false);
        } 
        
        public static void Parameterize(ITestStepParent scope, IMemberData targetMember, ITestStepParent[] source, string selectedName)
        {
            var currentMember = TypeData.GetTypeData(scope).GetMember(selectedName);
            object currentValue = currentMember?.GetValue(scope);
            using (WithSanityCheckDelayed())
            {
                foreach (var src in source)
                {
                    // Try to fetch the member, multi select might make some members hide each-other non-perfectly.
                    var member = TypeData.GetTypeData(src).GetMember(targetMember.Name) ?? targetMember;
                    var newMember = member.Parameterize(scope, src, selectedName);
                    if (currentValue != null)
                        newMember.SetValue(scope, currentValue);
                }
            }
        }
        
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
            Parameterize(scope, s.Member, source, parameterUserRequest.SelectedName);
        }

        public static bool UnmergableListType(IMemberData property)
        {
            var propertyType = property.TypeDescriptor;
            // merging IEnumerable is not supported (unless its a string).
            if (propertyType.DescendsTo(typeof(IEnumerable)))
            {
                if (propertyType.IsA(typeof(string)) == false)
                {
                    var elementType = propertyType.AsTypeData()?.ElementType;
                    if (elementType == null || elementType.IsNumeric == false)
                        return true;
                }
            }

            return false;
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
            
            var prevScope = parameterUserRequest.Scope;
            
            UserInput.Request(parameterUserRequest, true);
            if (parameterUserRequest.Response == OkCancel.Cancel || string.IsNullOrWhiteSpace(parameterUserRequest.Name))
                return;

            var err = parameterUserRequest.Error;
            if (string.IsNullOrWhiteSpace(err) == false)
            {
                log.Error("{0}", err);
                return; 
            }

            using (WithSanityCheckDelayed())
            {
                foreach (var oldScopeMember in scopeMembers)
                {
                    if (parameterUserRequest.SelectedName != member.Name ||
                        parameterUserRequest.Settings.Contains(oldScopeMember) == false ||
                        prevScope.Object != parameterUserRequest.Scope.Object)
                        oldScopeMember.Member.Unparameterize((ParameterMemberData) ui.Member, oldScopeMember.Scope);
                }

                var target = parameterUserRequest.Scope.Object;
                var selectedName = parameterUserRequest.SelectedName;
                bool anyCreated = false;
                foreach (var newScopeMember in parameterUserRequest.Settings)
                {
                    var scope = newScopeMember.Scope;
                    if (selectedName != member.Name ||
                        scopeMembers.Contains(newScopeMember) == false ||
                        prevScope.Object != target)
                    {
                        newScopeMember.Member.Parameterize(target, scope, selectedName);
                        anyCreated = true;
                    }
                }
                
                if (anyCreated)
                {
                    // Update the value, to make sure all parameterized properties
                    // has the right value.
                    var param = TypeData.GetTypeData(target).GetMember(selectedName);
                    param.SetValue(target, param.GetValue(target));
                }
            }

            var step = ui.Source.First();
            checkParameterSanity(step);
        }

        public static bool CanParameter(ITestStepMenuModel menuItemModel) => CanParameter(menuItemModel.Member, menuItemModel.Source);
        
        static bool isParameterized(ITestStepParent item, IMemberData member) => item.GetParents().Any(parent =>
            TypeData.GetTypeData(parent).GetMembers().OfType<ParameterMemberData>()
                .Any(x => x.ParameterizedMembers.Contains((item, Member: member))));
        public static bool CanParameter(IMemberData property, ITestStepParent[] steps)
        {
            if (steps.Length == 0) return false;
            if (property == null) return false;
            if (property.HasAttribute<System.Xml.Serialization.XmlIgnoreAttribute>())
            {
                // XmlIgnored properties cannot be serialized, so external property does not work for them.
                return false;
            }

            if (property.HasAttribute<UnparameterizableAttribute>())
                return false;

            foreach (var x in steps)
            {
                if (property.Readable == false || property.Writable == false) return false;
                if (x is ITestStep step && step.IsReadOnly)
                    return false;
                if (x is TestPlan) return false;
            }

            if (steps.Any(step => isParameterized(step, property)))
                return false;
            return true;
        }
        
        public static bool CanAutoParameterize(IMemberData property, ITestStepParent[] steps )
        {
            if (steps.Length == 0) return false;
            if (property == null) return false;

            var parameterUserRequest = new NamingQuestion(steps, property);

            var err = parameterUserRequest.Error;
            if (string.IsNullOrWhiteSpace(err) == false)
                return false;
            
            return true;
        }

        public static void RemoveParameter(ITestStepMenuModel data)
        {
            if (data.Member is ParameterMemberData member)
            {
                foreach (var (obj, mem) in member.ParameterizedMembers.ToArray())
                    mem.Unparameterize(member, obj);
            }
            else
            {
                throw new Exception("Invalid parameter");
            }
        }
        
        public static void Unparameterize(ITestStepMenuModel data)
        {
            using (WithSanityCheckDelayed())
            {
                foreach (var src in data.Source)
                {
                    var (scope, member) = ScopeMember.GetScope(new[] {src}, data.Member);
                    IMemberData property = data.Member;
                    
                    if (scope is ITestStep)
                    {
                        property.Unparameterize((ParameterMemberData) member, src);
                    }
                    else if (scope is TestPlan plan)
                    {
                        if (property != null)
                            plan.ExternalParameters.Remove(src as ITestStep, property);
                    }
                }
            }

            foreach (var src in data.Source)
            {
                checkParameterSanity(src);
            }
        }

        class ChangeId
        {
            public int Value { get; set; }
        }
        // used to store test plan change ids
        // if the test plan did not change since last sanity check,
        // then we can do it a lot faster.
        static readonly ConditionalWeakTable<ITestStepParent, ChangeId> recordedChangeIds = new ConditionalWeakTable<ITestStepParent, ChangeId>();

        public static bool CheckParameterSanity(ITestStepParent step, IMemberData[] parameters)
        {
            if (parameterSanityCheckDelayed) return true;
            foreach (var elem in parameters)
            {
                if (elem is ParameterMemberData)
                    return checkParameterSanity(step, parameters);
            }
            return false;
        }
        
        /// <summary>
        /// Verify that source of a declared parameter on a parent also exists in the step hierarchy.
        /// </summary>
        public static bool checkParameterSanity(ITestStepParent step, IMemberData[] parameters)
        {
            bool isSane = true;
            var changeid = recordedChangeIds.GetValue(step, x => new ChangeId());
            foreach (var _item in parameters)
            {
                if (_item is ParameterMemberData item)
                {
                    
                    if (changeid.Value == step.ChildTestSteps.ChangeId && item.AnyDynamicMembers == false)
                    {
                        continue;
                    }
                    foreach (var fwd in item.ParameterizedMembers.ToArray())
                    {
                        var src = fwd.Source as ITestStepParent;
                        if (src == null) continue;
                        var member = fwd.Member;
                        bool isParent = false;
                        bool unparented = false;
                        var subparent = src.Parent;

                        if (changeid.Value != step.ChildTestSteps.ChangeId)
                        {                            
                            // Multiple situations possible.
                            // 1. the step is no longer a child of the parent to which it has parameterized a setting.
                            // 2. the member of a parameter no longer exists.
                            // 3. the child has been deleted from the step heirarchy.
                            if (subparent != null && (src is ITestStep step2 &&
                                                      subparent.ChildTestSteps.GetStep(step2.Id) == null))
                                unparented = true;
                            if (subparent != step)
                            {
                                while (subparent != null)
                                {
                                    if (subparent.Parent != null &&
                                        subparent.Parent.ChildTestSteps.GetStep((subparent as ITestStep).Id) == null)
                                        unparented = true;
                                    if (subparent == step)
                                    {
                                        isParent = true;
                                        break;
                                    }

                                    subparent = subparent.Parent;
                                }
                            }
                            else
                            {
                                isParent = true;
                            }
                        }
                        else
                        {
                            isParent = true;
                        }

                        if (member is IParameterMemberData)
                            CheckParameterSanity(src, new[] {member});
                        
                        bool memberDisposed = member is IDynamicMemberData dynamicMember && dynamicMember.IsDisposed;
                        if (memberDisposed || isParent == false || unparented)
                        {
                            member.Unparameterize(item, src);
                            if (!isParent || unparented)
                                log.Warning("Step {0} is no longer a child step of the parameter owner. Removing from {1}.",
                                    (src as ITestStep)?.GetFormattedName() ?? src?.ToString(), item.Name);
                            else
                                log.Warning("Member {0} no longer exists, unparameterizing member.", member.Name);
                            isSane = false;
                        }
                    }
                }
            }
            // only update the change id for this step if sanity check passed.
            changeid.Value = step.ChildTestSteps.ChangeId;

            return isSane;
        }
        
        static bool checkParameterSanity(ITestStepParent step)
        {
            bool isSane = true;
            if (step == null) return isSane;
            ParameterMemberData[] parameters;
            using(WithSanityCheckDelayed()) // sanity checks are being done inside check members.
                parameters = TypeData.GetTypeData(step).GetMembers().OfType<ParameterMemberData>().ToArray();
            isSane = CheckParameterSanity(step, parameters);
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
