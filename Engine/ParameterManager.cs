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
                    if (scope is ITestStepParent parent)
                    {
                        var forwardedMembers = TypeData.GetTypeData(scope)
                            .GetMembers().OfType<IParameterMemberData>();
                        var f = forwardedMembers.FirstOrDefault(x => owners.All(y => x.ContainsMember((y, member))));
                        if(f != null)
                            return (parent, f);
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
                        var t1 = existing?.TypeDescriptor;
                        var t2 = member?.TypeDescriptor;
                        if (t1 != null && t2 != null && typeComparison(t1, t2) != null)
                        {
                            var error = $"Cannot merge properties of this kind.";
                            yield return (error, error);
                            yield break;
                        }
                        var val = existing.GetValue(step);
                        var cloner = new ObjectCloner(val);
                        if (member.HasAttribute<UnmergableAttribute>() || existing.HasAttribute<UnmergableAttribute>())
                        {
                            var error = $"The selected property does not support merging.";
                            yield return (error, error);
                            yield break;
                        }

                        if (member is IParameterMemberData memberAsParameter && originalExisting is IParameterMemberData targetParameter)
                        { 
                            // Verify that a parameter does not get merged with another parameter that has it in its list of dependencies. (cyclic dependency).
                            var dependencyList = Utils.FlattenHeirarchy(
                                targetParameter.ParameterizedMembers.Select(x => x.Member)
                                    .OfType<IParameterMemberData>(),
                                x => x.ParameterizedMembers.Select(x2 => x2.Member).OfType<IParameterMemberData>());
                            if (dependencyList.Contains(memberAsParameter))
                            {
                                var error = $"The selected scope's parameter cannot be merged with a parameterization that it depends on.";
                                yield return (error, error);
                                yield break;
                            }
                        }
                        if( cloner.CanClone(step, member.TypeDescriptor) == false)
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
            [Layout(LayoutMode.FullRow | LayoutMode.WrapText, 3)]
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

            HashSet<ITypeData> numericTypes = new HashSet<ITypeData>() {
                TypeData.FromType(typeof(decimal)), TypeData.FromType(typeof(double)), TypeData.FromType(typeof(float)), 
                TypeData.FromType(typeof(long)), TypeData.FromType(typeof(int)), TypeData.FromType(typeof(short)), TypeData.FromType(typeof(sbyte)),
                TypeData.FromType(typeof(ulong)), TypeData.FromType(typeof(uint)), TypeData.FromType(typeof(ushort)), TypeData.FromType(typeof(byte))
            };
            string typeComparison(ITypeData firstType, ITypeData secondType)
            {
                if (Equals(firstType, secondType))
                    return null;
                if (numericTypes.Contains(firstType) && numericTypes.Contains(secondType))
                    return null;
                return "Value types must match to support merging parameters";
            }

            string validateName()
            {
                if (string.IsNullOrWhiteSpace(SelectedName))
                    return "Name cannot be left empty.";
              return null;
            }

            IMemberData member;

            ScopeMember[] scopeMembers;

            public string GetError() => getMessage().FirstOrDefault(x => x.error != null).error;
            
            bool isEdit => scopeMembers != null;
            object originalScope;
            public NamingQuestion(ITestStepParent[] source, IMemberData member, ScopeMember[] scopeMembers = null, object originalScope = null)
            {
                Rules.Add(() => validateName() == null, validateName, nameof(SelectedName));
                Rules.Add(() => GetError() == null, () => getMessage().FirstOrDefault(x => x.error != null).error, nameof(Message));
                this.member = member;
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
        
        [ThreadStatic]
        private static bool quickSanityCheck;

        public static IDisposable WithSanityCheckDelayed() => WithSanityCheckDelayed(false);
        
        public static IDisposable WithSanityCheckDelayed(bool quickCheck)
        {
            if (parameterSanityCheckDelayed)
                return Utils.WithDisposable(() => { });
            parameterSanityCheckDelayed = true;
            quickSanityCheck = quickCheck;
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

        public static bool CanUnparameter(ITestStepMenuModel menuItemModel) => CanUnparameter(menuItemModel.Member, menuItemModel.Source);
        public static bool CanParameter(ITestStepMenuModel menuItemModel) => CanParameter(menuItemModel.Member, menuItemModel.Source);
        public static bool CanEditParameter(ITestStepMenuModel menuItemModel) => IsValidParameter(menuItemModel.Member, menuItemModel.Source, false);
        
        static bool isParameterized(ITestStepParent item, IMemberData member) => item.GetParents().Any(parent =>
            TypeData.GetTypeData(parent).GetMembers().OfType<ParameterMemberData>()
                .Any(x => x.ContainsMember(item, member)));
        static bool IsValidParameter(IMemberData property, ITestStepParent[] steps, bool checkTestPlan = true)
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
                if (checkTestPlan && x is TestPlan) return false;
            }

            return true;
        }

        public static bool CanParameter(IMemberData property, ITestStepParent[] steps)
        {
            if (IsValidParameter(property, steps) == false)
                return false;
            return !steps.Any(step => isParameterized(step, property));
        }
        public static bool CanUnparameter(IMemberData property, ITestStepParent[] steps)
        {
            if (IsValidParameter(property, steps) == false)
                return false;
            return steps.Any(step => isParameterized(step, property));
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
            List<ITestStepParent> targets = new List<ITestStepParent>();
            using (WithSanityCheckDelayed())
            {
                foreach (var src in data.Source)
                {
                    IMemberData property;
                    var (scope, member) = ScopeMember.GetScope(new[] {src}, data.Member);
                    if (scope == null || member == null)
                    {
                        // this may occur if src does not actually have the right member, but just a member with the same name.
                        // in this case we just extract the member with the right name and use that.
                        property = TypeData.GetTypeData(src).GetMember(data.Member.Name);
                        (scope, member) = ScopeMember.GetScope(new[]
                        {
                            src
                        }, property);
                    }
                    else
                    {
                        property = data.Member;
                    }
                    
                    if (scope is ITestStepParent p)
                    {
                        targets.Add(p);
                        property.Unparameterize((ParameterMemberData) member, src);
                    }
                }
            }

            foreach (var src in data.Source.Concat(targets))
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

        public static bool CheckParameterSanity(ParameterMemberData parameter, bool overrideCache = false)
        {
            if (parameter.Target is ITestStepParent par)
                return CheckParameterSanity(par, new IMemberData[] {parameter}, overrideCache);
            return false;
        }
        
        public static bool CheckParameterSanity(ITestStepParent step, ICollection<IMemberData> parameters, bool overrideCache = false)
        {
            if (parameterSanityCheckDelayed)
            {
                // in some cases we may want to do a sanity check even if it has been delayed. 
                if(quickSanityCheck == false)
                    return true;
                var changeid = recordedChangeIds.GetValue(step, x => new ChangeId());
                if (changeid.Value == step.ChildTestSteps.ChangeId)
                    return true;
            }
            foreach (var elem in parameters)
            {
                if (elem is ParameterMemberData)
                    return checkParameterSanity(step, parameters, overrideCache);
            }
            return true;
        }
        
        /// <summary>
        /// Verify that source of a declared parameter on a parent also exists in the step hierarchy.
        /// </summary>
        public static bool checkParameterSanity(ITestStepParent step, ICollection<IMemberData> parameters, bool overrideCache = false)
        {
            bool isSane = true;
            var changeid = recordedChangeIds.GetValue(step, x => new ChangeId());
            foreach (var _item in parameters.ToArray())
            {
                if (_item is ParameterMemberData item)
                {
                    
                    if (changeid.Value == step.ChildTestSteps.ChangeId && item.AnyDynamicMembers == false && !overrideCache)
                    {
                        continue;
                    }
                    foreach (var fwd in item.ParameterizedMembers)
                    {
                        // Multiple situations possible.
                        // 1. the step is no longer a child of the parent to which it has parameterized a setting.
                        // 2. the member of a parameter no longer exists.
                        // 3. the child has been deleted from the step heirarchy.

                        var parameterSource = fwd.Source as ITestStepParent;
                        if (parameterSource == null) continue;
                        var member = fwd.Member;
                        bool isParent = false;
                        if (parameterSource == step)
                        {
                            // the parameter is parementerized on itself.
                            isParent = true;
                        }
                        else if (changeid.Value != step.ChildTestSteps.ChangeId || overrideCache)
                        {
                            // now iterate up to confirm that there is an unbroken chain between the parameter source and target.
                            var stepi = parameterSource;
                            while (stepi != null)
                            {
                                if (stepi.Parent == step)
                                {
                                    isParent = true;
                                    break;
                                }
                                stepi = stepi.Parent;
                            }
                        }
                        else
                        {
                            isParent = true;
                        }

                        if (member is IParameterMemberData)
                            isSane &= CheckParameterSanity(parameterSource, [member]);

                        bool memberDisposed = member is IDynamicMemberData dynamicMember && dynamicMember.IsDisposed;

                        if (!memberDisposed && member is EmbeddedMemberData emb)
                        { // This is a special case, if the member is not as such dynamic, but an embedded member, then the owner member could have been disposed.
                            if (emb.OwnerMember is IDynamicMemberData dynamicMember2 && dynamicMember2.IsDisposed)
                                memberDisposed = true;
                        }
                        if (memberDisposed || isParent == false )
                        {
                            member.Unparameterize(item, parameterSource);
                            if (!isParent)
                                log.Info("Step {0} is no longer a child step of the parameter owner. Removing from {1}.",
                                    (parameterSource as ITestStep)?.GetFormattedName() ?? parameterSource?.ToString(), item.Name);
                            else
                                log.Warning("Member {0} no longer exists, unparameterizing member.", member.Name);
                            isSane = false;
                        }
                    }
                }
            }
            // only update the change id for this step if sanity check passed.
            if(isSane)
                changeid.Value = step.ChildTestSteps.ChangeId;

            return isSane;
        }
        
        static bool checkParameterSanity(ITestStepParent step)
        {
            if (step == null) return true;
            
            ParameterMemberData[] parameters;
            using(WithSanityCheckDelayed()) // sanity checks are being done inside check members.
                parameters = TypeData.GetTypeData(step).GetMembers().OfType<ParameterMemberData>().ToArray();
            var isSane = CheckParameterSanity(step, parameters);
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
