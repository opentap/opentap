//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenTap
{
    /// <summary> Annotators can be used to annotation objects with data for display. </summary>
    public interface IAnnotator : ITapPlugin
    {
        /// <summary> The priority of this annotator. Specifies which orders annotations are added. </summary>
        double Priority { get; }
        /// <summary> Implements annotation for an object. </summary>
        /// <param name="annotations">The current collection of annotations for an object. This method can add to the collection.</param>
        void Annotate(AnnotationCollection annotations);
    }

    /// <summary>
    /// Marker interface to indicate that a type represents annotation data for an object.
    /// </summary>
    public interface IAnnotation { }

    /// <summary> Gets or sets the value of a thing. </summary>
    public interface IObjectValueAnnotation : IAnnotation
    {
        /// <summary> Gets or sets the current value. Note, for the value to be written to the owner object, Annotation.Write has to be called.</summary>
        object Value { get; set; }
    }

    ///// <summary>
    ///// Specifies that an annotation depends on one or more other annotation(s).
    ///// </summary>
    //public class AnnotationAggregatorAttribute: Attribute
    //{
    //    /// <summary>
    //    /// Specifies that an annotation depends on one or more other annotation(s)
    //    /// </summary>
    //    /// <param name="types">Which types it depends on</param>
    //    public AnnotationAggregatorAttribute(params Type[] types)
    //    {

    //    }
    //}

    /// <summary> Specifies how available values proxies are implemented. This class should rarely be implemented. Consider implementing just IAvailableValuesAnnotation instead.</summary>
    //[AnnotationAggregator(typeof(IAvailableValuesAnnotation))]
    public interface IAvailableValuesAnnotationProxy : IAnnotation
    {
        /// <summary> Annotated available values. </summary>
        IEnumerable<AnnotationCollection> AvailableValues { get; }
        /// <summary> Annotated selected value. Not this should belong to the set of AvailableValues as well.</summary>
        AnnotationCollection SelectedValue { get; set; }
    }
    /// <summary> Specifies how suggested value proxies are implemented. This class should rarely be implemented. Consider implementing just ISuggestedValuesAnnotation instead.</summary>

    //[AnnotationAggregator(typeof(ISuggestedValuesAnnotation))]
    public interface ISuggestedValuesAnnotationProxy : IAnnotation
    {
        /// <summary>
        /// Annotated suggested values.
        /// </summary>
        IEnumerable<AnnotationCollection> SuggestedValues { get; }
        /// <summary>
        /// Annotated selected value.
        /// </summary>
        AnnotationCollection SelectedValue { get; set; }
    }

    /// <summary>Specifies how multi selection annotation proxies are implemented. Not this should rarely need to be implemented </summary>
    public interface IMultiSelectAnnotationProxy : IAnnotation
    {
        /// <summary> The annotated selected values. </summary>
        IEnumerable<AnnotationCollection> SelectedValues { get; set; }
    }

    /// <summary>
    /// Defines a available values implementation. Implement this to extend the data annotation system with a new available values.
    /// </summary>
    public interface IAvailableValuesAnnotation : IAnnotation
    {
        /// <summary> The available values. </summary>
        IEnumerable AvailableValues { get; }
    }

    /// <summary>
    /// Defines a suggested values implementation. 
    /// </summary>
    public interface ISuggestedValuesAnnotation : IAnnotation
    {
        /// <summary> The currently suggested values </summary>
        IEnumerable SuggestedValues { get; }
    }

    /// <summary>
    /// Defines a string value annotation implementation. This can be implemented for any type which can be converted to/from a string value. Note: IStringReadOnlyValueAnnotation can be implemented in the read-only case.
    /// </summary>
    public interface IStringValueAnnotation : IStringReadOnlyValueAnnotation
    {
        /// <summary> The string value representation of an object. The setter can throw an exception if the format is not correctly used. </summary>
        new string Value { get; set; }
    }

    /// <summary> Defines a read-only string value annotation implementation. </summary>
    public interface IStringReadOnlyValueAnnotation : IAnnotation
    {
        /// <summary> The string value representation of the object. </summary>
        string Value { get; }
    }

    /// <summary> Makes it possible to get an example of a value from a property. </summary>
    public interface IStringExampleValueAnnotation : IAnnotation
    {
        /// <summary> Gets an example of what the current value could be. </summary>
        string Example { get; }
    }

    /// <summary> Defines how an error annotation works. Note: Multiple of IErrorAnnotation can be used in the same annotation. In this case the erros will be concatenated. </summary>
    public interface IErrorAnnotation : IAnnotation
    {
        /// <summary> The list of errors for this annotation. </summary>
        IEnumerable<string> Errors { get; }
    }

    /// <summary> Specifies the access to an annotation. </summary>
    public interface IAccessAnnotation : IAnnotation
    {
        /// <summary> Gets if the annotation is read-only. This state can be temporary or permanent. </summary>
        bool IsReadOnly { get; }

        /// <summary> Gets if the annotation is visible. This state can be temporary or permanent. </summary>
        bool IsVisible { get; }
    }

    /// <summary>
    /// Owned annotations interacts directly with the source object. It is updated through the Read operation and changes are written with the Write operation. Specialized knowledge about the object is needed for implementation.
    /// </summary>
    public interface IOwnedAnnotation : IAnnotation
    {
        /// <summary> Read changes from the source. </summary>
        /// <param name="source"></param>
        void Read(object source);
        /// <summary> Write changes to the source. </summary>
        /// <param name="source"></param>
        void Write(object source);
    }

    /// <summary> Marks that an annotation reflects a member of an object. </summary>
    public interface IMemberAnnotation : IReflectionAnnotation
    {
        /// <summary>
        /// Gets the member.
        /// </summary>
        IMemberData Member { get; }
    }

    /// <summary> Reflects the type of the object value being annotated.</summary>
    public interface IReflectionAnnotation : IAnnotation
    {
        /// <summary>
        /// The reflection info object.
        /// </summary>
        ITypeData ReflectionInfo { get; }
    }

    /// <summary> The object can be used for multi select operations. Example: FlagAttribute enums can be multi-selected. </summary>
    public interface IMultiSelect : IAnnotation
    {
        /// <summary> The currently selected values. </summary>
        IEnumerable Selected { get; set; }
    }

    /// <summary> The annotation can be invoked to do some action. </summary> 
    public interface IMethodAnnotation : IAnnotation
    {
        /// <summary> Invokes the action.  </summary>
        void Invoke();
    }

    /// <summary> Specifies that the annotation reflects some kind of collection.</summary>
    public interface ICollectionAnnotation : IAnnotation
    {
        /// <summary> The reflected elements. </summary>
        IEnumerable<AnnotationCollection> AnnotatedElements { get; set; }
        /// <summary> Creates a new element that can be put into the collection. Note that initially the element should not be added to the collection. This task is for the user. </summary>
        /// <returns>The new element. </returns>
        AnnotationCollection NewElement();
    }

    /// <summary> The annotation reflects multiple members on an object. </summary>
    public interface IMembersAnnotation : IAnnotation
    {
        /// <summary> The reflected members. </summary>
        IEnumerable<AnnotationCollection> Members { get; }
    }

    /// <summary> Like IMembersAnnotation, but a specific member can be fetched.</summary>
    public interface INamedMembersAnnotation : IAnnotation
    {
        /// <summary> Returns the annotated member. </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        AnnotationCollection GetMember(IMemberData name);
    }

    /// <summary> Can be used to forward a set of members from one annotation to another.</summary>
    public interface IForwardedAnnotations : IAnnotation
    {
        /// <summary> The forwarded annotations. </summary>
        IEnumerable<AnnotationCollection> Forwarded { get; }
    }

    /// <summary> Interface for providing annotations with a way of explaining the value. </summary>
    public interface IValueDescriptionAnnotation : IAnnotation
    {
        /// <summary> Should return an</summary>
        /// <returns></returns>
        string Describe();
    }

    /// <summary>
    /// Annotates that a member is read only.
    /// </summary>
    public class ReadOnlyMemberAnnotation : IAccessAnnotation
    {
        /// <summary> Allways returns true.</summary>
        public bool IsReadOnly => true;
        /// <summary> Allways returns true.</summary>
        public bool IsVisible => true;
    }

    class MembersAnnotation : INamedMembersAnnotation,IMembersAnnotation, IOwnedAnnotation
    {
        Dictionary<IMemberData, AnnotationCollection> members = new Dictionary<IMemberData, AnnotationCollection>();
        IEnumerable<AnnotationCollection> getMembers()
        {
            
                var val2 = fac.Get<IObjectValueAnnotation>().Value;
                var val = fac.Get<IReflectionAnnotation>();
                var _members = val.ReflectionInfo.GetMembers();
                if (val2 != null)
                    _members = TypeData.GetTypeData(val2).GetMembers();
                if (members.Count == _members.Count()) return members.Values;

                foreach (var item in val.ReflectionInfo.GetMembers())
                {
                    if (members.ContainsKey(item)) continue;
                    members[item] = GetMember(item);
                }

                return members.Values;
            
        }

        public IEnumerable<AnnotationCollection> Members => getMembers();



        AnnotationCollection fac;
        public MembersAnnotation(AnnotationCollection fac)
        {
            this.fac = fac;
        }
        
        public void Read(object source)
        {
            var val = fac.Get<IObjectValueAnnotation>()?.Value;
            if (val == null) return;
            foreach(var mem in members)
            {
                mem.Value.Read(val);
            }
        }

        public void Write(object source)
        {
            var val = fac.Get<IObjectValueAnnotation>()?.Value;
            if (val == null) return;
            foreach (var mem in members)
            {
                mem.Value.Write(val);
            }
        }

        public AnnotationCollection GetMember(IMemberData member)
        {
            if (members.TryGetValue(member, out AnnotationCollection value)) return value;
            
            var annotation = fac.AnnotateMember(member);
            var objectValue = fac.Get<IObjectValueAnnotation>().Value;
            if (objectValue != null)
            {
                 annotation.Read(objectValue);
            }
            members[member] = annotation;
            return annotation;
        }
    }

    class EnabledIfAnnotation : IAccessAnnotation, IOwnedAnnotation
    {

        public bool IsReadOnly => isReadOnly;
        public bool IsVisible => isVisible;

        bool isReadOnly = false;
        bool isVisible = false;

        public void Read(object source)
        {
            isReadOnly = !EnabledIfAttribute
            .IsEnabled(mem.Member, source, out IMemberData prop, out IComparable val, out bool hidden);
            isVisible = !hidden;
        }

        public void Write(object source)
        {

        }

        IMemberAnnotation mem;
        public EnabledIfAnnotation(IMemberAnnotation mem)
        {
            this.mem = mem;
        }
    }

    class ErrorAnnotation : IErrorAnnotation
    {
        public List<string> Errors { get; set; } = new List<string>();

        IEnumerable<string> IErrorAnnotation.Errors => Errors;
    }

    class ValidationErrorAnnotation : IErrorAnnotation, IOwnedAnnotation
    {
        IMemberAnnotation mem;
        string error;
        public ValidationErrorAnnotation(IMemberAnnotation mem)
        {
            this.mem = mem;
        }

        public IEnumerable<string> Errors
        {
            get
            {
                if (string.IsNullOrWhiteSpace(error) == false)
                    yield return error;
            }
        }

        public void Read(object source)
        {
            if (source is IDataErrorInfo err)
            {
                try
                {
                    error = err[mem.Member.Name];
                }
                catch(Exception e)
                {
                    error = e.Message;
                }
            }
        }

        public void Write(object source)
        {

        }
    }


    class NumberAnnotation : IStringValueAnnotation
    {
        string currentError;
        public string Value
        {
            get
            {
                var value = annotation.Get<IObjectValueAnnotation>();
                if (value != null)
                {
                    var unit = annotation.Get<UnitAttribute>();
                    return new NumberFormatter(CultureInfo.CurrentCulture, unit).FormatNumber(value.Value);
                }
                return null;
            }
            set
            {
                string newerror = null;
                var unit = annotation.Get<UnitAttribute>();
                if (annotation.Get<IReflectionAnnotation>()?.ReflectionInfo is TypeData cst)
                {
                    object number = null;
                    try
                    {
                        number = new NumberFormatter(CultureInfo.CurrentCulture, unit).ParseNumber(value, cst.Type);
                    }
                    catch(Exception e)
                    {
                        newerror = e.Message;
                    }
                    finally
                    {
                        var err = annotation.Get<ErrorAnnotation>();
                        if (err != null)
                        {
                            if (currentError != null)
                                err.Errors.Remove(currentError);
                            currentError = newerror;
                            if (currentError != null)
                                err.Errors.Add(currentError);
                        }
                    }
                    if (number != null)
                    {
                        var val = annotation.Get<IObjectValueAnnotation>();
                        val.Value = number;
                    }
                    
                }
                else
                {
                    throw new InvalidOperationException("Number converter supports only C# types");
                }
            }
        }
        AnnotationCollection annotation;
        public NumberAnnotation(AnnotationCollection mem)
        {
            this.annotation = mem;
        }
    }

    class TimeSpanAnnotation : IStringValueAnnotation
    {
        public string Value
        {
            get
            {
                
                if(annotation.Get<IObjectValueAnnotation>(from: this).Value is TimeSpan timespan)
                    return TimeSpanFormatter.Format(timespan, fmt.Verbosity);
                return "";
            }

            set
            {
                var timespan = TimeSpanParser.Parse(value);
                annotation.Get<IObjectValueAnnotation>(from: this).Value = timespan;
            }
        }
        AnnotationCollection annotation;
        TimeSpanFormatAttribute fmt;
        public TimeSpanAnnotation(AnnotationCollection annotation)
        {
            fmt = annotation?.Get<IMemberAnnotation>()?.Member.GetAttribute<TimeSpanFormatAttribute>();
            if (fmt == null) fmt = new TimeSpanFormatAttribute();
            this.annotation = annotation;
        }
    }

    class NumberSequenceAnnotation : IStringValueAnnotation
    {
        public string Value
        {
            get
            {
                var member = mem.Get<IObjectValueAnnotation>();
                var value = (IEnumerable)member.Value;
                if (value == null) return "";
                var unit = mem.Get<UnitAttribute>();

                return new NumberFormatter(System.Globalization.CultureInfo.CurrentCulture, unit).FormatRange(value);
            }
            set
            {
                var objVal = mem.Get<IObjectValueAnnotation>();
                var reflect = mem.Get<IReflectionAnnotation>();
                var unit = mem.Get<UnitAttribute>();
                if ((reflect.ReflectionInfo as ITypeData) is TypeData cst)
                {
                    var numbers = DoConvertBack(value, cst.Type, unit, CultureInfo.CurrentCulture);
                    objVal.Value = numbers;
                }
                else
                {
                    throw new InvalidOperationException("Number converter supports only C# types");
                }
            }
        }

        public object DoConvertBack(object _value, Type targetType, UnitAttribute unit, CultureInfo culture)
        {
            string value = _value as string;
            if (value == null)
                return null;

            Type elementType = targetType.GetEnumerableElementType();

            IEnumerable seq = null;
            if (elementType.IsNumeric())
            {
                seq = new NumberFormatter(culture, unit).Parse(value).CastTo(elementType);
            }
            else
            {
                var items = value.Split(new string[] { culture.NumberFormat.NumberGroupSeparator }, StringSplitOptions.RemoveEmptyEntries);
                if (elementType.IsEnum)
                {
                    seq = items.Select(item => Enum.Parse(elementType, item));
                }
                else
                {
                    seq = items.Select(item => System.Convert.ChangeType(item, elementType));
                }
            }

            Type genericBaseType = null;
            if (targetType.IsArray)
            {
                elementType = targetType.GetElementType();
            }
            else if (targetType.IsGenericType)
            {
                genericBaseType = targetType.GetGenericTypeDefinition();
            }

            if (typeof(IEnumerable<>) == genericBaseType)
            {
                var genarg = targetType.GetGenericArguments().FirstOrDefault();
                if (genarg != null && genarg.IsNumeric())
                {
                    var comb = seq as ICombinedNumberSequence;
                    if (comb != null)
                        return comb.CastTo(genarg);
                    return seq.Cast<object>().Select(v => System.Convert.ChangeType(v, genarg));
                }
            }
            else if (seq.Cast<object>().IsLongerThan(100000))
                throw new Exception("Sequence is too large. (max number of elements is 100000).");

            if (targetType.IsArray)
            {
                Array array = Array.CreateInstance(elementType, seq.Cast<object>().Count());
                int idx = 0;
                foreach (var item in seq)
                    array.SetValue(System.Convert.ChangeType(item, elementType), idx++);
                return array;
            }
            else if (targetType.DescendsTo(typeof(System.Collections.ObjectModel.ReadOnlyCollection<>)))
            {
                var lst = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType), seq);
                return Activator.CreateInstance(targetType, lst);
            }
            else
            {
                Type typeToInstanciate = targetType;
                object l2 = Activator.CreateInstance(typeToInstanciate);

                IList lst = l2 as IList;
                if (lst != null)
                {
                    foreach (object item in seq)
                        lst.Add(item);

                }
                else
                {
                    dynamic lst_dynamic = l2;
                    foreach (dynamic item in seq)
                        lst_dynamic.Add(item);
                }

                return l2;
            }
        }


        AnnotationCollection mem;
        public NumberSequenceAnnotation(AnnotationCollection mem)
        {
            this.mem = mem;
        }
    }

    class MergedValueAnnotation : IObjectValueAnnotation, IOwnedAnnotation
    {
        public IEnumerable<AnnotationCollection> Merged => merged;
        List<AnnotationCollection> merged;
        public MergedValueAnnotation(List<AnnotationCollection> merged)
        {
            this.merged = merged;
        }

        public object Value
        {
            get
            {
                var values = merged.Select(x => (x.Get<IStringValueAnnotation>()?.Value ?? x.Get<IObjectValueAnnotation>().Value)).Distinct().Take(2).ToArray();
                if (values.Length != 1) return null;

                return merged.Select(x => x.Get<IObjectValueAnnotation>().Value).FirstOrDefault();
            }
            set
            {
                foreach (var m in merged)
                {
                    m.Get<IObjectValueAnnotation>().Value = value;
                }
            }
        }

        public void Read(object source)
        {
            foreach (var annotation in merged)
            {
                annotation.Read();
            }
        }

        public void Write(object source)
        {
            foreach(var annotation in merged)
            {
                annotation.Write();
            }
        }
    }

    class ManyToOneAnnotation : IMembersAnnotation, IOwnedAnnotation
    {
        AnnotationCollection[] members;
        public AnnotationCollection[] Members
        {
            get
            {
                if (members != null) return members;
                var c = parentAnnotation.Get<ICollectionAnnotation>();
                var rdOnly = parentAnnotation.Get<ReadOnlyMemberAnnotation>() == null;
                AnnotationCollection[] annotatedElements;
                bool collectAll = rdOnly;
                if (c == null)
                {
                    var members = parentAnnotation.Get<IMembersAnnotation>();
                    var merged = parentAnnotation.Get<MergedValueAnnotation>();
                    annotatedElements = merged.Merged.ToArray();
                }
                else
                {
                    annotatedElements = c.AnnotatedElements.ToArray();
                }

                if (annotatedElements == null) return Array.Empty<AnnotationCollection>();
                var sources = annotatedElements;
                var mems = sources.Select(x => x.Get<IMembersAnnotation>()?.Members.ToArray() ?? Array.Empty<AnnotationCollection>()).ToArray();
                if (mems.Length == 0) return Array.Empty<AnnotationCollection>();
                var fst = mems[0];
                var dicts = mems.Select(x => x.ToDictionary(d => {
                    // the way we separate members is by display name and type.
                    // these are normally the different things the user want to select between.
                    var mem = d.Get<IMemberAnnotation>()?.Member;
                    return mem.GetDisplayAttribute().GetFullName() + mem.TypeDescriptor.Name;
                    })).ToArray();
                var union = dicts.SelectMany(x => x.Keys).Distinct().ToArray();
                List<AnnotationCollection> CommonAnnotations = new List<AnnotationCollection>();
                
                foreach (var name in union)
                { 
                    List<AnnotationCollection> mergething = new List<AnnotationCollection>();
                    IMemberData mem = null;
                    foreach (var thing2 in dicts)
                    {
                        if (!thing2.ContainsKey(name))
                        {
                            if(collectAll)
                                continue;
                            else
                                goto next_thing;
                        }

                        var otherAnnotation = thing2[name];
                        
                        var othermember = otherAnnotation.Get<IMemberAnnotation>()?.Member;
                        if (mem == null) mem = othermember;
                        //if (othermember != mem) continue;

                        mergething.Add(thing2[name]);
                    }
                    if (mem == null) continue;
                    var newa = parentAnnotation.AnnotateMember(mem, sources[0].ExtraAnnotations.Append(new MergedValueAnnotation(mergething)).ToArray());

                    var manyAccess = new ManyAccessAnnotation(mergething.Select(x => x.Get<IAccessAnnotation>()).ToArray());
                    // Enabled if is not supported when multi-selecting.
                    newa.RemoveType<EnabledIfAnnotation>();
                    
                    newa.Add(manyAccess);

                    newa.Read(parentAnnotation.Get<IObjectValueAnnotation>().Value);
                    CommonAnnotations.Add(newa);

                    next_thing:;
                }
                return (members = CommonAnnotations.ToArray());
                
            }
        }
        
        IEnumerable<AnnotationCollection> IMembersAnnotation.Members => Members;

        public ManyToOneAnnotation(AnnotationCollection parentAnnotation)
        {
            this.parentAnnotation = parentAnnotation;
        }

        AnnotationCollection parentAnnotation;

        public void Read(object source)
        {
            if (members == null) return;
            foreach(var annotation in members)
            {
                annotation.Read();
            }
        }

        public void Write(object source)
        {
            if (members != null)
            {
                foreach(var mem in members)
                    mem.Write(source);
            }
        }
    }

    class ManyToOneAvailableValuesAnnotation : IAvailableValuesAnnotation
    {
        public IEnumerable AvailableValues
        {
            get
            {
                var sets = others.Select(x => x.AvailableValues.Cast<object>().ToHashSet()).ToArray();
                HashSet<object> r = sets.FirstOrDefault();
                foreach (var set in sets.Skip(1))
                {
                    r.ExceptWith(set);
                }
                return (IEnumerable)r ?? Array.Empty<object>();
            }
        }

        IAvailableValuesAnnotation[] others;

        public ManyToOneAvailableValuesAnnotation(IAvailableValuesAnnotation[] others)
        {
            this.others = others;
        }
    }

    class ManyAccessAnnotation : IAccessAnnotation
    {
        public bool IsReadOnly => others.Any(x => x.IsReadOnly);
        public bool IsVisible => others.Any(x => x.IsVisible);

        IAccessAnnotation[] others;
        public ManyAccessAnnotation(IAccessAnnotation[] others)
        {
            this.others = others;
        }
    }
    class DefaultValueAnnotation : IObjectValueAnnotation, IOwnedAnnotation
    {
        string currentError;
        AnnotationCollection annotation;
        object currentValue;
        public object Value
        {
            get => currentValue;
            set => currentValue = value;
        }

        public DefaultValueAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }
        
        public void Read(object source)
        {
            var m = annotation.Get<IMemberAnnotation>();
            currentValue = m.Member.GetValue(source);
        }

        public void Write(object source)
        {
            var m = annotation.Get<IMemberAnnotation>();
            if (m.Member.Writable == false) return;
            string newerror = null;
            try
            {
                if(object.Equals(currentValue, m.Member.GetValue(source)) == false)
                    m.Member.SetValue(source, currentValue);
            }
            catch (Exception _e)
            {
                newerror = _e.GetInnerMostExceptionMessage();
            }
            finally
            {
                var err = annotation.Get<ErrorAnnotation>();
                if (err != null)
                {
                    if (currentError != null)
                    {
                        err.Errors.Remove(currentError);
                    }
                    currentError = newerror;
                    if (currentError != null)
                        err.Errors.Add(currentError);
                }
            }
        }
    }
    class ObjectValueAnnotation : IObjectValueAnnotation, IReflectionAnnotation
    {
        public object Value { get; set; }

        public ITypeData ReflectionInfo { get; private set; }

        public ObjectValueAnnotation(object value, ITypeData reflect)
        {
            this.Value = value;
            this.ReflectionInfo = reflect;
        }
    }
    class AvailableMemberAnnotation : IAnnotation
    {
        public AnnotationCollection AvailableMember;
        public AvailableMemberAnnotation(AnnotationCollection annotation)
        {
            this.AvailableMember = annotation;
        }
    }
    
    /// <summary> The default data annotator plugin. This normally forms the basis for annotation. </summary>
    public class DefaultDataAnnotator : IAnnotator
    {
        double IAnnotator.Priority => 1;

        //[AnnotationAggregator(typeof(IMemberAnnotation))]
        class AvailableValuesAnnotation : IAvailableValuesAnnotation
        {
            string availableValuesMember;

            public IEnumerable AvailableValues
            {
                get
                {
                    var mem = annotation.ParentAnnotation.Get<IMembersAnnotation>()?.Members;
                    var mem2 = mem.FirstOrDefault(x => x.Get<IMemberAnnotation>()?.Member.Name == availableValuesMember);

                    return mem2?.Get<IObjectValueAnnotation>().Value as IEnumerable ?? Enumerable.Empty<object>();
                }
            }

            AnnotationCollection annotation;
            public AvailableValuesAnnotation(AnnotationCollection annotation, string available)
            {
                availableValuesMember = available;
                this.annotation = annotation;
            }

            public void Annotate(AnnotationCollection anntotation)
            {
                //annotation.
            }
        }
        
        class InputStepAnnotation : IAvailableValuesAnnotation, IObjectValueAnnotation
        {
            struct InputThing
            {
                public ITestStep Step { get; set; }
                public IMemberData Member { get; set; }
                public override string ToString()
                {
                    if(Step == null) return "None";
                    return $"{Member.GetDisplayAttribute().Name} from {Step.GetFormattedName()}";
                }

                public static InputThing FromInput(IInput inp)
                {
                    return new InputThing { Step = inp.Step, Member = inp.Property };
                }

                public override bool Equals(object obj)
                {
                    if(obj is InputThing other)
                    {
                        return other.Step == Step && other.Member == Member;
                    }
                    return false;
                }

                public override int GetHashCode()
                {
                    return (Step?.GetHashCode() ?? 1) ^ (Member?.GetHashCode() ?? 2);
                }

            }
            public IEnumerable AvailableValues
            {
                get
                {
                    var inp = getInput();
                    if (inp == null)
                        return Enumerable.Empty<object>();
                    AnnotationCollection parent = annotation;
                    while (parent.ParentAnnotation != null)
                        parent = parent.ParentAnnotation;

                    object context = parent.Get<IObjectValueAnnotation>().Value;
                    ITestStepParent step = context as ITestStep;
                    if(context is IEnumerable enumerable_context)
                    {
                        step = enumerable_context.OfType<ITestStep>().FirstOrDefault();
                    }
                    if (step == null) return Enumerable.Empty<object>();

                    HashSet<ITestStepParent> parents = new HashSet<ITestStepParent>();
                    while (step.Parent != null)
                    {
                        parents.Add(step);
                        step = step.Parent;
                    }
                    
                    var steps = Utils.FlattenHeirarchy(step.ChildTestSteps, x => x.ChildTestSteps);
                    
                    List<InputThing> accepted = new List<InputThing>();
                    accepted.Add(new InputThing() { });
                    if(inp is IInputTypeRestriction res)
                    {
                        foreach(var s in steps)
                        {
                            if (parents.Contains(s)) continue;
                            var t = TypeData.GetTypeData(s);
                            foreach(var mem in t.GetMembers())
                            {
                                if (mem.HasAttribute<OutputAttribute>())
                                {
                                    if (res.SupportsType(mem.TypeDescriptor))
                                    {
                                        accepted.Add(new InputThing { Step = s, Member = mem });
                                    }
                                }
                            }
                        }
                    }

                    return accepted;
                }
            }

            IInput getInput() => annotation.GetAll<IObjectValueAnnotation>().FirstOrDefault(x => x != this && x.Value is IInput)?.Value as IInput;

            public object Value
            {
                get
                {
                    var current = getInput();
                    if (current == null) return null;
                    return InputThing.FromInput(current);
                }

                set {
                    var inp = getInput();
                    inp.Step = ((InputThing)value).Step;
                    inp.Property = ((InputThing)value).Member;
                }
            }

            AnnotationCollection annotation;
            public InputStepAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }
        }

        class EnumValuesAnnotation : IAvailableValuesAnnotation
        {
            public IEnumerable AvailableValues => Enum.GetValues(enumType);

            Type enumType;
            AnnotationCollection a;
            public EnumValuesAnnotation(Type enumType, AnnotationCollection a)
            {
                this.enumType = enumType;
                this.a = a;
            }
        }

        class EnumStringAnnotation : IStringValueAnnotation, IAccessAnnotation
        {

            string enumToString(Enum value)
            {
                if (value == null) return null;
                var mem = enumType.GetMember(value.ToString()).FirstOrDefault();
                
                if (mem == null)
                {
                    if (enumType.HasAttribute<FlagsAttribute>())
                    {
                        var flags = Enum.GetValues(enumType);
                        StringBuilder sb = new StringBuilder();

                        bool first = true;
                        foreach (Enum flag in flags)
                        {
                            if (value.HasFlag(flag))
                            {
                                if (!first)
                                    sb.Append(" | ");
                                else
                                    first = false;
                                sb.Append(enumToString(flag));
                            }
                        }
                        return sb.ToString();
                    }
                    return value.ToString(); 
                }
                return mem.GetDisplayAttribute().Name;
            }

            Enum evalue
            {
                get => a.Get<IObjectValueAnnotation>()?.Value as Enum;
                set => a.Get<IObjectValueAnnotation>().Value = value;
            }

            public string Value
            {
                get => enumToString(evalue);
                set {
                    var values = Enum.GetValues(enumType).Cast<Enum>();

                    var newvalue = values.FirstOrDefault(x => enumToString(x) == value);
                    if(newvalue == null)
                    {
                        newvalue = values.FirstOrDefault(x => enumToString(x).ToLower() == value.ToLower());
                    }
                    if (newvalue != null)
                        evalue = newvalue;
                    else
                        throw new FormatException($"Unable to parse {value} as an {enumType}");
                }
            }

            public bool IsReadOnly => false;

            public bool IsVisible
            {
                get
                {
                    if (evalue != null)
                    {
                        var mem = enumType.GetMember(evalue.ToString()).FirstOrDefault();
                        if(mem != null)
                        {
                            return mem.IsBrowsable();
                        }
                    }
                    return true;
                }
            }

            AnnotationCollection a;
            Type enumType;
            public EnumStringAnnotation(Type enumType, AnnotationCollection annotation)
            {
                this.a = annotation;
                this.enumType = enumType;
            }
        }

        class MultiSelectable
        {
            public string FriendlyName { get; set; }
            public object Value { get; set; }

            public override string ToString()
            {
                return FriendlyName;
            }
            public override bool Equals(object obj)
            {
                if (obj is MultiSelectable ms)
                {
                    return object.Equals(ms.Value, Value);
                }
                return base.Equals(obj);
            }
            public override int GetHashCode()
            {
                return Value.GetHashCode() ^ typeof(MultiSelectable).GetHashCode();
            }
        }

        class FlagEnumAnnotation : IMultiSelect
        {
            public IEnumerable Selected
            {
                get
                {
                    List<Enum> items = new List<Enum>();
                    if (this.val.Value is Enum value)
                    {
                        foreach (var enumValue in Enum.GetValues(enumType))
                        {
                            var ev = (Enum)enumValue;
                            if (value.HasFlag(ev))
                                items.Add(ev);
                        }
                    }
                    return items;
                }
                set
                {
                    int sum = 0;
                    var items = value.Cast<Enum>();

                    foreach (var item in items)
                    {
                        sum += Convert.ToInt32(item);
                    }
                    val.Value = Enum.ToObject(enumType, sum);
                }
            }

            IObjectValueAnnotation val;
            Type enumType;

            public FlagEnumAnnotation(IObjectValueAnnotation val, Type enumType)
            {
                this.val = val;
                this.enumType = enumType;
            }
        }

        class StringValueAnnotation : IStringValueAnnotation
        {
            public string Value
            {
                get => (string)annotation.Get<IObjectValueAnnotation>().Value;
                set => annotation.Get<IObjectValueAnnotation>().Value = value;
            }

            AnnotationCollection annotation;
            public StringValueAnnotation(AnnotationCollection dataAnnotation)
            {
                annotation = dataAnnotation;
            }
        }

        class MacroStringValueAnnotation : IStringValueAnnotation, IValueDescriptionAnnotation, IStringExampleValueAnnotation
        {
            public string Value
            {
                get => ((MacroString)annotation.Get<IObjectValueAnnotation>().Value)?.Text;
                set
                {
                    var mcs = annotation.Get<IObjectValueAnnotation>().Value as MacroString;
                    if (mcs == null)
                    {
                        mcs = new MacroString();
                        annotation.Get<IObjectValueAnnotation>().Value = mcs;
                    }
                    mcs.Text = value;
                }
            }

            AnnotationCollection annotation;
            public MacroStringValueAnnotation(AnnotationCollection dataAnnotation)
            {
                annotation = dataAnnotation;
            }

            public string Describe()
            {
                var mcs = annotation.Get<IObjectValueAnnotation>().Value as MacroString;
                return mcs?.Expand();
            }

            public string Example => Describe();
        }

        class ColumnAccessAnnotation : IAccessAnnotation
        {
            public bool IsReadOnly => true;

            public bool IsVisible => true;
        }

        class StepNameStringValue : IStringReadOnlyValueAnnotation
        {
            AnnotationCollection annotation;
            public StepNameStringValue(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }
            public string Value
            {
                get => ((ITestStep)annotation.ParentAnnotation.Get<IObjectValueAnnotation>().Value).GetFormattedName();
            }
        }

        class MethodAnnotation : IMethodAnnotation, IOwnedAnnotation
        {
            public void Invoke()
            {
                if (source == null)
                    throw new InvalidOperationException("Unable to invoke method");
                var action_proto = member.GetValue(source);
                var action = action_proto as Action;
                if (action != null)
                    action();
            }

            public MethodAnnotation(IMemberData member)
            {
                this.member = member;
            }

            IMemberData member;

            object source;

            public void Read(object source)
            {
                this.source = source;
            }

            public void Write(object source)
            {

            }
        }

        class GenericSequenceAnnotation : ICollectionAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation
        {
            public IEnumerable Elements => fac.Get<IObjectValueAnnotation>().Value as IEnumerable;

            IEnumerable<AnnotationCollection> annotatedElements = null;

            public IEnumerable<AnnotationCollection> AnnotatedElements
            {
                get
                {
                    if (annotatedElements == null && Elements != null)
                    {
                        List<AnnotationCollection> annotations = new List<AnnotationCollection>();
                        foreach (var elem in Elements)
                        {
                            var elem2 = TypeData.GetTypeData(elem);
                            annotations.Add(fac.AnnotateSub(elem2, elem));
                        }

                        annotatedElements = annotations;
                    }

                    return annotatedElements ?? Array.Empty<AnnotationCollection>();

                }
                set
                {
                    annotatedElements = value;
                }
            }

            public string Value => string.Format("Count: {0}", Elements.Cast<object>().Count());

            AnnotationCollection fac;
            public GenericSequenceAnnotation(AnnotationCollection fac)
            {
                this.fac = fac;
            }

            public void Read(object source)
            {
                annotatedElements = null;
            }

            bool isWriting = false;
            public void Write(object source)
            {
                if (isWriting) return;
                if (annotatedElements == null) return;
                bool rdonly = fac.Get<ReadOnlyMemberAnnotation>() != null;
                var objValue = fac.Get<IObjectValueAnnotation>();
                var lst = objValue.Value;
                if (lst is IList lst2)
                {
                    if (!rdonly)
                        lst2.Clear();
                    foreach (var elem in annotatedElements)
                    {
                        var val = elem.Get<IObjectValueAnnotation>().Value;

                        elem.Write(val);
                        if (!rdonly)
                            lst2.Add(val);
                    }
                }
                isWriting = true;
                try
                {
                    fac.Write(source);
                }
                finally
                {
                    isWriting = false;
                }
            }

            class ElementInfo : IMemberData
            {
                public ITypeData DeclaringType { get; private set; }

                public ITypeData TypeDescriptor { get; private set; }

                public bool Writable => true;

                public bool Readable => true;

                public IEnumerable<object> Attributes => Array.Empty<object>();

                public string Name => "";

                object value;

                public object GetValue(object owner)
                {
                    return value;
                }

                public void SetValue(object owner, object value)
                {
                    this.value = value;
                }

                public ElementInfo(ITypeData type, ITypeData listType, object value)
                {
                    DeclaringType = listType;
                    TypeDescriptor = type;
                    this.value = value;
                }
            }

            public AnnotationCollection NewElement()
            {
                var reflect = fac.Get<IReflectionAnnotation>();

                if (reflect.ReflectionInfo is TypeData cstype)
                {
                    var elemType = cstype.Type.GetEnumerableElementType();

                    var elem2 = TypeData.FromType(elemType);
                    if (elem2.CanCreateInstance == false)
                    {
                        return fac.AnnotateSub(elem2, null);
                    }
                    else
                    {
                        object instance = null;
                        try
                        {
                            instance = elem2.CreateInstance(Array.Empty<object>());
                        }
                        catch
                        {

                        }
                        return fac.AnnotateSub(elem2, instance);
                    }
                }
                throw new InvalidOperationException();
            }
        }

        class ResourceAnnotation : IAvailableValuesAnnotation, IStringValueAnnotation
        {
            public IEnumerable AvailableValues => ComponentSettingsList.GetContainer(basetype).Cast<object>().Where(x => x.GetType().DescendsTo(basetype));

            public string Value
            {
                get => (a.Get<IObjectValueAnnotation>()?.Value as IResource)?.ToString();
                set => throw new NotSupportedException("Resource annotation cannot convert from a string");
            }

            Type basetype;
            AnnotationCollection a;
            public ResourceAnnotation(AnnotationCollection a, Type lowerstType)
            {
                this.basetype = lowerstType;
                this.a = a;
            }
        }

        class DefaultAccessAnnotation : IAccessAnnotation
        {
            public bool IsReadOnly => rdonly;

            public bool IsVisible => browsable;
            bool rdonly;
            bool browsable;

            public DefaultAccessAnnotation(bool rdonly, bool browsable)
            {
                this.rdonly = rdonly;
                this.browsable = browsable;
            }
        }

        class MemberToStringAnnotation : IStringValueAnnotation
        {
            public string Value
            {
                get
                {
                    var mem = (annotation.Get<IObjectValueAnnotation>()?.Value as IMemberData);
                    return mem?.GetDisplayAttribute().GetFullName();
                }
                set => throw new NotImplementedException();
            }

            AnnotationCollection annotation;
            public MemberToStringAnnotation(AnnotationCollection da)
            {
                annotation = da;
            }
        }

        class MultiResourceSelector : IMultiSelect
        {
            public IEnumerable Selected
            {
                get => annotation.Get<IObjectValueAnnotation>().Value as IEnumerable;
                set
                {
                    var seq = annotation.Get<ICollectionAnnotation>();
                    var anot = seq.AnnotatedElements.ToArray();
                    List<AnnotationCollection> elements = new List<AnnotationCollection>();
                    var values = value.Cast<object>().ToArray();
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (anot.Length < i)
                        {
                            var val = anot[i].Get<IObjectValueAnnotation>();
                            val.Value = values[i];
                            elements.Add(anot[i]);
                        }
                        else
                        {
                            var anot2 = seq.NewElement();
                            anot2.Get<IObjectValueAnnotation>().Value = values[i];
                            elements.Add(anot2);
                        }
                    }
                    seq.AnnotatedElements = elements;
                }
            }

            AnnotationCollection annotation;
            Type baseType;
            public MultiResourceSelector(AnnotationCollection annotation, Type baseType)
            {
                this.baseType = baseType;
                this.annotation = annotation;
            }

            public override string ToString()
            {
                return $"{Selected.Cast<object>().Count()} objects selected";
            }
        }

        class EnabledAnnotation : IMembersAnnotation, IOwnedAnnotation
        {
            class EnabledAccessAnnotation : IAccessAnnotation
            {
                AnnotationCollection parentAnnotation;
                public EnabledAccessAnnotation(AnnotationCollection parentAnnotation)
                {
                    this.parentAnnotation = parentAnnotation;
                }
                public bool IsReadOnly => (parentAnnotation.Get<IObjectValueAnnotation>().Value as IEnabled).IsEnabled == false;

                public bool IsVisible => true;
            }
            
            IEnumerable<AnnotationCollection> members;
            public IEnumerable<AnnotationCollection> Members
            {
                get
                {
                    if (this.members != null) return this.members;
                    var members = annotation.GetAll<IMembersAnnotation>().FirstOrDefault(x => x != this)?.Members;

                    var enabledMember = members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == nameof(Enabled<int>.IsEnabled));

                    var valueMember = members.FirstOrDefault(x => x != enabledMember);

                    var unit = annotation.Get<UnitAttribute>();
                    var avail = annotation.Get<IAvailableValuesAnnotation>();
                    List<IAnnotation> extra = new List<IAnnotation>();
                    if (unit != null) { extra.Add(unit); }
                    if (avail != null) { extra.Add(avail); }
                    extra.Add(new EnabledAccessAnnotation(annotation));
                    var newValueMember = annotation.AnnotateMember(valueMember.Get<IMemberAnnotation>().Member, extra.ToArray());
                    var src = annotation.Get<IObjectValueAnnotation>().Value;
                    if(src != null)
                        newValueMember.Read(src);
                    this.members = new[] { enabledMember, newValueMember };
                    return this.members;
                }
            }

            AnnotationCollection annotation;
            public EnabledAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }

            public void Read(object source)
            {
                if (Members != null)
                {
                    var val = annotation.Get<IObjectValueAnnotation>().Value;
                    if (val == null) return;
                    foreach (var member in Members)
                        member.Read(val);
                }
            }

            public void Write(object source)
            {
                if (Members != null)
                {
                    var val = annotation.Get<IObjectValueAnnotation>().Value;
                    foreach(var member in Members)
                        member.Write(val);
                    {
                        // since the Enabled annotation overrides the other IMembersAnnotation
                        // we need to make sure to update that too.
                        var otherMembers = annotation.Get<IMembersAnnotation>(from: this);
                        foreach (var member in otherMembers.Members)
                            member.Read();
                    }
                }
            }
        }

        class DeviceAddressAnnotation : ISuggestedValuesAnnotation
        {
            public IEnumerable SuggestedValues => getDeviceAddresses();

            public IEnumerable<string> getDeviceAddresses()
            {
                var mem = annotation.Get<IMemberAnnotation>();
                var device_attr = mem.Member.GetAttribute<DeviceAddressAttribute>();
                var plugins = PluginManager.GetPlugins<IDeviceDiscovery>();
                foreach(var plugin in plugins)
                {
                    try
                    {
                        var device_discoverer = (IDeviceDiscovery) Activator.CreateInstance(plugin);
                        if (device_discoverer.CanDetect(device_attr))
                        {
                            return device_discoverer.DetectDeviceAddresses(device_attr);
                        }
                    }
                    catch
                    {

                    }
                }
                return Enumerable.Empty<string>();
            }

            AnnotationCollection annotation;

            public DeviceAddressAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }
        }

        class SuggestedValueAnnotation : ISuggestedValuesAnnotation
        {
            string suggestedValuesMember;
            public IEnumerable SuggestedValues => getSuggestedValues();

            public IEnumerable getSuggestedValues()
            {
                var mem = annotation.ParentAnnotation.Get<IMembersAnnotation>()?.Members;
                var mem2 = mem.FirstOrDefault(x => x.Get<IMemberAnnotation>()?.Member.Name == suggestedValuesMember);

                return mem2?.Get<IObjectValueAnnotation>().Value as IEnumerable ?? Enumerable.Empty<object>();
            }

            AnnotationCollection annotation;

            public SuggestedValueAnnotation(AnnotationCollection annotation, string suggestedValuesMember)
            {
                this.annotation = annotation;
                this.suggestedValuesMember = suggestedValuesMember;
            }
        }

        class PortAnnotation : IAvailableValuesAnnotation, IStringReadOnlyValueAnnotation
        {
            public IEnumerable AvailableValues
            {
                get
                {
                    IEnumerable<Port> getPorts(IResource res) => res.GetConstProperties<Port>();
                    
                    var dutPorts = DutSettings.Current.SelectMany(getPorts);
                    var instrumentPorts = InstrumentSettings.Current.SelectMany(getPorts);
                    var availablePorts = dutPorts.Concat(instrumentPorts);
                    return availablePorts;
                }
            }

            public string Value
            {
                get
                {
                    var port = annotation.Get<IObjectValueAnnotation>().Value as Port;
                    if (port == null) return "";
                    return port.ToString();
                }
            }

            AnnotationCollection annotation;
            public PortAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }
        }

        class ViaPointAnnotation : IAvailableValuesAnnotation, IMultiSelect, IStringReadOnlyValueAnnotation
        {
            AnnotationCollection annotation;
            public ViaPointAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }

            public IEnumerable AvailableValues
            {
                get
                {
                    List<ViaPoint> all = InstrumentSettings.Current.SelectMany(instr => instr.GetConstProperties<ViaPoint>()).ToList();
                    return all;
                }
            }

            public IEnumerable Selected
            {
                get
                {
                    return (IEnumerable)annotation.Get<IObjectValueAnnotation>(from: this).Value;
                }
                set
                {
                    var lst = (IList)annotation.Get<IObjectValueAnnotation>(from: this).Value;
                    lst.Clear();
                    foreach (var val in value)
                        lst.Add(val);
                }
            }

            public string Value => string.Format("Via {0} Points", Selected?.Cast<object>().Count() ?? 0);
        }

        class ToStringAnnotation : IStringReadOnlyValueAnnotation
        {
            public string Value => annotations.Get<IObjectValueAnnotation>().Value?.ToString();

            AnnotationCollection annotations;
            public ToStringAnnotation(AnnotationCollection annotations)
            {
                this.annotations = annotations;
            }
        }

        class TestStepSelectAnnotation : IAvailableValuesAnnotation, IStringValueAnnotation
        {
            public IEnumerable AvailableValues
            {
                get
                {
                    var sibling = annotation.Get<IMemberAnnotation>()?.Member.GetAttribute<StepSelectorAttribute>();
                    if (sibling == null) sibling = new StepSelectorAttribute(StepSelectorAttribute.FilterTypes.All);
                    var step = annotation.ParentAnnotation.Get<IObjectValueAnnotation>().Value as ITestStep;
                    return getSteps(step, sibling.Filter);
                }
            }

            public string Value {
                get {
                    var step = annotation.Get<IObjectValueAnnotation>().Value;
                    if (step is ITestStep _step) return _step.GetFormattedName();
                    return (step ?? "").ToString();
                }
                set => throw new NotSupportedException();
            }

            IEnumerable<ITestStep> getSteps(ITestStep step, StepSelectorAttribute.FilterTypes filter)
            {
                switch (filter)
                {
                    case StepSelectorAttribute.FilterTypes.All:
                        return Utils.FlattenHeirarchy(step.GetParent<TestPlan>().ChildTestSteps, x => x.ChildTestSteps);
                    case StepSelectorAttribute.FilterTypes.AllExcludingSelf:
                        return getSteps(step, StepSelectorAttribute.FilterTypes.All).Where(x => x.Id != step.Id);
                    case StepSelectorAttribute.FilterTypes.Children:
                        return step.ChildTestSteps;
                    case StepSelectorAttribute.FilterTypes.Sibling:
                        return step.Parent.ChildTestSteps.Where(x => x.Id != step.Id);
                    default:
                        throw new InvalidOperationException("Invalid filter type: " + filter);
                }
            }

            AnnotationCollection annotation;
            public TestStepSelectAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }
        }

        class PluginTypeSelectAnnotation : IAvailableValuesAnnotation
        {
            IEnumerable<object> selection;
            public IEnumerable AvailableValues {
                get
                {
                    var currentValue = annotation.Get<IObjectValueAnnotation>()?.Value ?? null;
                    if (selection != null) return selection;
                    var member = annotation.Get<IMemberAnnotation>()?.Member;
                    var attrib = member.GetAttribute<PluginTypeSelectorAttribute>();
                    if (attrib == null)
                    {
                        selection = Enumerable.Empty<object>();
                        return selection;
                    }
                    if(member.TypeDescriptor is TypeData cst)
                    {
                        var currentType = TypeData.GetTypeData(currentValue);
                        List<object> selection = new List<object>();
                        foreach(var type in PluginManager.GetPlugins(cst.Type))
                        {
                            try
                            {
                                var cstt = TypeData.FromType(type);
                                if (cstt == currentType)
                                {
                                    selection.Add(currentValue);
                                }
                                else
                                {
                                    var obj = Activator.CreateInstance(type);
                                    selection.Add(obj);
                                }
                            }
                            catch
                            {

                            }
                        }
                        this.selection = selection;
                    }
                    return selection ?? Enumerable.Empty<object>();
                }
            }
            
            AnnotationCollection annotation;
            public PluginTypeSelectAnnotation(AnnotationCollection annotation) => this.annotation = annotation;
            
        }

        class MetaDataPromptAnnotation : IForwardedAnnotations
        {
            AnnotationCollection annotation;
            public MetaDataPromptAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }

            public IEnumerable<AnnotationCollection> Forwarded
            {
                get
                {
                    List<AnnotationCollection> metadataAnnotations = new List<AnnotationCollection>();
                    MetadataPromptObject obj = (MetadataPromptObject) annotation.Get<IObjectValueAnnotation>().Value;
                    var named = annotation.Get<INamedMembersAnnotation>();
                    if (named == null) return Enumerable.Empty<AnnotationCollection>();
                    var member = named.GetMember(TypeData.GetTypeData(obj).GetMember(nameof(MetadataPromptObject.Resources)));
                    var col = member.Get<ICollectionAnnotation>();
                    foreach (var annotatedResource in col.AnnotatedElements) {
                        object resource = annotatedResource.Get<IObjectValueAnnotation>().Value;
                        var named2 = annotatedResource.Get<INamedMembersAnnotation>();
                        var type = TypeData.GetTypeData(resource);
                        var rname = annotatedResource.Get<IStringReadOnlyValueAnnotation>()?.Value ?? resource.ToString();
                        foreach(var member2 in type.GetMembers())
                        {
                            if(member2.GetAttribute<MetaDataAttribute>() is MetaDataAttribute attr && attr.PromptUser)
                            {
                                var namedmember = named2.GetMember(member2);
                                if (namedmember == null) continue;
                                var disp = namedmember.Get<DisplayAttribute>();
                                var disp2 = new DisplayAttribute(disp.Name, disp.Description, Groups: new[] { rname }.Append(disp.Group).ToArray());
                                namedmember.Add(disp2);
                                metadataAnnotations.Add(namedmember);
                            }
                        }
                    }
                    return metadataAnnotations;
                }
            }
        }
        void IAnnotator.Annotate(AnnotationCollection annotation)
        {
            var reflect = annotation.Get<IReflectionAnnotation>();
            if (reflect != null)
                annotation.Add(reflect.ReflectionInfo.GetDisplayAttribute());
            if (reflect != null)
            {

                var help = reflect.ReflectionInfo.GetHelpLink();
                if (help != null)
                    annotation.Add(help);
                if (reflect.ReflectionInfo is TypeData ct)
                {
                    if (ct.Type.DescendsTo(typeof(IMemberData)))
                    {
                        annotation.Add(new MemberToStringAnnotation(annotation));
                    }
                    if (reflect.ReflectionInfo.DescendsTo(typeof(Port)))
                    {
                        annotation.Add(new PortAnnotation(annotation));
                    }
                    if (reflect.ReflectionInfo.DescendsTo(typeof(ViaPoint)))
                    {
                        annotation.Add(new ToStringAnnotation(annotation));
                    }
                }
            }

            var mem = annotation.Get<IMemberAnnotation>();

            annotation.Add(new ErrorAnnotation());
            if (mem != null)
            { 
                if (annotation.Get<IObjectValueAnnotation>() == null)
                    annotation.Add(new DefaultValueAnnotation(annotation));

                annotation.Add(mem.Member.GetDisplayAttribute());
                
                if (mem.Member.GetAttribute<AvailableValuesAttribute>() is AvailableValuesAttribute avail)
                    annotation.Add(new AvailableValuesAnnotation(annotation, avail.PropertyName));

                if(mem.Member.GetAttribute<SuggestedValuesAttribute>() is SuggestedValuesAttribute suggested)
                    annotation.Add(new SuggestedValueAnnotation(annotation, suggested.PropertyName));

                if (mem.Member.GetAttribute<DeviceAddressAttribute>() is DeviceAddressAttribute dev_addr)
                    annotation.Add(new DeviceAddressAnnotation(annotation));

                if (mem.Member.GetAttribute<PluginTypeSelectorAttribute>() is PluginTypeSelectorAttribute typeSelector)
                    annotation.Add(new PluginTypeSelectAnnotation(annotation));

                var browsable = mem.Member.GetAttribute<BrowsableAttribute>();
                annotation.Add(new DefaultAccessAnnotation(mem.Member.Writable == false, browsable?.Browsable ?? true));

                if (mem.Member.TypeDescriptor.DescendsTo(typeof(Action<object>)) || mem.Member.TypeDescriptor.DescendsTo(typeof(Action)))
                   annotation.Add(new MethodAnnotation(mem.Member));
                
                
                if (mem.ReflectionInfo.DescendsTo(typeof(TimeSpan)))
                    annotation.Add(new TimeSpanAnnotation(annotation));

                if (mem.Member.GetAttribute<UnitAttribute>() is UnitAttribute unit)
                    annotation.Add(unit);
                if (mem.Member.GetAttribute<HelpLinkAttribute>() is HelpLinkAttribute help)
                    annotation.Add(help);
                if (mem.Member.GetAttribute<ColumnDisplayNameAttribute>() is ColumnDisplayNameAttribute col)
                    annotation.Add(col);
                if(mem.Member.GetAttribute<FilePathAttribute>() is FilePathAttribute fp)
                    annotation.Add(fp);
                if (mem.Member.GetAttribute<DirectoryPathAttribute>() is DirectoryPathAttribute dp)
                    annotation.Add(dp);
            }

            var rdonly = annotation.Get<ReadOnlyMemberAnnotation>();

            var availMem = annotation.Get<AvailableMemberAnnotation>();
            if (availMem != null)
            {
                var da = availMem.AvailableMember.Get<UnitAttribute>();
                if (da != null)
                    annotation.Add(da);
            }

            if (mem != null)
            {
                annotation.Add(new ValidationErrorAnnotation(mem));

                if (mem.Member.HasAttribute<EnabledIfAttribute>())
                {
                    annotation.Add(new EnabledIfAnnotation(mem));
                }
                if (mem.Member.Writable == false)
                {
                    var currentaccess = annotation.Get<IAccessAnnotation>();
                    annotation.Add(new ReadOnlyMemberAnnotation());
                }
            }

            if (reflect?.ReflectionInfo is TypeData csharpType)
            {
                var type = csharpType.Load();
                if (type.IsNumeric())
                {
                    annotation.Add(new NumberAnnotation(annotation));
                }
                if (type == typeof(string))
                    annotation.Add(new StringValueAnnotation(annotation));
                if (type == typeof(MacroString))
                {
                    annotation.Add(new MacroStringValueAnnotation(annotation));
                }
                if(type == typeof(MetadataPromptObject))
                {
                    annotation.Add(new MetaDataPromptAnnotation(annotation));
                }

                if (type.DescendsTo(typeof(IEnumerable<>)) && type != typeof(String))
                {
                    var innerType = type.GetEnumerableElementType();
                    if (innerType.IsNumeric())
                    {
                        annotation.Add(new NumberSequenceAnnotation(annotation));
                    }
                    else
                    {
                        annotation.Add(new GenericSequenceAnnotation(annotation));
                        if (innerType.DescendsTo(typeof(IResource)))
                        {
                            annotation.Add(new ResourceAnnotation(annotation, innerType));
                            annotation.Add(new MultiResourceSelector(annotation, innerType));
                        }
                        else if (innerType.DescendsTo(typeof(ViaPoint)))
                            annotation.Add(new ViaPointAnnotation(annotation));
                        
                    }
                }
                if (type.IsEnum)
                {
                    annotation.Add(new EnumValuesAnnotation(type, annotation));
                    annotation.Add(new EnumStringAnnotation(type, annotation));

                    if (type.HasAttribute<FlagsAttribute>())
                    {
                        annotation.Add(new FlagEnumAnnotation(annotation.Get<IObjectValueAnnotation>(), type));
                    }
                }
                if (type.DescendsTo(typeof(IResource)))
                    annotation.Add(new ResourceAnnotation(annotation, type));
                else if (type.DescendsTo(typeof(ITestStep)))
                    annotation.Add(new TestStepSelectAnnotation(annotation));
            }
            


            if (mem?.Member is MemberData mem2 && mem2.DeclaringType.DescendsTo(typeof(ITestStep)))
            {
                /*
                var plan = step.GetParent<TestPlan>();

                var externalParameter = plan?.ExternalParameters.Find(step, mem2.Member);
                if (externalParameter != null)
                {
                    resolver.Annotate(new ExternalParameterAnnotation() { ExternalName = externalParameter.Name });
                }*/

                if (mem2.Name == nameof(ITestStep.Name))
                {
                    annotation.Add(new StepNameStringValue(annotation));
                }
                if (mem.Member.TypeDescriptor.DescendsTo(typeof(IInput)))
                {
                    annotation.Add(new InputStepAnnotation(annotation));
                }
            }

            if (reflect.ReflectionInfo is ITypeData tp)
            {
                bool csharpPrimitive = tp is TypeData cst && (cst.Type.IsPrimitive || cst.Type == typeof(string));
                if (tp.GetMembers().Any() && !csharpPrimitive)
                {
                    annotation.Add(new MembersAnnotation(annotation));
                    if (tp.DescendsTo(typeof(Enabled<>)))
                    {
                        annotation.Add(new EnabledAnnotation(annotation));
                    }
                }
            }

            {
                var attr = annotation.Get<ColumnDisplayNameAttribute>();
                if (attr != null && attr.IsReadOnly)
                {
                    annotation.Add(new ColumnAccessAnnotation());
                }
            }
        }
    }
    /// <summary> Proxy annotation for wrapping simpler annotation types. For example IAvailableValuesAnnotation is wrapped in a IAvailableValuesAnnotationProxy.</summary>
    public class ProxyAnnotation : IAnnotator
    {
        class AvailableValuesAnnotationProxy : IAvailableValuesAnnotationProxy, IOwnedAnnotation
        {
            IEnumerable<AnnotationCollection> annotations = null;
            IEnumerable prevValues = Enumerable.Empty<object>();
            public IEnumerable<AnnotationCollection> AvailableValues
            {
                get
                {
                    if (annotations == null)
                    {
                        var values = a.Get<IAvailableValuesAnnotation>()?.AvailableValues;
                        if (values != null)
                            prevValues = values.Cast<object>().ToArray();
                        else
                            prevValues = Array.Empty<object>();
                        
                        var readOnly = new ReadOnlyMemberAnnotation();
                        var lst = new List<AnnotationCollection>();
                        foreach (var obj in prevValues)
                        {
                            if (obj is AnnotationCollection da)
                            {
                                lst.Add(da);
                            }
                            else
                            {
                                var da2 = a.AnnotateSub(TypeData.GetTypeData(obj), obj, readOnly, new AvailableMemberAnnotation(a));
                                lst.Add(da2);
                            }
                        }
                        lst.RemoveIf<AnnotationCollection>(x => x.Get<IAccessAnnotation>()?.IsVisible == false);
                        annotations = lst;

                    }
                    return annotations;
                }
            }
            public AnnotationCollection SelectedValue
            {
                get
                {
                    var current = a.Get<IObjectValueAnnotation>()?.Value;
                    if (current == null) return null;
                    foreach (var a in AvailableValues)
                    {
                        if (object.Equals(current, a.Get<IObjectValueAnnotation>()?.Value))
                        {
                            return a;
                        }
                    }
                    var val = a.Get<IObjectValueAnnotation>().Value;
                    return a.AnnotateSub(TypeData.GetTypeData(val), val, new ReadOnlyMemberAnnotation(), new AvailableMemberAnnotation(a));
                }
                set
                {
                    if (value == null) return;
                    a.Get<IObjectValueAnnotation>().Value = value.Get<IObjectValueAnnotation>().Value;
                }
            }

            AnnotationCollection a;
            bool listTarget = false;
            public AvailableValuesAnnotationProxy(AnnotationCollection a)
            {
                this.a = a;
                listTarget = a.Get<ICollectionAnnotation>() != null;
            }

            public void Read(object source)
            {
                if (annotations == null) return;
                var values = a.Get<IAvailableValuesAnnotation>()?.AvailableValues;
                if(Enumerable.SequenceEqual(prevValues.Cast<object>(), values.Cast<object>()) == false)
                    annotations = null;
            }

            public void Write(object source)
            {

            }
        }

        // Note: This class is more or less a clone of the AvailableValuesAnnotationProxy, but it cannot exactly be reused.
        class SuggestedValuesAnnotationProxy : ISuggestedValuesAnnotationProxy, IOwnedAnnotation
        {
            IEnumerable<AnnotationCollection> annotations = null;
            IEnumerable prevValues = Enumerable.Empty<object>();
            public IEnumerable<AnnotationCollection> SuggestedValues
            {
                get
                {
                    if (annotations == null)
                    {
                        var values = a.Get<ISuggestedValuesAnnotation>()?.SuggestedValues;
                        prevValues = values ?? Enumerable.Empty<object>();

                        var readOnly = new ReadOnlyMemberAnnotation();
                        var lst = new List<AnnotationCollection>();
                        foreach (var obj in prevValues)
                        {
                            if (obj is AnnotationCollection da)
                            {
                                lst.Add(da);
                            }
                            else
                            {
                                var da2 = a.AnnotateSub(TypeData.GetTypeData(obj), obj, readOnly, new AvailableMemberAnnotation(a));
                                lst.Add(da2);
                            }
                        }
                        annotations = lst;

                    }
                    return annotations;
                }
            }
            public AnnotationCollection SelectedValue
            {
                get
                {
                    var current = a.Get<IObjectValueAnnotation>()?.Value;
                    if (current == null) return null;
                    foreach (var a in SuggestedValues)
                    {
                        if (object.Equals(current, a.Get<IObjectValueAnnotation>()?.Value))
                        {
                            return a;
                        }
                    }
                    var val = a.Get<IObjectValueAnnotation>().Value;
                    return a.AnnotateSub(TypeData.GetTypeData(val), val, new ReadOnlyMemberAnnotation(), new AvailableMemberAnnotation(a));
                }
                set
                {
                    if (value == null) return;
                    a.Get<IObjectValueAnnotation>().Value = value.Get<IObjectValueAnnotation>().Value;
                }
            }

            AnnotationCollection a;
            public SuggestedValuesAnnotationProxy(AnnotationCollection a)
            {
                this.a = a;
            }

            public void Read(object source)
            {
                if (annotations == null) return;
                var values = a.Get<ISuggestedValuesAnnotation>()?.SuggestedValues;
                if (Enumerable.SequenceEqual(prevValues.Cast<object>(), values.Cast<object>()) == false)
                    annotations = null;
            }

            public void Write(object source)
            {

            }
        }
        class MultiSelectProxy : IMultiSelectAnnotationProxy
        {
            IEnumerable<AnnotationCollection> annotations
            {
                get
                {
                    return annotation.Get<IAvailableValuesAnnotationProxy>().AvailableValues;
                }
            }

            IEnumerable selection
            {
                get
                {
                    return annotation.Get<IMultiSelect>().Selected as IEnumerable;
                }
                set
                {
                    annotation.Get<IMultiSelect>().Selected = value;
                }
            }

            public IEnumerable<AnnotationCollection> SelectedValues
            {
                get
                {
                    var select = (selection ?? Array.Empty<object>()).Cast<object>().ToHashSet();
                    foreach (var a in annotations)
                    {
                        var val = a.Get<IObjectValueAnnotation>().Value;
                        if (select.Contains(val))
                            yield return a;
                    }
                }
                set
                {
                    selection = value.Select(x => x.Get<IObjectValueAnnotation>().Value).ToArray();
                }
            }

            AnnotationCollection annotation;

            public MultiSelectProxy(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }
        }

        class AccessProxy : IAccessAnnotation, IOwnedAnnotation
        {
            AnnotationCollection annotations;
            public AccessProxy(AnnotationCollection annotations)
            {
                this.annotations = annotations;
            }

            public bool IsReadOnly { get; private set; }

            public bool IsVisible { get; private set; } = true;

            public void Read(object source)
            {
                IsReadOnly = false;
                IsVisible = true;
                foreach (var access in annotations.GetAll<IAccessAnnotation>())
                {
                    IsReadOnly |= access.IsReadOnly;
                    IsVisible &= access.IsVisible;
                }
            }

            public void Write(object source)
            {

            }
        }
        double IAnnotator.Priority => 20;

        void IAnnotator.Annotate(AnnotationCollection annotation)
        {
            var rdonly = annotation.Get<ReadOnlyMemberAnnotation>();
            if (rdonly == null)
            {
                var avail = annotation.Get<IAvailableValuesAnnotation>();
                var multi = annotation.Get<IMultiSelect>();
                if (avail != null)
                {
                    annotation.Add(new AvailableValuesAnnotationProxy(annotation));
                }

                if (multi != null)
                {
                    annotation.Add(new MultiSelectProxy(annotation));
                }

                if(annotation.Get<ISuggestedValuesAnnotation>() != null)
                     annotation.Add(new SuggestedValuesAnnotationProxy(annotation));
            }
            if (annotation.Get<IAccessAnnotation>() != null)
            {
                annotation.Add(new AccessProxy(annotation));
            }
        }
    }

    /// <summary>
    /// Used for wrapping multi selections of objects.
    /// </summary>
    public class MultiObjectAnnotator : IAnnotator
    {
        class MergedAvailableValues : IAvailableValuesAnnotation
        {
            public IEnumerable AvailableValues
            {
                get
                {
                    var merged = annotation.Get<MergedValueAnnotation>();
                    if (merged == null) return Enumerable.Empty<object>();
                    var array = merged.Merged.Select(x => x.Get<IAvailableValuesAnnotation>()).Where(x => x != null).ToArray();
                    
                    Dictionary<object, int> counts = new Dictionary<object, int>();
                    int maxCount = 0;
                    foreach(var annotation in merged.Merged)
                    {
                        maxCount += 1;
                        var values = annotation.Get<IAvailableValuesAnnotation>()?.AvailableValues;
                        if(values != null)
                        {
                            foreach(var value in values)
                            {
                                counts.TryGetValue(value, out int val);
                                counts[value] = val + 1;
                            }
                        }
                    }
                    return counts.Where(x => x.Value == maxCount).Select(x => x.Key).ToArray();
                }
            }
            AnnotationCollection annotation;
            public MergedAvailableValues(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }
        }
        double IAnnotator.Priority => ((IAnnotator)new ProxyAnnotation()).Priority - 1;

        void IAnnotator.Annotate(AnnotationCollection annotation)
        {
            if (annotation.ParentAnnotation == null)
            {
                var collection = annotation.Get<ICollectionAnnotation>();
                if (collection != null)
                {
                    var manyToOne = new ManyToOneAnnotation(annotation);
                    annotation.Add(manyToOne);
                    
                }
            }
            var merged = annotation.Get<MergedValueAnnotation>();
            if (merged == null) return;
            var members = annotation.Get<IMembersAnnotation>();
            
            if(members != null && merged != null)
            {
                var manyToOne = new ManyToOneAnnotation(annotation);
                annotation.Add(manyToOne);
            }
            var manyAnnotation = annotation.Get<ManyToOneAnnotation>();
            if (manyAnnotation == null) return;

            var avail = annotation.Get<IAvailableValuesAnnotation>();
            if (avail == null) return;

            annotation.Add(new MergedAvailableValues(annotation));

            /*var avail = manyAnnotation.Members.Select(a => a?.Get<IAvailableValuesAnnotationProxy>()).Where(x => x != null).ToArray();
            if(avail.Length > 0)
            {
                annotation.Add(new ManyToOneAvailableValuesAnnotation(avail));
            }
            var access = manyAnnotation.Members.Select(a => a.Get<IAccessAnnotation>()).Where(x => x != null).ToArray();
            if(access.Length > 0)
            {
                annotation.Add(new ManyAccessAnnotation(access));
            }*/
        }
    }

    /// <summary>
    /// Used for resolving data annotation. Loops through the various IDataAnnotator implementations.
    /// </summary>
    internal class AnnotationResolver
    {
        List<IAnnotator> annotators;
        /// <summary> The current annotation. </summary>
        public AnnotationCollection Annotations { get; private set; }
        bool stop = false;
        int offset = 0;
        
        /// <summary> </summary>
        public AnnotationResolver()
        {
            var annotatorTypes = PluginManager.GetPlugins<IAnnotator>();
            annotators = annotatorTypes.Select(x => Activator.CreateInstance(x)).OfType<IAnnotator>().ToList();
            annotators.Sort((x, y) => x.Priority.CompareTo(y.Priority));
        }

        /// <summary>
        /// Iterates through the data annotation process.
        /// </summary>
        /// <param name="annotation"></param>
        public void Iterate(AnnotationCollection annotation)
        {
            this.Annotations = annotation;
            while (offset < annotators.Count && !stop)
            {
                var provider = annotators[offset];
                offset++;
                provider.Annotate(this.Annotations);
            }
        }

    }

    /// <summary> A collection of annotations. Used to store high-level information about an object. </summary>
    public class AnnotationCollection : IEnumerable<IAnnotation>
    {
        class MemberAnnotation : IMemberAnnotation, IReflectionAnnotation
        {
            public IMemberData Member { get; private set; }
            public ITypeData ReflectionInfo => Member.TypeDescriptor;

            public MemberAnnotation(IMemberData member)
            {
                this.Member = member;
            }
        }

        /// <summary> Creates a new shallow clone of the object. The Annotations list is clone, but the elements are not. </summary>
        /// <returns></returns>
        public AnnotationCollection Clone()
        {
            return new AnnotationCollection(Annotations)
            {
                ParentAnnotation = ParentAnnotation,
                source = source
            };
        }

        /// <summary>
        /// The annotation that created this annotation.
        /// </summary>
        public AnnotationCollection ParentAnnotation { get; private set; }

        /// <summary> The source object currently used for this annotation. </summary>
        public object Source => source;

        /// <summary>
        /// The list of annotation that the is object represents.
        /// </summary>
        private List<IAnnotation> Annotations = new List<IAnnotation>();
        /// <summary> </summary>
        public AnnotationCollection()
        {

        }

        private AnnotationCollection(IEnumerable<IAnnotation> annotation)
        {
            Annotations = annotation.ToList();
        }

        /// <summary> Adds an annotation. </summary>
        /// <param name="annotation"></param>
        public void Add(IAnnotation annotation)
        {
            Annotations.Add(annotation);
        }
        /// <summary> adds a list of annotations. </summary>
        /// <param name="elements"></param>
        public void Add(params IAnnotation[] elements)
        {
            this.AddRange(elements);
        }

        /// <summary> adds a list of annotations. </summary>
        /// <param name="elements"></param>
        public void AddRange(IEnumerable<IAnnotation> elements)
        {
            Annotations.AddRange(elements);
        }

        /// <summary>
        /// Removes all annotations of a specific type from the collection.
        /// </summary>
        public void RemoveType<T>() where T : IAnnotation
        {
            Annotations.RemoveIf(x => x is T);
        }

        /// <summary>
        /// Removes a specific annotation from the collection.
        /// </summary>
        public void Remove(IAnnotation item)
        {
            Annotations.Remove(item);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        public IEnumerator<IAnnotation> GetEnumerator()
        {
            return Annotations.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Annotations.GetEnumerator();
        }

        /// <summary>
        /// Gets the first annotation of a specific kind. Note this goes by the most-recently added principle. 
        /// </summary>
        /// <typeparam name="T">The kind of annotation to look for.</typeparam>
        /// <param name="recursive">Whether to include parent annotation search.</param>
        /// <param name="from">Where the search should start. </param>
        /// <returns></returns>
        public T Get<T>(bool recursive = false, object from = null) where T : IAnnotation
        {
            int i = 0;
            if (from != null)
            {
                for (; i < Annotations.Count; i++)
                {
                    if (Annotations[Annotations.Count - i - 1] == from)
                    {
                        i++;
                        break;
                    }
                }
            }

            for (; i < Annotations.Count; i++)
            {
                var x = Annotations[Annotations.Count - i - 1];
                if (x is T y) return y;
            }
            if (recursive && ParentAnnotation != null)
                return ParentAnnotation.Get<T>(true);

            return default(T);
        }

        /// <summary> Updates the annotation based on a source object. </summary>
        /// <param name="source"></param>
        public void Read(object source)
        {
            this.source = source;
            foreach (var annotation in Annotations)
            {
                if (annotation is IOwnedAnnotation owned)
                    owned.Read(source);
            }
        }

        /// <summary> Updates the annotation based on that last specified source object. </summary>
        public void Read()
        {
            if(source != null)
                Read(source);
        }

        /// <summary> Writes the annotation data to the last specified source object. </summary>
        public void Write()
        {
            if(source != null)
                Write(source);
        }

        /// <summary> Writes the annotation data to a specific source object. </summary>
        /// <param name="target"></param>
        public void Write(object target)
        {
            this.source = target;
            for (int i = 0; i < Annotations.Count; i++)
            {
                var annotation = Annotations[Annotations.Count - 1 - i];
                if (annotation is IOwnedAnnotation owned)
                    owned.Write(target);
            }
        }

        /// <summary>
        /// Gets all the annotations of a specific kind.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public IEnumerable<T> GetAll<T>(bool recursive = false) where T : IAnnotation
        {
            for (int i = 0; i < Annotations.Count; i++)
            {
                var x = Annotations[Annotations.Count - i - 1];
                if (x is T y) yield return y;
            }
            if (recursive && ParentAnnotation != null)
            {
                foreach (var elem in ParentAnnotation.GetAll<T>())
                {
                    yield return elem;
                }
            }
        }

        /// <summary> Creates a new data annotation. </summary>
        /// <param name="obj"></param>
        /// <param name="member"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public static AnnotationCollection Create(object obj, IReflectionData member, params IAnnotation[] extraAnnotations)
        {
            var annotation = new AnnotationCollection();

            if (member is IMemberData mem)
            {
                annotation.Add(new MemberAnnotation(mem), new DefaultValueAnnotation(annotation));
            }
            annotation.AddRange(extraAnnotations);
            var resolver = new AnnotationResolver();
            resolver.Iterate(annotation);
            if (obj != null)
                annotation.Read(obj);
            return annotation;
        }

        object source;
        /// <summary> Additional annotations added to the current one. </summary>
        public IAnnotation[] ExtraAnnotations = Array.Empty<IAnnotation>();

        /// <summary>
        /// Annotates an object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public static AnnotationCollection Annotate(object obj, params IAnnotation[] extraAnnotations)
        {
            var annotation = new AnnotationCollection { source = obj, ExtraAnnotations = extraAnnotations ?? Array.Empty<IAnnotation>() };
            annotation.AddRange(extraAnnotations);
            annotation.Add(new ObjectValueAnnotation(obj, TypeData.GetTypeData(obj)));
            var resolver = new AnnotationResolver();
            resolver.Iterate(annotation);
            annotation.Read(obj);
            return annotation;
        }
        
        /// <summary> Annotates a member of the object annotated by this. </summary>
        /// <param name="member"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public AnnotationCollection AnnotateMember(IMemberData member, params IAnnotation[] extraAnnotations)
        {
            var annotation = new AnnotationCollection { ParentAnnotation = this, source = source, ExtraAnnotations = extraAnnotations ?? Array.Empty<IAnnotation>() };
            annotation.Add(new MemberAnnotation(member));
            annotation.AddRange(extraAnnotations);
            var resolver = new AnnotationResolver();
            resolver.Iterate(annotation);
            return annotation;
        }

        /// <summary> Annotates a sub-object of the object annotated by this. </summary>
        /// <param name="reflect"></param>
        /// <param name="obj"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public AnnotationCollection AnnotateSub(ITypeData reflect, object obj, params IAnnotation[] extraAnnotations)
        {
            var annotation = new AnnotationCollection { ParentAnnotation = this, ExtraAnnotations = extraAnnotations ?? Array.Empty<IAnnotation>() };

            annotation.AddRange(extraAnnotations);
            annotation.Add(new ObjectValueAnnotation(obj, reflect));

            var resolver = new AnnotationResolver();
            resolver.Iterate(annotation);
            if (obj != null)
                annotation.Read(obj);
            return annotation;
        }

        /// <summary> Creats a string from this. </summary>
        /// <returns></returns>
        public override string ToString() => $"{ParentAnnotation?.ToString() ?? ""}/{Get<DisplayAttribute>()?.Name ?? Get<IObjectValueAnnotation>()?.Value?.ToString() ?? ""}";

        /// <summary> Insert an annotation at a location. </summary>
        /// <param name="index"></param>
        /// <param name="v"></param>
        public void Insert(int index, IAnnotation v)
        {
            Annotations.Insert(index, v);
        }
    }
}
