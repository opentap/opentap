//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Concurrent;
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

    /// <summary> Specifies how a display is implemented and presented to user </summary>
    public interface IDisplayAnnotation : IAnnotation
    {
        /// <summary> Optional text that provides a description of the item. </summary>
        string Description { get; }
        /// <summary> Optional text used to group displayed items. </summary>
        string[] Group { get; }
        /// <summary> Name displayed by the UI. </summary>
        string Name { get; }
        /// <summary> Optional integer that ranks items and groups in ascending order relative to other items/groups. 
        /// Default is -10000. For a group, the order is the average order of the elements inside the group. 
        /// Any double value is allowed. Items with same order are ranked alphabetically.
        /// </summary>
        double Order { get; }
        /// <summary> Boolean setting that indicates whether a group's default appearance is collapsed. </summary>
        bool Collapsed { get; }
    }

    /// <summary> Gets or sets the value of a thing. </summary>
    public interface IObjectValueAnnotation : IAnnotation
    {
        /// <summary> Gets or sets the current value. Note, for the value to be written to the owner object, Annotation.Write has to be called.</summary>
        object Value { get; set; }
    }

    /// <summary>
    /// A marker interface for object value annotations that comes from a merged source instead of a single-value source.
    /// </summary>
    public interface IMergedValueAnnotation : IObjectValueAnnotation
    {

    }

    /// <summary> Specifies how available values proxies are implemented. This class should rarely be implemented. Consider implementing just IAvailableValuesAnnotation instead.</summary>
    public interface IAvailableValuesAnnotationProxy : IAnnotation
    {
        /// <summary> Annotated available values. </summary>
        IEnumerable<AnnotationCollection> AvailableValues { get; }
        /// <summary> Annotated selected value. Note this should belong to the set of AvailableValues as well.</summary>
        AnnotationCollection SelectedValue { get; set; }
    }
    /// <summary> Specifies how suggested value proxies are implemented. This class should rarely be implemented. Consider implementing just ISuggestedValuesAnnotation instead.</summary>

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
    /// Enhances the IAvailableValuesAnnotation with a 'SelectedValue'. Having this ensures that objects that has been transformed can get read back in the correct way.
    /// </summary>
    interface IAvailableValuesSelectedAnnotation : IAvailableValuesAnnotation
    {
        /// <summary> Gets or sets the selected value. </summary>
        object SelectedValue { get; set; }
    }

    /// <summary> Defines a suggested values implementation.  </summary>
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

    /// <summary>
    /// If the object value is based on copying values, some performance optimizations can be done, so these string value annotations can be marked with this interface.
    /// </summary>
    interface ICopyStringValueAnnotation : IStringValueAnnotation
    {
        
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

    /// <summary> Defines how an error annotation works. Note: Multiple of IErrorAnnotation can be used in the same annotation. In this case the errors will be concatenated. </summary>
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
        /// <summary> Gets the member. </summary>
        IMemberData Member { get; }
    }

    /// <summary> Reflects the type of the object value being annotated.</summary>
    public interface IReflectionAnnotation : IAnnotation
    {
        /// <summary> The reflection info object. </summary>
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

    /// <summary>
    /// The merged method annotation marks a custom method annotation which overrides
    /// the standard behavior in the case where multiple values are merged.
    /// This can happen during multi select of a test step with a method.
    /// </summary>
    public interface IMergedMethodAnnotation : IMethodAnnotation
    {

    }

    /// <summary> Specifies how to implement basic collection annotations. </summary>
    public interface IBasicCollectionAnnotation : IAnnotation
    {
        /// <summary> he currently selected elements in the list. </summary>
        IEnumerable Elements { get; set; }
    }
    
    /// <summary> Used to mark a collection as fixed-size. </summary>
    public interface IFixedSizeCollectionAnnotation : IAnnotation
    {
        /// <summary> Gets if the collection annotated is fixed size. </summary>
        bool IsFixedSize { get; }
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

    /// <summary> Marks that a property should be ignored when annotating members.
    /// This can be applied as an optimization to properties in order to improve annotation performance. </summary>
    [AttributeUsage(AttributeTargets.Property )]
    public class AnnotationIgnoreAttribute : Attribute
    {
        
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
        /// <summary> Description of a value. </summary>
        /// <returns>A string describing the current value.</returns>
        string Describe();
    }

    /// <summary> Annotation for marking something as enabled or disabled. </summary>
    public interface IEnabledAnnotation : IAnnotation
    {
        /// <summary> Gets if an annotation is enabled. </summary>
        bool IsEnabled { get; }
    }

    /// <summary>
    /// Annotates that a member is read only.
    /// </summary>
    public class ReadOnlyMemberAnnotation : IAccessAnnotation
    {
        /// <summary> Always returns true.</summary>
        public bool IsReadOnly => true;
        /// <summary> Always returns true.</summary>
        public bool IsVisible => true;
    }

    class MembersAnnotation : INamedMembersAnnotation, IMembersAnnotation, IOwnedAnnotation
    {
        Dictionary<IMemberData, AnnotationCollection> members = new Dictionary<IMemberData, AnnotationCollection>();
        IEnumerable<AnnotationCollection> getMembers()
        {

            var val2 = fac.Get<IObjectValueAnnotation>().Value;
            var val = fac.Get<IReflectionAnnotation>();
            IEnumerable<IMemberData> _members;
            if (val2 != null)
                _members = TypeData.GetTypeData(val2).GetMembers();
            else _members = val.ReflectionInfo.GetMembers();
            var cnt = _members.Count();
            if (members.Count == cnt) return members.Values;
            if (members.Count == 0)
                members = new Dictionary<IMemberData, AnnotationCollection>(cnt);

            var members2 = val.ReflectionInfo.GetMembers();
            foreach (var item in members2)
            {
                if (item.HasAttribute<AnnotationIgnoreAttribute>()) continue;
                if (item.Readable == false) continue;
                GetMember(item);
            }

            return members.Values;

        }

        public IEnumerable<AnnotationCollection> Members => getMembers();

        readonly AnnotationCollection fac;
        public MembersAnnotation(AnnotationCollection fac)
        {
            this.fac = fac;
        }

        public void Read(object source)
        {
            var val = fac.Get<IObjectValueAnnotation>()?.Value;
            if (val == null) return;
            foreach (var mem in members)
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
            var objectValue = fac.Get<IObjectValueAnnotation>().Value;

            var annotation = fac.AnnotateMember(member, objectValue);
            members[member] = annotation;
            if (objectValue != null)
            {
                annotation.Read(objectValue);
            }

            return annotation;
        }
    }

    class EnabledIfAnnotation : IAccessAnnotation, IOwnedAnnotation, IEnabledAnnotation
    {

        public bool IsReadOnly
        {
            get
            {
                doRead();
                return isReadOnly;
            }   
        }

        public bool IsVisible
        {
            get
            {
                doRead();
                return isVisible;
            }
        }

        bool isReadOnly;
        bool isVisible;
        object source;

        void doRead()
        {
            if (source != null)
            {
                isReadOnly = !EnabledIfAttribute
                    .IsEnabled(mem.Member, source, out IMemberData _, out IComparable __, out bool hidden);
                isVisible = !hidden;
                source = null;
            }
        }

        public void Read(object source)
        {
            this.source = source;
        }

        public void Write(object source)
        {

        }

        IMemberAnnotation mem;
        public EnabledIfAnnotation(IMemberAnnotation mem)
        {
            this.mem = mem;
        }

        public bool IsEnabled => IsReadOnly == false;
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
                DoRead();
                if (string.IsNullOrWhiteSpace(error) == false)
                    return new[] { error };
                return Array.Empty<string>();
            }
        }

        object source;
        void DoRead()
        {
            var source = this.source;
            var mem = this.mem.Member;
            
            // Special case to add support for EmbeddedMemberData.
            // The embedded member may be nested in multiple layers
            // of embeddings normally it is just one level though.
            // iterate to grab the innermost source and member.
            while (mem is EmbeddedMemberData m2)
            {   
                if (source == null) return;
                source = m2.OwnerMember.GetValue(source);
                mem = m2.InnerMember;
            }

            if (source is IDataErrorInfo dataErrorInfo)
            {
                try
                {
                    error = dataErrorInfo[mem.Name];
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
                // set source to null to signal that errors has been read this time.
            }
            this.source = null; 
        }

        public void Read(object source) => this.source = source;

        public void Write(object source) { }
    }


    class NumberAnnotation : IStringValueAnnotation, IErrorAnnotation, ICopyStringValueAnnotation
    {
        public Type NullableType { get; set; }
        string currentError;
        public string Value
        {
            get
            {
                var value = annotation.Get<IObjectValueAnnotation>();
                if (value != null)
                {
                    var unit = annotation.Get<UnitAttribute>();
                    var value2 = value.Value;
                    if (NullableType != null && value2 == null)
                        return "";
                    return new NumberFormatter(CultureInfo.CurrentCulture, unit).FormatNumber(value2);
                }
                return null;
            }
            set
            {
                if (NullableType != null && value == "")
                {
                    var val = annotation.Get<IObjectValueAnnotation>();
                    val.Value = null;
                    return;
                }

                currentError = null;
                var unit = annotation.Get<UnitAttribute>();
                if (annotation.Get<IReflectionAnnotation>()?.ReflectionInfo is TypeData cst)
                {
                    object number = null;
                    try
                    {
                        number = new NumberFormatter(CultureInfo.CurrentCulture, unit).ParseNumber(value, NullableType ?? cst.Type);
                    }
                    catch (Exception e)
                    {
                        currentError = e.Message;
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

        public IEnumerable<string> Errors => currentError == null ? Array.Empty<string>() : new[] { currentError };
    }

    class TimeSpanAnnotation : IStringValueAnnotation, ICopyStringValueAnnotation
    {
        public string Value
        {
            get
            {

                if (annotation.Get<IObjectValueAnnotation>(from: this).Value is TimeSpan timespan)
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

    class NumberSequenceAnnotation : IStringValueAnnotation, ICopyStringValueAnnotation, IErrorAnnotation
    {
        string currentError;
        public IEnumerable<string> Errors => currentError == null ? Array.Empty<string>() : new[] { currentError };
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
                if (reflect.ReflectionInfo is TypeData cst)
                {
                    currentError = null;
                    try
                    {
                        var numbers = DoConvertBack(value, cst.Type, unit, CultureInfo.CurrentCulture);
                        objVal.Value = numbers;
                    }
                    catch (Exception e)
                    {
                        currentError = e.Message;
                    }    
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

    class BooleanValueAnnotation : IStringValueAnnotation, ICopyStringValueAnnotation
    {
        AnnotationCollection annotation;
        public BooleanValueAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }

        bool parseBool(string str) {
            if (string.Compare(str, "true", true) == 0)
                return true;
            if (string.Compare(str, "false", true) == 0)
                return false;
            throw new FormatException("Unable to parse string as boolean.");

        }
        public string Value
        {
            get => annotation.Get<IObjectValueAnnotation>().Value?.ToString();
            set => annotation.Get<IObjectValueAnnotation>().Value = parseBool(value);
        }

    }

    class MergedValueAnnotation : IMergedValueAnnotation, IOwnedAnnotation
    {
        public IEnumerable<AnnotationCollection> Merged => merged;
        readonly List<AnnotationCollection> merged;
        public MergedValueAnnotation(List<AnnotationCollection> merged)
        {
            this.merged = merged;
        }

        public object Value
        {
            get
            {
                if (merged.Count == 0) return null;
                // this getter is performance critical. Avoid doing any allocations or unnessesary operations here.
                var first = merged[0];
                object selectedValue = first.Get<IObjectValueAnnotation>().Value;
                if (selectedValue != null)
                {
                    for (int i = 1; i < merged.Count; i++)
                    {
                        var x = merged[i];
                        var thisVal = x.Get<IObjectValueAnnotation>().Value;
                        if (thisVal == selectedValue) 
                            continue;
                        if (selectedValue is IEnumerable ie1 && !(selectedValue is string))
                        {
                            // if the two lists has the same content it is fine to just return one of them.
                            // upon writing the two values will be cloned back.
                            // if they are not the same, null should be returned to signal this.
                            if (thisVal is IEnumerable ie2)
                            {
                                if (ie2.Cast<object>().SequenceEqual(ie1.Cast<object>()))
                                    continue;
                            }

                            return null;
                        }

                        if (Equals(selectedValue, thisVal) == false)
                            return null;
                    }
                }
                return selectedValue;
            }
            set
            {

                if (value is ValueType || value is string)
                {
                    //  The trivial case - just copy the values.
                    foreach (var m in merged)
                        m.Get<IObjectValueAnnotation>().Value = value;
                    return;
                }

                // same as for parameters.
                var first = merged[0];
                first.Get<IObjectValueAnnotation>().Value = value;
                var cloner = new ObjectCloner(value);
                foreach (var m in merged.Skip(1))
                {
                    var memberType = m.Get<IMemberAnnotation>()?.ReflectionInfo ?? TypeData.GetTypeData(value);
                    var context = m.ParentAnnotation?.Source;
                    // use the source of the parent annotation as the context object because it may contain
                    // additional information about how to decode the value internally
                    if (cloner.TryClone(context, memberType, false, out var val))
                        m.Get<IObjectValueAnnotation>().Value = val;
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
            // Force align the value using the object cloner.
            var currentValue = Value;
            if(currentValue != null)
                Value = Value;
            
            foreach (var annotation in merged)
                annotation.Write();
        }
    }

    /// <summary>
    /// Marker interface that indicates that an IAnnotation does not support multi-selecting. 
    /// When multi-selecting, the UI should not show properties annotated with this. 
    /// </summary>
    // Used by ManyToOneAnnotation
    public interface IHideOnMultiSelectAnnotation : IAnnotation { }

    class ManyToOneMethodAnnotation : IMethodAnnotation
    {
        AnnotationCollection annotation;
        public void Invoke()
        {
            foreach (var merged in annotation.Get<MergedValueAnnotation>().Merged)
            {
                var m = merged.Get<IMethodAnnotation>();
                m?.Invoke();
            }
        }

        public ManyToOneMethodAnnotation(AnnotationCollection a) => annotation = a;
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
                    var merged = parentAnnotation.Get<MergedValueAnnotation>();
                    annotatedElements = merged.Merged.ToArray();
                }
                else
                {
                    annotatedElements = c.AnnotatedElements.ToArray();
                }

                var sources = annotatedElements;
                var mems = sources.Select(x => x.Get<IMembersAnnotation>()?.Members ?? Array.Empty<AnnotationCollection>()).ToArray();
                if (mems.Length == 0) return Array.Empty<AnnotationCollection>();
                Dictionary<string, AnnotationCollection>[] dicts = mems.Select(x =>
                {
                    var dict = new Dictionary<string, AnnotationCollection>();
                    foreach (var d in x)
                    {
                        if (d.Get<IHideOnMultiSelectAnnotation>() != null)
                            continue;
                        var mem = d.Get<IMemberAnnotation>()?.Member;
                        var key = mem.GetDisplayAttribute().GetFullName() + mem.TypeDescriptor.Name;
                        if (dict.ContainsKey(key) == false)
                            dict[key] = d;
                    }
                    return dict;
                }).ToArray();
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
                            if (collectAll)
                                continue;
                            goto next_thing;
                        }

                        var otherAnnotation = thing2[name];

                        var othermember = otherAnnotation.Get<IMemberAnnotation>()?.Member;
                        if (mem == null) mem = othermember;

                        mergething.Add(thing2[name]);
                    }
                    if (mem == null) continue;
                    var newa = parentAnnotation.AnnotateMember(mem, sources[0].ExtraAnnotations.Append(new MergedValueAnnotation(mergething)).ToArray());
                    if(parentAnnotation.Get<MergedValueAnnotation>()?.Merged.First().Get<BreakConditionsAnnotation>() is BreakConditionsAnnotation br)
                    {
                        if(br.Value.Get<IMemberAnnotation>().Member.Name == mem.Name)
                            newa.Add(new BreakConditionsAnnotation.BreakConditionValueAnnotation(br) { valueAnnotation = newa });
                    }

                    var manyAccess = new ManyAccessAnnotation(mergething.SelectValues(x => x.Get<IAccessAnnotation>()).ToArray());
                    // Enabled if is not supported when multi-selecting.
                    newa.RemoveType<EnabledIfAnnotation>();
                    var method = newa.Get<IMethodAnnotation>();
                    if (method != null)
                    {
                        // skip adding many-to-one method annotation.
                        // if the method annotation can already handle the merged case.
                        if(!(method is IMergedMethodAnnotation))
                            newa.Add(new ManyToOneMethodAnnotation(newa));
                    }

                    var enabledValue = newa.Get<IEnabledValueAnnotation>();
                    if (enabledValue != null)
                    {
                        newa.Add(new ManyIEnabledValueAnnotation(newa));
                        newa.Remove(enabledValue);
                    }
                    
                    newa.Add(manyAccess);
                    
                    var manyError = new ManyErrorAnnotation(mergething.SelectValues(x => x.Get<IErrorAnnotation>()).ToArray());
                    newa.RemoveType<IErrorAnnotation>();
                    newa.Add(manyError);
                    
                    var enabledAnnotations = mergething.SelectMany(x => x.GetAll<IEnabledAnnotation>()).ToArray();
                    if (enabledAnnotations.Length > 0)
                    {
                        var manyEnabled = new ManyEnabledAnnotation(enabledAnnotations);
                        newa.RemoveType<IEnabledAnnotation>();
                        newa.Add(manyEnabled);
                    }

                    newa.Read(parentAnnotation.Get<IObjectValueAnnotation>().Value);

                    if (newa.Get<IStringValueAnnotation>() is IStringValueAnnotation strValueAnnotation && (strValueAnnotation is ICopyStringValueAnnotation == false))
                    {
                        // see comment on ManyToOneStringValueAnnotation
                        var merged = newa.Get<MergedValueAnnotation>();
                        if (merged != null)
                        {
                            int idx = newa.IndexWhen(x => x == strValueAnnotation);
                            // insert after last string value annotation.
                            newa.Insert(idx + 1, new ManyToOneStringValueAnnotation(merged));
                        }
                    }

                    IconAnnotationHelper.AddParameter(newa, mem, newa.Source);
                    
                    CommonAnnotations.Add(newa);

                next_thing:;
                }
                return members = CommonAnnotations.ToArray();
            }
        }

        class ManyIEnabledValueAnnotation : IEnabledValueAnnotation
        {
            AnnotationCollection annotation;
            public ManyIEnabledValueAnnotation(AnnotationCollection annotation) => this.annotation = annotation;

            public AnnotationCollection IsEnabled
            {
                get
                {
                    var eMember = annotation.Get<ManyToOneAnnotation>().Members
                        .FirstOrDefault(x => x.Get<IMemberAnnotation>()?.Member.Name == "IsEnabled");
                    return eMember;
                }
            }

            public AnnotationCollection Value
            {
                get
                {
                    var eMember = annotation.Get<ManyToOneAnnotation>().Members
                        .FirstOrDefault(x => x.Get<IMemberAnnotation>()?.Member.Name != "IsEnabled")?.Clone();
                    if(annotation.Get<UnitAttribute>() is UnitAttribute attr)
                        eMember.Add(attr);
                    if(annotation.Get<IAvailableValuesAnnotation>() is IAvailableValuesAnnotation avail)
                        eMember.Add(avail);
                    return eMember;
                }
            }
        }

        class ManyEnabledAnnotation : IEnabledAnnotation
        {
            readonly IEnabledAnnotation[] subAnnotations;

            public ManyEnabledAnnotation(IEnabledAnnotation[] subAnnotationsAnnotations)
            {
                subAnnotations = subAnnotationsAnnotations;
            }

            public bool IsEnabled => subAnnotations.All(x => x.IsEnabled);
        }

        /// <summary>
        /// Some string value annotations does not work very well with multi-select
        /// to mitigate that, a ManyToOneStringValueAnnotation is used.
        /// one example is MacroString.
        /// </summary>
        class ManyToOneStringValueAnnotation : IStringValueAnnotation
        {
            MergedValueAnnotation merged;

            public string Value
            {
                get
                {
                    string value = null;
                    bool first = true;
                    foreach (var m in merged.Merged)
                    {
                        var val = m.Get<IStringValueAnnotation>()?.Value;
                        if (first)
                        {
                            value = val;
                            first = false;
                        }
                        else
                        {
                            if (!object.Equals(val, value))
                                return null;
                        }
                    }

                    return value;
                }
                set
                {
                    foreach (var m in merged.Merged)
                    {
                        var sv = m.Get<IStringValueAnnotation>();
                        if (sv != null) sv.Value = value;
                    }
                }
            }

            public ManyToOneStringValueAnnotation(MergedValueAnnotation mva) => merged = mva;
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
            foreach (var annotation in members)
            {
                annotation.Read();
            }
        }

        public void Write(object source)
        {
            if (members != null)
            {
                foreach (var mem in members)
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

    /// <summary>
    /// This error annotation enables showing errors when multi select is being used. It sums up all the errors and distincts them.
    /// </summary>
    class ManyErrorAnnotation : IErrorAnnotation
    {
        readonly IErrorAnnotation[] others;

        public IEnumerable<string> Errors => others.SelectMany(x => x.Errors).Distinct();
        public ManyErrorAnnotation(IErrorAnnotation[] others)
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
    class MemberValueAnnotation : IObjectValueAnnotation, IOwnedAnnotation, IErrorAnnotation
    {
        readonly AnnotationCollection annotation;
        object currentValue;

        bool wasRead;
        bool wasSet;
        // the the member is a parameter we may need to update it
        // even if the value is the same as the cached one.
        bool isParameter; 
        public object Value
        {
            get
            {
                if (!wasRead)
                    read(); // lazy read.
                return currentValue;
            }
            set
            {
                wasSet = true;
                // current value is cached until next Read().
                wasRead = true;
                currentValue = value;
            }
        }

        public MemberValueAnnotation(AnnotationCollection annotation, IMemberAnnotation mem = null)
        {
            this.annotation = annotation;
            memberCache = mem;
        }

        // often the member annotation is know at 'annotate' time, so it is better to cache it here.
        // this section of the code is also performance-critical.
        IMemberAnnotation memberCache;
        void read()
        {
            if (annotation.Source == null) return;
            var m = memberCache ?? (memberCache = annotation.Get<IMemberAnnotation>());
            isParameter = m.Member is IParameterMemberData;
            try
            {
                currentValue = m.Member.GetValue(annotation.Source);
                // This value is now being cached until the next Read().
                wasRead = true;
            }
            catch
            {
                // the member itself threw an exception. 
            }
        }
        public void Read(object source)
        {
            wasRead = false;
        }

        public void Write(object source)
        {
            if (annotation.Source == null) return;
            
            var m = memberCache ?? (memberCache = annotation.Get<IMemberAnnotation>());
            if (m.Member.Writable == false) return;
            
            if (wasSet == false && !isParameter) return;
            
            error = null;
            try
            {
                if (object.Equals(currentValue, m.Member.GetValue(source)) == false || isParameter)
                    m.Member.SetValue(source, currentValue);
            }
            catch (Exception e)
            {
                error = e.GetInnerMostExceptionMessage();
            }
        }

        string error = null;

        public IEnumerable<string> Errors => error == null ? Array.Empty<string>() : new[] { error };
    }
    class ObjectValueAnnotation : IObjectValueAnnotation, IReflectionAnnotation, IOwnedAnnotation
    {
        public object Value { get; set; }

        public ITypeData ReflectionInfo => cachedType ?? (cachedType = (initType ?? TypeData.GetTypeData(Value)));
        ITypeData cachedType;
        ITypeData initType;
        public ObjectValueAnnotation(object value, ITypeData reflect) : this(value)
        {
            initType = reflect;
        }
        
        public ObjectValueAnnotation(object value) => Value = value;

        public void Read(object source)
        {
            // invalidate.
            cachedType = null; 
        }

        public void Write(object source) { }
    }
    class AvailableMemberAnnotation : IAnnotation
    {
        public AnnotationCollection AvailableMember;
        public AvailableMemberAnnotation(AnnotationCollection annotation)
        {
            this.AvailableMember = annotation;
        }
    }
    
    /// <summary>
    /// This is interface is used for generating something from a string.
    /// Currently it is used only for PluginTypeSelectorAttribute
    /// </summary>
    interface IAnnotationStringer : IAnnotation
    {
        /// <summary> Gets the string value of an object.  </summary>
        string GetString(AnnotationCollection item);
    }


    /// <summary> The default data annotator plugin. This normally forms the basis for annotation. </summary>
    public class DefaultDataAnnotator : IAnnotator
    {
        double IAnnotator.Priority => 1;

        class AvailableValuesAnnotation : IAvailableValuesAnnotation
        {
            string availableValuesMember;

            public IEnumerable AvailableValues
            {
                get
                {
                    var mem = annotation.ParentAnnotation.Get<IMembersAnnotation>()?.Members;
                    var mem2 = mem.FirstOrDefault(x => x.Get<IMemberAnnotation>()?.Member.Name == availableValuesMember);
                    if (mem2?.Get<IObjectValueAnnotation>() is MergedValueAnnotation merged)
                    {
                        // special handling for 'MergedValueAnnotation'
                        // let's try to intersect the lists.
                        
                        var lists = merged.Merged.Select(x => x.Get<IObjectValueAnnotation>().Value).OfType<IEnumerable>()
                            .ToArray();
                        
                        if (lists.FirstOrDefault() is IEnumerable lst)
                        {
                            var set = lst.Cast<object>().ToHashSet();
                            foreach (var subset in lists.Skip(1))
                            {
                                set.IntersectWith(subset.Cast<object>());
                            }

                            return set;
                        }
                    }
                        

                    return mem2?.Get<IObjectValueAnnotation>().Value as IEnumerable ?? Enumerable.Empty<object>();
                }
            }

            AnnotationCollection annotation;
            public AvailableValuesAnnotation(AnnotationCollection annotation, string available)
            {
                availableValuesMember = available;
                this.annotation = annotation;
            }
        }

        class MultipleAvailableValuesAnnotation : IAvailableValuesAnnotation, IMultiSelect
        {
            AvailableValuesAnnotation avail;
            AnnotationCollection annotation;
            string availableValuesMember;
            public MultipleAvailableValuesAnnotation(AnnotationCollection annotation, string avail2PropertyName)
            {
                availableValuesMember = avail2PropertyName;
                this.annotation = annotation;
                avail = new AvailableValuesAnnotation(annotation, avail2PropertyName);
            }

            public IEnumerable AvailableValues => avail.AvailableValues;

            public IEnumerable Selected
            {
                get => annotation.Get<IBasicCollectionAnnotation>().Elements;
                set
                {
                    var asCollection = annotation.Get<IBasicCollectionAnnotation>();
                    asCollection.Elements = value;
                }

            }
        }


        class InputStepAnnotation : IAvailableValuesSelectedAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation
        {
            struct InputThing
            {
                public ITestStep Step { get; set; }
                public IMemberData Member { get; set; }
                public override string ToString()
                {
                    if (Step == null) return "None";
                    return $"{Member.GetDisplayAttribute().Name} from {Step.GetFormattedName()}";
                }

                public static InputThing FromInput(IInput inp)
                {
                    return new InputThing { Step = inp.Step, Member = inp.Property };
                }

                public override bool Equals(object obj)
                {
                    if (obj is InputThing other)
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

            ITestStepParent getContextStep()
            {
                ITestStepParent step;
                if (parameterized)
                {
                    step = (annotation.Get<IMemberAnnotation>()?.Member as IParameterMemberData)
                        ?.ParameterizedMembers.Select(x => x.Source).OfType<ITestStep>()
                        .FirstOrDefault();
                }
                else
                {
                    AnnotationCollection parent = annotation;
                    while (parent.ParentAnnotation != null)
                        parent = parent.ParentAnnotation;

                    object context = parent.Get<IObjectValueAnnotation>().Value;
                    step = context as ITestStep;
                
                    if (context is IEnumerable enumerable_context)
                    {
                        step = enumerable_context.OfType<ITestStep>().FirstOrDefault();
                    }
                }

                while (step.Parent != null)
                    step = step.Parent;
                return step;
            }
            
            public IEnumerable AvailableValues
            {
                get
                {
                    var inp = getInput();
                    if (inp == null)
                        return Enumerable.Empty<object>();

                    ITestStepParent step = getContextStep();

                    var steps = Utils.FlattenHeirarchy(step.ChildTestSteps, x => x.ChildTestSteps);

                    List<InputThing> accepted = new List<InputThing>();
                    accepted.Add(new InputThing() );
                    if (inp is IInputTypeRestriction res)
                    {
                        foreach (var s in steps)
                        {
                            var t = TypeData.GetTypeData(s);
                            foreach (var mem in t.GetMembers())
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

            IInput getInput() => annotation.GetAll<IObjectValueAnnotation>()
                .FirstNonDefault(x => x.Value as IInput);

            // Use this when an instance of IInput is required, but the exact one is not critical
            IInput getInputRecursive()
            {
                // Recursively search for IInput instances. This should only be used during Write.
                // This is only relevant if the ObjectValueAnnotation is a MergedValueAnnotation.
                // If we are dealing with a merged value, we don't care if the current values are not aligned,
                // because they will be aligned after we call the setter.
                foreach (var ova in annotation.GetAll<IObjectValueAnnotation>())
                {
                    // This is the case in almost all instances
                    if (ova.Value is IInput i)
                        return i;

                    // This is the case when using unaligned merged values
                    if (ova is MergedValueAnnotation merged)
                    {
                        foreach (var m in merged.Merged)
                        {
                            if (m.Get<IObjectValueAnnotation>().Value is IInput i2)
                                return i2;
                        }
                    }
                }

                return null;
            }

            public void Read(object source)
            {
                setValue = null;
            }

            public void Write(object source)
            {
                if (!(setValue is InputThing v)) return;
                
                var inp = getInputRecursive();
                if (inp == null) return;

                inp.Step = v.Step;
                inp.Property = v.Member;
                 
                // If we are dealing with a MergedValueAnnotation, call the setter to propagate the IInput changes
                var merged = annotation.Get<MergedValueAnnotation>();
                if (merged != null) merged.Value = inp;
            }

            InputThing? setValue = null;

            public object SelectedValue
            {
                get
                {
                    if (setValue.HasValue == false)
                    {
                        var input = getInput();
                        if (input != null)
                            setValue = InputThing.FromInput(input);
                    }

                    return setValue;
                }

                set => setValue = value as InputThing?;
            }

            AnnotationCollection annotation;
            readonly bool parameterized;

            public InputStepAnnotation(AnnotationCollection annotation) => this.annotation = annotation;

            public InputStepAnnotation(AnnotationCollection annotation, bool parameterized) : this(annotation) =>
                this.parameterized = parameterized;
            
            public string Value
            {
                get 
                { 
                    var currentValue = annotation.GetAll<IObjectValueAnnotation>()
                        .FirstNonDefault(x => x.Value as IInput);
                    if(currentValue != null && currentValue.Property != null && currentValue.Step != null)
                        return $"{currentValue.Property?.GetDisplayAttribute().Name} from {currentValue.Step?.GetFormattedName()}";
                    return "None";
                }
            }
        }

        class EnumValuesAnnotation : IAvailableValuesAnnotation
        {
            IEnumerable availableValues;
            public IEnumerable AvailableValues
            {
                get
                {
                    if (availableValues == null)
                    {
                        var names = Enum.GetNames(enumType);
                        var values = Enum.GetValues(enumType);
                        
                        var orders = names.Select(x =>
                        {
                            var memberInfo = enumType.GetMember(x).FirstOrDefault();
                            return (memberInfo.GetDisplayAttribute(), memberInfo.IsBrowsable());
                        }).ToArray();
                        availableValues = Enumerable.Range(0, names.Length)
                            .Where(i => orders[i].Item2)
                            .OrderBy(i => orders[i].Item1.Order)
                            .Select(i => values.GetValue(i))
                            .ToArray();
                    }
                    return availableValues;
                }
            }

            readonly Type enumType;
            EnumValuesAnnotation(Type enumType) => this.enumType = enumType;

            static readonly ConcurrentDictionary<Type, EnumValuesAnnotation> lookup =
                new ConcurrentDictionary<Type, EnumValuesAnnotation>();
            public static EnumValuesAnnotation FromEnumType(Type type) =>
                lookup.GetOrAdd(type, x => new EnumValuesAnnotation(x));
        }

        class EnumStringAnnotation : IStringValueAnnotation, IValueDescriptionAnnotation, ICopyStringValueAnnotation
        {

            Enum evalue
            {
                get => a.Get<IObjectValueAnnotation>()?.Value as Enum;
                set => a.Get<IObjectValueAnnotation>().Value = value;
            }

            public string Value
            {
                get => Utils.EnumToReadableString(evalue);
                set {
                    var values = Enum.GetValues(enumType).Cast<Enum>();

                    var newvalue = values.FirstOrDefault(x => Utils.EnumToReadableString(x) == value);
                    if (newvalue == null)
                    {
                        newvalue = values.FirstOrDefault(x => Utils.EnumToReadableString(x).ToLower() == value.ToLower());
                    }
                    if (newvalue != null)
                        evalue = newvalue;
                    else
                        throw new FormatException($"Unable to parse {value} as an {enumType}");
                }
            }

            AnnotationCollection a;
            Type enumType;
            public EnumStringAnnotation(Type enumType, AnnotationCollection annotation)
            {
                this.a = annotation;
                this.enumType = enumType;
            }

            public string Describe()
            {
                if (evalue is Enum e)
                    return Utils.EnumToDescription(e);
                return null;
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
                        var zeroVal = Enum.ToObject(enumType, 0);
                        foreach (Enum enumValue in Enum.GetValues(enumType))
                        {
                            if (value.HasFlag(enumValue))
                            {
                                // To remove default value 0 for any value > 0 selected, else just select 0
                                if (value.Equals(zeroVal) || !enumValue.Equals(zeroVal))
                                    items.Add(enumValue);
                            }
                        }
                    }
                    prevSelected = items;
                    return items;
                }
                set
                {
                    var currSelected = value.Cast<Enum>();

                    // Get prev state
                    long prevBits = GetBitState(prevSelected);

                    long bits = SetBitState(prevBits, currSelected);
                    val.Value = Enum.ToObject(enumType, bits);
                }
            }

            IEnumerable<Enum> prevSelected = Enumerable.Empty<Enum>();
            IObjectValueAnnotation val => annotation.Get<IObjectValueAnnotation>();
            Type enumType;
            AnnotationCollection annotation;

            public FlagEnumAnnotation(AnnotationCollection annotation, Type enumType)
            {
                this.annotation = annotation;
                this.enumType = enumType;
            }

            private long GetBitState(IEnumerable state)
            {
                long bitValue = 0;
                foreach (Enum item in state)
                    bitValue |= Convert.ToInt64(item);

                return bitValue;
            }

            private long SetBitState(long bits, IEnumerable<Enum> currSelected)
            {
                // Get the diff (ie selection or unselection value)
                IEnumerable<Enum> diff = prevSelected.Except(currSelected);
                long removedBits = GetBitState(diff);
                if (diff.Any())
                    bits ^= removedBits;

                diff = currSelected.Except(prevSelected);
                long addedBits = GetBitState(diff);
                if (diff.Count() > 0)
                {
                    if (addedBits == 0)
                        bits = 0;   // Special handling for zero value to unselect all values
                    else
                        bits |= addedBits;
                }

                return bits;
            }
        }

        class StringValueAnnotation : IStringValueAnnotation, ICopyStringValueAnnotation
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
            bool member;
            public StepNameStringValue(AnnotationCollection annotation, bool member)
            {
                this.annotation = annotation;
                this.member = member;
            }
            public string Value
            {
                get
                {
                    object value;
                    if (member)
                    {
                        value = annotation.ParentAnnotation.Get<IObjectValueAnnotation>().Value;
                    }
                    else
                    {
                        value = annotation.Get<IObjectValueAnnotation>().Value;
                    }

                    if (value is ITestStep step)
                    {
                        return step.GetFormattedName();
                    }
                    
                    if (value is IEnumerable<ITestStep> steps)
                    {
                        string formattedName = steps.FirstOrDefault()?.GetFormattedName();
                        return steps.Skip(1).Any(s => s.GetFormattedName() != formattedName) ? null : formattedName;
                    }

                    return null;
                }
            }
        }

        class MethodAnnotation : IMethodAnnotation, IOwnedAnnotation
        {
            public void Invoke()
            {
                if (source == null)
                    throw new InvalidOperationException("Unable to invoke method");
                if(member.GetValue(source) is Action action)
                    action();
            }

            public MethodAnnotation(IMemberData member) => this.member = member;

            IMemberData member;
            object source;

            public void Read(object source) => this.source = source;

            public void Write(object source) { }
        }

        class BasicCollectionAnnotation : IBasicCollectionAnnotation, IOwnedAnnotation, IFixedSizeCollectionAnnotation
        {
            public IEnumerable Elements { get; set; }

            public bool IsFixedSize
            {
                get
                {
                    if (Elements is Array) return false; // to maintain backwards compatibility Array is not fixed size.
                    if (Elements is IList l) return l.IsFixedSize;
                    return false;
                }
            }

            IEnumerable origin;
            public void Read(object source)
            {
                Elements = annotations.Get<IObjectValueAnnotation>().Value as IEnumerable;
                origin = Elements;
            }

            bool isWriting;
            
            public void Write(object source)
            {
                if (isWriting) return;
                if (object.ReferenceEquals(origin, Elements)) return;
                var fac = annotations;
                bool rdonly = fac.Get<ReadOnlyMemberAnnotation>() != null;
                var objValue = fac.Get<IObjectValueAnnotation>();
                var lst = objValue.Value;
                if (lst is IList lst2)
                {
                    if (lst2.IsReadOnly)
                        rdonly = true;
                    if (!rdonly)
                    {
                        // Arrays must be re-allocated
                        if (lst2.GetType().IsArray)
                        {
                            // If lst2 is an array, re-allocate it to have the exact number of elements required
                            var cnt = Elements.Count();
                            if (cnt != lst2.Count)
                            {
                                var elemType = lst2.GetType().GetElementType();
                                lst2 = Array.CreateInstance(elemType!, cnt);
                                objValue.Value = lst2;
                            }
                        }
                        // Dynamic collections can just be cleared
                        else
                        {
                            lst2.Clear();
                        }
                    }

                    int index = 0;
                    foreach (var val in Elements)
                    {
                        if (!rdonly)
                        {
                            if (lst2.IsFixedSize)
                            {
                                lst2[index] = val;
                                index++;
                            }
                            else
                            {
                                lst2.Add(val);
                            }
                        }
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

            readonly AnnotationCollection annotations;

            public BasicCollectionAnnotation(AnnotationCollection annotations)
            {
                this.annotations = annotations;
            }
        }

        class MemberDataSequenceStringAnnotation : IStringReadOnlyValueAnnotation
        {
            readonly AnnotationCollection annotations;

            public MemberDataSequenceStringAnnotation(AnnotationCollection annotations) =>
                this.annotations = annotations;

            public string Value
            {
                get
                {
                    var seq = annotations.Get<IObjectValueAnnotation>().Value as IEnumerable;
                    var mems = seq?.OfType<IMemberData>() ?? Array.Empty<IMemberData>();
                    if (mems.Any() == false) return "None";
                    return string.Join(", ", mems.Select(x => x.GetDisplayAttribute().Name));
                }
            }
        }

        class GenericSequenceAnnotation : ICollectionAnnotation, IOwnedAnnotation, IStringReadOnlyValueAnnotation
        {
            public IEnumerable Elements => fac.Get<IObjectValueAnnotation>().Value as IEnumerable;
            /// <summary>
            /// Invalidated means that the values needs to get re-evaluated.
            /// So it may be that the previous value is used if the values are the same.
            /// This is also why Read needs to be called even if invalidate gets set.
            /// </summary>
            bool invalidated;

            IEnumerable<AnnotationCollection> annotatedElements;

            public IEnumerable<AnnotationCollection> AnnotatedElements
            {
                get
                {
                    if (invalidated || annotatedElements == null)
                    {
                        invalidated = false;
                        
                        var elements = Elements;
                        // if the elements is null, just return an empty array (below).
                        if (elements != null)
                        {
                            if (annotatedElements != null && elements.Cast<object>()
                                    .SequenceEqual(annotatedElements.Select(x => x.Source)))
                            {
                                // The values has not changed. Don't create a new list of annotation, just re-use the old.
                                return annotatedElements;
                            }

                            List<AnnotationCollection> annotations = new List<AnnotationCollection>();
                            foreach (var elem in elements)
                                annotations.Add(fac.AnnotateSub(null, elem));

                            annotatedElements = annotations;
                        }
                    }

                    return annotatedElements ?? Array.Empty<AnnotationCollection>();

                }
                set
                {
                    annotatedElements = value;
                }
            }

            public string Value => string.Format("Count: {0}", Elements?.Cast<object>().Count() ?? 0);

            AnnotationCollection fac;
            public GenericSequenceAnnotation(AnnotationCollection fac)
            {
                this.fac = fac;
            }

            public void Read(object source)     
            {
                invalidated = true;
                foreach (var elem in annotatedElements ?? Array.Empty<AnnotationCollection>())
                    elem.Read();
            }

            bool isWriting = false;
            public void Write(object source)
            {
                if (isWriting) return;
                if (annotatedElements == null) return;
                bool rdonly = fac.Get<ReadOnlyMemberAnnotation>() != null;
                var objValue = fac.Get<IObjectValueAnnotation>();
                var lst = objValue.Value;
                if (lst == null)
                { 
                    // if the list is null, create a new instance as long as the member is writable.
                    
                    if ((fac.Get<IMemberAnnotation>()?.Member?.Writable) == false)
                        throw new Exception($"Cannot add elements to collection because it is not writable.");
                    
                    var typedata = fac.Get<IReflectionAnnotation>().ReflectionInfo.AsTypeData();
                    if (typedata.DescendsTo(typeof(Array)))
                    {                    
                        // A new array of different length is created further down.
                        lst = Array.CreateInstance(typedata.ElementType.Type, 0);               
                    }
                    else if(typedata.CanCreateInstance)
                    {
                        lst = typedata.CreateInstance();
                    }

                    objValue.Value = lst;
                }

                if (lst is IList lst2)
                {
                    if (lst2.IsReadOnly)
                        rdonly = true;

                    if (!rdonly && !lst2.IsFixedSize)
                    {
                        // this clause contains a special case for lists
                        // Emulate adding/removing elements to the list
                        // calculate the changes so that fewest possible changes are done
                        // to the source list.

                        foreach (var elem in annotatedElements)
                        {
                            var val = elem.Get<IObjectValueAnnotation>().Value;
                            elem.Write(val);
                        }

                        var values = annotatedElements.Select(x => x.Get<IObjectValueAnnotation>().Value).ToList();

                        {   // remove elements that does no longer exists.

                            var hsh = values.ToHashSet();
                            for (int i = 0; i < lst2.Count; i++)
                            {
                                var e = lst2[i];
                                if (hsh.Contains(e) == false)
                                {
                                    lst2.RemoveAt(i);
                                    i--;
                                }
                            }
                        }

                        // now iteratively add/move elements
                        // so that lst2 becomes the same as 'values'

                        for (int i1 = 0,  i2 = 0; i1 < values.Count; i1++, i2++)
                        {
                            if (i1 >= lst2.Count)
                            {
                                lst2.Add(values[i1]);
                                continue;
                            }
                            if (lst2[i2] == values[i1])
                                continue; // same element.. we can continue.

                            for (int i3 = i2; i3 < lst2.Count; i3++)
                            {
                                // before inserting the element check that it does not figure further ahead in the list.
                                // if this is the case, remove it. This handles cases where things has been moved.
                                if (values[i1] == lst2[i3])
                                {
                                    lst2.RemoveAt(i3);
                                    break;
                                }
                            }

                            var val = values[i1];
                            if (val == null)
                            {
                                var typedata = fac.Get<IReflectionAnnotation>().ReflectionInfo.AsTypeData().ElementType;
                                if (typedata.CanCreateInstance || typedata.IsValueType)
                                    val = typedata.Type.CreateInstance();
                            }

                            lst2.Insert(i2, val);
                        }
                        while (lst2.Count > values.Count)
                        {
                            lst2.RemoveAt(lst2.Count - 1);
                        }

                    }
                    else
                    {

                        if (!rdonly)
                            lst2.Clear();

                        if (lst2.IsFixedSize)
                        {
                            var nElements = annotatedElements.Count();
                            if (nElements != lst2.Count)
                            {
                                var typedata = fac.Get<IReflectionAnnotation>().ReflectionInfo.AsTypeData();
                                if (typedata.DescendsTo(typeof(Array)))
                                {
                                    if ((fac.Get<IMemberAnnotation>()?.Member?.Writable) == false)
                                        throw new Exception($"Cannot add elements to collection because it is not writable.");
                                        
                                    lst2 = Array.CreateInstance(typedata.ElementType.Type, nElements);
                                    lst = lst2;

                                }
                                else
                                {
                                    throw new Exception("Could not extend container of fixed size.");
                                }
                            }
                        }

                        int index = 0;
                        foreach (var elem in annotatedElements)
                        {
                            var val = elem.Get<IObjectValueAnnotation>().Value;

                            elem.Write(val);
                            if (!rdonly)
                            {
                                if (lst2.IsFixedSize)
                                {
                                    lst2[index] = val;
                                    index++;
                                }
                                else
                                {
                                    lst2.Add(val);
                                }
                            }
                        }
                    }
                }

                if (rdonly && lst == null)
                {
                    //throw new Exception("Unable to show list value");
                    // an error should be thrown here, but that will critically break current implementations
                    // lets wait a few releases before we do that.
                    return;
                }

                // Some IObjectValue annotations works best if they are notified of a modification this way.
                // for example MergedValueAnnotation.
                objValue.Value = lst;
                
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


            public AnnotationCollection NewElement()
            {
                var reflect = fac.Get<IReflectionAnnotation>();

                if (reflect.ReflectionInfo is TypeData cstype)
                {
                    var elemType = cstype.Type.GetEnumerableElementType();

                    var elem2 = TypeData.FromType(elemType);
                    if (elem2.CanCreateInstance == false)
                    {
                        if (elem2.Type == typeof(string))
                            return fac.AnnotateSub(elem2, "");
                        if (elem2.IsNumeric)
                            return fac.AnnotateSub(elem2, Convert.ChangeType(0, elem2.Type));
                        object instance = null;
                        var member = fac.Get<IMemberAnnotation>()?.Member;
                       
                        if(member?.GetAttribute<ElementFactoryAttribute>() is ElementFactoryAttribute f)
                        {
                            var source = fac.Source;
                            if (member is IParameterMemberData param)
                            {
                                source = (param.ParameterizedMembers.FirstOrDefault(x => x.Member.GetAttribute<ElementFactoryAttribute>() == f).Source) ?? source;
                            }
                            instance = FactoryAttribute.Create(source, f);
                        }
                        if (instance != null)
                            return fac.AnnotateSub(null, instance);
                        
                        if (elem2.IsValueType)
                        {
                            if (elem2.DescendsTo(typeof(Enum)))
                                return fac.AnnotateSub(elem2, Enum.ToObject(elem2.Type, 0));
                            if(elem2.DescendsTo(typeof(DateTime)))
                                return fac.AnnotateSub(elem2, DateTime.MinValue);
                            if(elem2.DescendsTo(typeof(TimeSpan)))
                                return fac.AnnotateSub(elem2, TimeSpan.MinValue);
                            if(elem2.DescendsTo(typeof(bool)))
                                return fac.AnnotateSub(elem2, false);
                        }
                        return fac.AnnotateSub(elem2, null);
                    }
                    else
                    {
                        object instance = null;
                        try
                        {
                            var member = fac.Get<IMemberAnnotation>()?.Member;
                            if(member?.GetAttribute<ElementFactoryAttribute>() is ElementFactoryAttribute f)
                            {
                                var source = fac.Source;
                                if (member is IParameterMemberData param)
                                {
                                    source = (param.ParameterizedMembers.FirstOrDefault(x => x.Member.GetAttribute<ElementFactoryAttribute>() == f).Source) ?? source;
                                }
                                instance = FactoryAttribute.Create(source, f);
                            }
                            if(instance == null)
                            {
                               instance = elem2.CreateInstance(Array.Empty<object>());
                            }
                        }
                        catch
                        {

                        }
                        return fac.AnnotateSub(null, instance);
                    }
                }
                throw new InvalidOperationException();
            }
        }

        
        class ResourceAnnotation : IAvailableValuesAnnotation, IStringValueAnnotation, ICopyStringValueAnnotation, IErrorAnnotation
        {
            readonly Type baseType;
            readonly AnnotationCollection a;

            public IEnumerable AvailableValues 
            {
                get
                {
                    var x = ComponentSettingsList.GetContainers(baseType).Select(x => x.Cast<object>())
                        .SelectMany(x => x);
                    var cv = a.Get<IObjectValueAnnotation>()?.Value as IResource;
                    var result = x.Where(y => y.GetType().DescendsTo(baseType)).ToList();
                    // if the selected value is not in the list show it anyway.
                    if (cv != null && result.Contains(cv) == false)
                        result.Add(cv);

                    return result;
                }
            }
            
            string IStringReadOnlyValueAnnotation.Value => (a.Get<IObjectValueAnnotation>()?.Value as IResource)?.ToString();

            public string Value
            {
                get => (a.Get<IObjectValueAnnotation>()?.Value as IResource)?.ToString();
                set
                {
                    var values = AvailableValues.OfType<IResource>();
                    var resource = values.FirstOrDefault(x => x.Name == value) ?? values.FirstOrDefault(x => x.ToString() == value);
                    if(resource == null)
                        throw new FormatException("Unknown resource: " + value ?? "");
                    var objectValue = a.Get<IObjectValueAnnotation>();
                    if (objectValue != null)
                        objectValue.Value = resource;
                    else 
                        throw new Exception("Cannot set resource value");
                } 
            }

            public ResourceAnnotation(AnnotationCollection a, Type lowerstType)
            {
                baseType = lowerstType;
                this.a = a;
            }

            static readonly string[] errorResponse = {"The selected value has been deleted."};
            public IEnumerable<string> Errors
            {
                get
                {
                    var list = ComponentSettingsList.GetContainer(baseType) as IComponentSettingsList;
                    // if the selected value has been deleted. This can occur with resources referencing other resources.
                    if (a.Get<IObjectValueAnnotation>()?.Value is IResource cv && list?.GetRemovedAliveResources().Contains(cv) == true) 
                        return errorResponse;
                    return Array.Empty<string>();   
                }
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

        class MemberToStringAnnotation : IStringValueAnnotation, ICopyStringValueAnnotation
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

        class MultiResourceSelector : IMultiSelect, IStringReadOnlyValueAnnotation
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

            public string Value => string.Join(", ", Selected.Cast<IResource>().Select(s => s?.Name ?? ""));

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

        class EnabledAnnotation : IEnabledValueAnnotation, IMembersAnnotation, IOwnedAnnotation
        {
            class EnabledAccessAnnotation : IAccessAnnotation
            {
                AnnotationCollection parentAnnotation;
                public EnabledAccessAnnotation(AnnotationCollection parentAnnotation)
                {
                    this.parentAnnotation = parentAnnotation;
                }
                public bool IsReadOnly => (parentAnnotation.Get<IObjectValueAnnotation>().Value as IEnabled)?.IsEnabled == false;

                public bool IsVisible => true;
            }

            readonly AnnotationCollection annotations;

            private AnnotationCollection isEnabled;
            public AnnotationCollection IsEnabled
            {
                get
                {
                    if (isEnabled == null)
                        isEnabled = annotations.Get<IMembersAnnotation>(from : this).Members
                                               .FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name == nameof(Enabled<int>.IsEnabled));
                    return isEnabled;
                }
            }
            private AnnotationCollection value;
            public AnnotationCollection Value
            {
                get
                {
                    if (value != null) return value;
                    var unit = annotations.Get<UnitAttribute>();
                    var avail = annotations.Get<IAvailableValuesAnnotation>();
                    List<IAnnotation> extra = new List<IAnnotation>();
                    if (unit != null) { extra.Add(unit); }
                    if (avail != null) { extra.Add(avail); }
                    if (annotations.Get<DirectoryPathAttribute>() is DirectoryPathAttribute d)
                        extra.Add(d);
                    if (annotations.Get<FilePathAttribute>() is FilePathAttribute f)
                        extra.Add(f);
                    if (annotations.Get<ISuggestedValuesAnnotation>() is ISuggestedValuesAnnotation s)
                        extra.Add(s);

                    extra.Add(new EnabledAccessAnnotation(annotations));
                    var valueMember = annotations.Get<IMembersAnnotation>(from: this).Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member.Name != nameof(Enabled<int>.IsEnabled));
                    var src = annotations.Get<IObjectValueAnnotation>().Value as IEnabledValue;
                    var sub = annotations.AnnotateSub(valueMember.Get<IReflectionAnnotation>().ReflectionInfo, src?.Value, extra.ToArray());
                    sub.Add(new AnnotationCollection.MemberAnnotation(TypeData.GetTypeData(src).GetMember("Value") ?? TypeData.FromType(typeof(IEnabledValue)).GetMember("Value"))); // for compatibility with 9.8 UIs, emulate that this is a Value member from a Enabled<T> class
                    value = sub;
                    return value;
                }
            }

            public IEnumerable<AnnotationCollection> Members => new [] { IsEnabled, Value };

            public EnabledAnnotation(AnnotationCollection annotations)
            {
                this.annotations = annotations;
            }

            public void Read(object source)
            {
                if (Members != null)
                {
                    var val = annotations.Get<IObjectValueAnnotation>().Value;
                    if (val == null) return;
                    foreach (var member in Members)
                        member.Read(val);
                }

                if (value != null)
                {
                    var val = annotations.Get<IObjectValueAnnotation>().Value as IEnabledValue;
                    value.Get<ObjectValueAnnotation>().Value = val?.Value;
                }
            }

            public void Write(object source)
            {
                if (Members != null)
                {
                    var val = annotations.Get<IObjectValueAnnotation>().Value;
                    foreach (var member in Members)
                        member.Write(val);
                    {
                        // since the Enabled annotation overrides the other IMembersAnnotation
                        // we need to make sure to update that too.
                        var otherMembers = annotations.Get<IMembersAnnotation>(from: this);
                        foreach (var member in otherMembers.Members)
                            member.Read();
                    }
                }
                if (value != null)
                {
                    if(annotations.Get<IObjectValueAnnotation>().Value is IEnabledValue en)
                        en.Value = value.Get<ObjectValueAnnotation>().Value;
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
                List<string> result = new List<string>();
                foreach (var plugin in plugins)
                {
                    try
                    {
                        var device_discoverer = (IDeviceDiscovery)Activator.CreateInstance(plugin);
                        if (device_discoverer.CanDetect(device_attr))
                        {
                            result.AddRange(device_discoverer.DetectDeviceAddresses(device_attr));
                        }
                    }
                    catch
                    {

                    }
                }
                return result.Distinct().ToArray();
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

        class TestStepMultiSelectAnnotation : IAvailableValuesAnnotation, IMultiSelect, IStringReadOnlyValueAnnotation
        {
            AnnotationCollection annotation;
            public TestStepMultiSelectAnnotation(AnnotationCollection annotation) => this.annotation = annotation;

            public IEnumerable AvailableValues => annotation.Get<TestStepSelectAnnotation>().AvailableValues;
            public IEnumerable Selected
            {
                get => annotation.Get<IBasicCollectionAnnotation>().Elements;
                set => annotation.Get<IBasicCollectionAnnotation>().Elements = value;
            }

            public string Value => $"{Selected?.Cast<object>().Count()} Steps Selected";
        }

        class TestStepSelectAnnotation : IAvailableValuesAnnotation, IStringValueAnnotation, ICopyStringValueAnnotation
        {
            public IEnumerable AvailableValues
            {
                get
                {
                    var member = annotation.Get<IMemberAnnotation>()?.Member;
                    if (member == null) return Enumerable.Empty<object>();
                    var sibling = member.GetAttribute<StepSelectorAttribute>();
                    if (sibling == null) sibling = new StepSelectorAttribute(StepSelectorAttribute.FilterTypes.All);
                    var step = annotation.ParentAnnotation.Get<IObjectValueAnnotation>().Value as ITestStep;
                    var basicType = member.TypeDescriptor;
                    if (basicType is TypeData td && td.ElementType is ITypeData)
                        basicType = td.ElementType; // for enumerables of steps
                    return getSteps(step, sibling.Filter).Where(x => TypeData.GetTypeData(x).DescendsTo(basicType));
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

  
        class PluginTypeSelectAnnotation : IAvailableValuesAnnotation, IAnnotationStringer
        {
            /// <summary> This is used for generating strings for available and selected values. </summary>
            public string GetString(AnnotationCollection item)
            {
                return item.Get<IDisplayAnnotation>()?.Name;
            }
            
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
                    if (member.TypeDescriptor is TypeData cst)
                    {
                        var currentType = TypeData.GetTypeData(currentValue);
                        List<object> selection = new List<object>();
                        
                        if (attrib.ObjectSourceProperty != null)
                        {
                            // if there is a source property, look for that. 
                            var obj = annotation.ParentAnnotation.Source;
                            var mem = TypeData.GetTypeData(obj).GetMember(attrib.ObjectSourceProperty);
                            var src = mem.GetValue(obj) as IEnumerable;
                            foreach (var item in src)
                                selection.Add(item);
                        }
                        else
                        {
                            // there is no objects source, so just generate them from the plugins.
                            foreach (var type in PluginManager.GetPlugins(cst.Type))
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
                        }
                        this.selection = selection;
                    }
                    return selection ?? Enumerable.Empty<object>();
                }
            }

            AnnotationCollection annotation;
            public PluginTypeSelectAnnotation(AnnotationCollection annotation) => this.annotation = annotation;

        }

        /// <summary>
        /// For annotating MetaDataPromptObjects. This is only used when running the test plan with AllowPromptMetaData enabled.
        /// </summary>
        class MetaDataPromptAnnotation : IForwardedAnnotations, IOwnedAnnotation
        {
            AnnotationCollection annotation;
            public MetaDataPromptAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }

            IEnumerable<AnnotationCollection> forwarded;
            
            public IEnumerable<AnnotationCollection> Forwarded
            {
                get
                {
                    if (forwarded != null) return forwarded;
                        
                    List<AnnotationCollection> metadataAnnotations = new List<AnnotationCollection>();
                    MetadataPromptObject obj = (MetadataPromptObject)annotation.Get<IObjectValueAnnotation>().Value;
                    var named = annotation.Get<INamedMembersAnnotation>();
                    if (named == null) return Enumerable.Empty<AnnotationCollection>();
                    var member = named.GetMember(TypeData.GetTypeData(obj).GetMember(nameof(MetadataPromptObject.Resources)));
                    var col = member.Get<ICollectionAnnotation>();
                    foreach (var annotatedResource in col.AnnotatedElements) {
                        object resource = annotatedResource.Get<IObjectValueAnnotation>().Value;
                        var named2 = annotatedResource.Get<INamedMembersAnnotation>();
                        var type = TypeData.GetTypeData(resource);
                        var rname = annotatedResource.Get<IStringReadOnlyValueAnnotation>()?.Value ?? resource.ToString();
                        foreach (var member2 in type.GetMembers())
                        {
                            if (member2.GetAttribute<MetaDataAttribute>() is MetaDataAttribute attr && attr.PromptUser)
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

                    forwarded = metadataAnnotations;
                    return metadataAnnotations;
                }
            }

            public void Read(object source) => forwarded?.ForEach(elem => elem.Read());
            public void Write(object source) => forwarded?.ForEach(elem => elem.Write());
        }
        void IAnnotator.Annotate(AnnotationCollection annotation)
        {
            var reflect = annotation.Get<IReflectionAnnotation>();
            var mem = annotation.Get<IMemberAnnotation>();
            if (mem == null && reflect != null)
            {
                if (reflect.ReflectionInfo.DescendsTo(typeof(IDisplayAnnotation)))
                    annotation.Add(new DisplayAnnotationWrapper());
                else
                    annotation.Add(reflect.ReflectionInfo.GetDisplayAttribute());
            }

            bool rd_only = annotation.Get<ReadOnlyMemberAnnotation>() != null;
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

            if (annotation.Source is ITestStep step)
            {
                // if any parent step has disabled their child steps list.
                // then the settings of this step should be disabled.
                // There is an overlap between it being "ReadOnly" and "Disabled"
                // in this case we want it to be disabled, because e.g buttons should not be clickable.
                if(step.GetParents().Any(parent => parent.ChildTestSteps.IsReadOnly))
                    annotation.Add(DisabledSettingsAnnotation.Instance);
            }

            if (mem != null)
            {
                if (annotation.Get<IObjectValueAnnotation>() == null)
                    annotation.Add(new MemberValueAnnotation(annotation));

                var attributes = mem.Member.Attributes;
                bool displayFound = false;
                Sequence.ProcessPattern(attributes,
                    (SuggestedValuesAttribute suggested) => annotation.Add(new SuggestedValueAnnotation(annotation, suggested.PropertyName)),
                    (DeviceAddressAttribute x) => annotation.Add(new DeviceAddressAnnotation(annotation)),
                    (PluginTypeSelectorAttribute x) => annotation.Add(new PluginTypeSelectAnnotation(annotation)),
                    (DisplayAttribute x) =>
                    {
                        displayFound = true;
                        annotation.Add(x);
                    });
                if(!displayFound)
                    annotation.Add(mem.Member.GetDisplayAttribute());

                var browsable = mem.Member.GetAttribute<BrowsableAttribute>();
                if(mem.Member.Writable == false || browsable != null)
                    annotation.Add(new DefaultAccessAnnotation(mem.Member.Writable == false, browsable?.Browsable ?? true));

                if (mem.Member.TypeDescriptor.DescendsTo(typeof(Action<object>)) || mem.Member.TypeDescriptor.DescendsTo(typeof(Action)))
                    annotation.Add(new MethodAnnotation(mem.Member));


                if (mem.ReflectionInfo.DescendsTo(typeof(TimeSpan)))
                    annotation.Add(new TimeSpanAnnotation(annotation));
                if (mem.ReflectionInfo.DescendsTo(typeof(DateTime)))
                    annotation.Add(new DateTimeAnnotation(annotation));
                
                Sequence.ProcessPattern(attributes, 
                    (UnitAttribute x) => annotation.Add(x), 
                    (HelpLinkAttribute x) => annotation.Add(x),
                    (ColumnDisplayNameAttribute x) => annotation.Add(x),
                    (FilePathAttribute x) => annotation.Add(x),
                    (DirectoryPathAttribute x) => annotation.Add(x),
                    (IconAnnotationAttribute x) => annotation.Add(x)
                );

                IconAnnotationHelper.AddParameter(annotation, mem.Member, annotation.Source);
                
            }

            var availMem = annotation.Get<AvailableMemberAnnotation>();
            if (availMem != null)
            {
                var da = availMem.AvailableMember.Get<UnitAttribute>();
                if (da != null)
                    annotation.Add(da);
            }

            if (mem != null)
            {
                var member = mem.Member;
                if (member.DeclaringType.DescendsTo(typeof(IValidatingObject)))
                {
                    annotation.Add(new ValidationErrorAnnotation(mem));
                }
                else if (member is EmbeddedMemberData emb)
                {
                    // if the member is not part of a validating object, but
                    // it comes from an embedded property which is, then the annotation
                    // should also be added.
                    while (emb != null)
                    {
                        if (emb.InnerMember.DeclaringType.DescendsTo(typeof(IValidatingObject)))
                        {
                            annotation.Add(new ValidationErrorAnnotation(mem));
                            break;
                        }
                        emb = emb.InnerMember as EmbeddedMemberData;
                    }
                }

                if (member.HasAttribute<EnabledIfAttribute>())
                {
                    annotation.Add(new EnabledIfAnnotation(mem));
                }
                if (member.Writable == false)
                {
                    annotation.Add(new ReadOnlyMemberAnnotation());
                }
                
                    
            }

            if (reflect?.ReflectionInfo is TypeData csharpType)
            {
                var type = csharpType.Load();
                
                bool isNullable = type.IsPrimitive == false && type.IsGenericType && csharpType.IsValueType && type.DescendsTo(typeof(Nullable<>));
                if (isNullable)
                {
                    Type type2 = type.GetGenericArguments().FirstOrDefault();
                    if (type2.IsNumeric())
                    {
                        annotation.Add(new NumberAnnotation(annotation) { NullableType = type2 });
                    }
                }

                if (type.IsNumeric())
                {
                    annotation.Add(new NumberAnnotation(annotation));
                }
                if (type == typeof(bool)) {
                    annotation.Add(new BooleanValueAnnotation(annotation));
                }
                if (type == typeof(string))
                    annotation.Add(new StringValueAnnotation(annotation));
                if (type == typeof(MacroString))
                {
                    annotation.Add(new MacroStringValueAnnotation(annotation));
                }
                if (type == typeof(MetadataPromptObject))
                {
                    annotation.Add(new MetaDataPromptAnnotation(annotation));
                }

                if (type.IsPrimitive == false)
                {
                    if (type != typeof(String) && csharpType.ElementType != null)
                    {
                        annotation.Add(new BasicCollectionAnnotation(annotation));
                        var innerType = csharpType.ElementType;
                        if (innerType.IsNumeric)
                        {
                            annotation.Add(new NumberSequenceAnnotation(annotation));
                        }
                        else
                        {
                            // the type must implement IList, otherwise it cannot be used by generic sequence annotation.
                            // this excludes IEnumerable, but not array types or List<T> types. 
                            annotation.Add(new GenericSequenceAnnotation(annotation));
                            if (!rd_only && innerType.DescendsTo(typeof(IResource)))
                            {
                                annotation.Add(new ResourceAnnotation(annotation, innerType.Type));
                                annotation.Add(new MultiResourceSelector(annotation, innerType.Type));
                            }
                            else if (!rd_only && innerType.DescendsTo(typeof(ITestStep)))
                            {
                                annotation.Add(new TestStepSelectAnnotation(annotation));
                                annotation.Add(new TestStepMultiSelectAnnotation(annotation));
                            }
                            else if (innerType.DescendsTo(typeof(ViaPoint)))
                                annotation.Add(new ViaPointAnnotation(annotation));
                            else if (innerType.DescendsTo(typeof(IMemberData)))
                                annotation.Add(new MemberDataSequenceStringAnnotation(annotation));
                        }
                    }

                    if (type.IsEnum)
                    {
                        if (type == typeof(BreakCondition))
                        {
                            annotation.Add(new BreakConditionsAnnotation(annotation));
                        }
                        else
                        {    
                            annotation.Add(EnumValuesAnnotation.FromEnumType(type));
                            annotation.Add(new EnumStringAnnotation(type, annotation));

                            if (csharpType.HasFlags())
                            {
                                annotation.Add(new FlagEnumAnnotation(annotation, type));
                            }
                        }
                    }

                    if (csharpType.IsValueType == false && type.DescendsTo(typeof(IResource)))
                        annotation.Add(new ResourceAnnotation(annotation, type));
                    else if (csharpType.IsValueType == false && type.DescendsTo(typeof(ITestStep)) && mem?.Member.DeclaringType?.DescendsTo(typeof(ITestStepParent)) == true)
                        annotation.Add(new TestStepSelectAnnotation(annotation));
                }
            }

            if (mem?.Member is IMemberData mem2)
            {
                if (mem2.GetAttribute<AvailableValuesAttribute>() is AvailableValuesAttribute avail)
                {
                    if (mem2.TypeDescriptor.DescendsTo(typeof(IEnumerable<>)) && mem2.TypeDescriptor.IsA(typeof(string)) == false)
                    {
                        annotation.Add(new MultipleAvailableValuesAnnotation(annotation, avail.PropertyName));
                    }
                    else
                    {
                        annotation.Add(new AvailableValuesAnnotation(annotation, avail.PropertyName));
                    }
                }

                if (mem2.TypeDescriptor.DescendsTo(typeof(IPicture)))
                {
                    annotation.Add(new PictureAnnotation(annotation));
                }

                if (mem2.DeclaringType.DescendsTo(typeof(ITestStep)))
                {

                    if (mem2.Name == nameof(ITestStep.Name))
                        annotation.Add(new StepNameStringValue(annotation, member: true));
                    
                    if (mem2.TypeDescriptor.DescendsTo(typeof(IInput)))
                        annotation.Add(new InputStepAnnotation(annotation));
                }else if (mem2 is IParameterMemberData param)
                {
                    if (mem2.TypeDescriptor.DescendsTo(typeof(IInput)) 
                        && param.ParameterizedMembers.All(x => x.Member.DeclaringType.DescendsTo(typeof(ITestStep))))
                        annotation.Add(new InputStepAnnotation(annotation, true));
                }

                if (mem2.DeclaringType.DescendsTo(typeof(ITestStepParent)))
                {
                    annotation.Add(new MenuAnnotation(mem2, mem2.DeclaringType, annotation.ParentAnnotation));
                }

                if (mem2.Name == nameof(ParameterManager.NamingQuestion.Settings) && mem2.DeclaringType.DescendsTo(TypeData.FromType(typeof(ParameterManager.NamingQuestion))))
                    annotation.Add(new ParameterManager.SettingsName(annotation));
            }
            
            if (reflect?.ReflectionInfo is ITypeData tp)
            {
                if (tp.DescendsTo(typeof(ITestStep)))
                {
                    annotation.Add(new StepNameStringValue(annotation, member: false));                
                }

                // When not annotating a member, but an object, we add type annotation.
                if (annotation.Any(x => x is MenuAnnotation) == false && (tp.DescendsTo(typeof(ITestStepParent)) || tp.DescendsTo(typeof(IResource))))
                    annotation.Add(new MenuAnnotation(tp));
          
                bool csharpPrimitive = tp is TypeData cst && (cst.Type.IsPrimitive || cst.Type == typeof(string));
                if (tp.GetMembers().Any(x => x.HasAttribute<AnnotationIgnoreAttribute>() == false) && !csharpPrimitive)
                {
                    annotation.Add(new MembersAnnotation(annotation));
                    if (tp.DescendsTo(typeof(IEnabled)))
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

    
    internal class DisabledSettingsAnnotation : IEnabledAnnotation
    {
        public bool IsEnabled => false;
        public static DisabledSettingsAnnotation Instance { get; } = new DisabledSettingsAnnotation();
    }

    internal class DisplayAnnotationWrapper : IAnnotation, IDisplayAnnotation, IOwnedAnnotation
    {
        public string Description { get; private set; }

        public string[] Group { get; private set; }

        public string Name { get; private set; }

        public double Order { get; private set; }

        public bool Collapsed { get; private set; }

        public void Read(object source)
        {
            if (source is IDisplayAnnotation src)
            {
                Name = src.Name;
                Description = src.Description;
                Group = src.Group;
                Order = src.Order;
                Collapsed = src.Collapsed;
            }
        }

        public void Write(object source)
        {
           
        }
    }


    /// <summary> Proxy annotation for wrapping simpler annotation types. For example IAvailableValuesAnnotation is wrapped in a IAvailableValuesAnnotationProxy.</summary>
    public class ProxyAnnotation : IAnnotator
    {
        class AvailableValuesAnnotationProxy : IAvailableValuesAnnotationProxy, IOwnedAnnotation
        {
            IEnumerable<AnnotationCollection> annotations;
            object[] prevValues = Array.Empty<object>();
            public IEnumerable<AnnotationCollection> AvailableValues
            {
                get
                {
                    IEnumerable values;
                    if (annotations != null)
                    {
                        if (invalidated)
                        {
                            invalidated = false;
                            values = a.Get<IAvailableValuesAnnotation>()?.AvailableValues;
                            if (prevValues.SequenceEqual(values?.Cast<object>() ?? Array.Empty<object>()))
                                return annotations;
                        }
                        else
                        {
                            return annotations;
                        }
                    }
                    else
                    {
                        values = a.Get<IAvailableValuesAnnotation>()?.AvailableValues ?? Array.Empty<object>();    
                    }

                    if (a?.Get<MergedValueAnnotation>() is MergedValueAnnotation merged)
                    {
                        // Merged available value fields are the common of all the original available values.
                        HashSet<object> values2 = null;
                        foreach (var m in merged.Merged)
                        {
                            var e =
                                (m.Get<IAvailableValuesAnnotation>()?.AvailableValues as IEnumerable)?.Cast<object>();
                            if (e == null) continue;
                            if (values2 == null)
                            {
                                values2 = new HashSet<object>(e);
                                continue;
                            }

                            foreach (var value in values2.ToArray())
                            {
                                if (e.Contains(value) == false)
                                {
                                    values2.Remove(value);
                                }
                            }
                        }

                        values = values2;
                    }

                    if (values != null)
                        prevValues = values.Cast<object>().ToArray();
                    else
                    {
                        prevValues = Array.Empty<object>();
                    }

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
                            var da2 = a.AnnotateSub(TypeData.GetTypeData(obj), obj, readOnly,
                                new AvailableMemberAnnotation(a));
                            
                            // the annotation stringer is just used for PluginTypeSelector
                            var annotationStringer = a.Get<IAnnotationStringer>();
                            if (annotationStringer != null)
                                da2.Add(new ConstStringAnnotation(annotationStringer.GetString(da2)));
                            
                            lst.Add(da2);
                        }
                    }

                    lst.RemoveIf(x => x.Get<IAccessAnnotation>()?.IsVisible == false);
                    annotations = lst;
                    return annotations;
                }
            }

            /// <summary>  This is just a constant string shown in the UI. </summary>
            class ConstStringAnnotation : IStringReadOnlyValueAnnotation
            {
                public ConstStringAnnotation(string str) => Value = str;
                public string Value { get; }
            }
            
            public AnnotationCollection SelectedValue
            {
                get
                {
                    object current;

                    if (a.Get<IAvailableValuesSelectedAnnotation>() is IAvailableValuesSelectedAnnotation x)
                        current = x.SelectedValue;
                    else
                        current = a.Get<IObjectValueAnnotation>()?.Value;

                    if (current == null) return null;
                    foreach (var a2 in AvailableValues)
                    {
                        if (object.Equals(current, a2.Get<IObjectValueAnnotation>()?.Value))
                        {
                            return a2;
                        }
                    }

                    var a3 = a.AnnotateSub(TypeData.GetTypeData(current), current, new ReadOnlyMemberAnnotation(), new AvailableMemberAnnotation(a));

                    {   // the annotation stringer is just used for PluginTypeSelector
                        var stringer = a.Get<IAnnotationStringer>();
                        if (stringer != null) a3.Add(new ConstStringAnnotation(stringer.GetString(a3)));
                    }

                    return a3;
                }
                set
                {
                    if (value == null) return;
                    if (a.Get<IAvailableValuesSelectedAnnotation>() is IAvailableValuesSelectedAnnotation x)
                        x.SelectedValue = value.Get<IObjectValueAnnotation>().Value;
                    else
                        a.Get<IObjectValueAnnotation>().Value = value.Get<IObjectValueAnnotation>().Value;
                }
            }

            AnnotationCollection a;
            
            public AvailableValuesAnnotationProxy(AnnotationCollection a)
            {
                this.a = a;
            }

            // when the object has been re-read, we need to check if the AvailableValues annotations needs update.
            bool invalidated;
            
            public void Read(object source)
            {
                invalidated = true;
                if (annotations == null) return;
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
                        var values = a.Get<ISuggestedValuesAnnotation>()?.SuggestedValues ?? Enumerable.Empty<object>();
                        // the same reference of an the value may be updated, so keep a copy of the values instead of a reference.
                        prevValues = values.OfType<object>().ToArray();

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
        
        /// <summary>
        /// For things that can be multi-selected, for example flag enum.
        /// </summary>
        class MultiSelectProxy : IMultiSelectAnnotationProxy
        {
            IEnumerable<AnnotationCollection> annotations => annotation.Get<IAvailableValuesAnnotationProxy>().AvailableValues;

            IEnumerable selection
            {
                get => annotation.Get<IMultiSelect>().Selected;
                set => annotation.Get<IMultiSelect>().Selected = value;
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

            readonly AnnotationCollection annotation;

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

            bool isReadOnly = false;
            public bool IsReadOnly
            {
                get
                {
                    doRead();
                    return isReadOnly;
                }
            }

            bool isVisible = true;

            public bool IsVisible {
                get
                {
                    doRead();
                    return isVisible;
                }
            }

            bool wasRead = false;
            void doRead()
            {
                if (wasRead) return;
                wasRead = true;
                isReadOnly = false;
                isVisible = true;
                foreach (var access in annotations.GetAll<IAccessAnnotation>())
                {
                    isReadOnly |= access.IsReadOnly;
                    isVisible &= access.IsVisible;
                }
            }

            public void Read(object source)
            {
                wasRead = false;
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

                if (annotation.Get<ISuggestedValuesAnnotation>() != null)
                    annotation.Add(new SuggestedValuesAnnotationProxy(annotation));
            }
            
            annotation.Add(new AccessProxy(annotation));
            
        }
    }

    /// <summary>
    /// Used for wrapping multi selections of objects.
    /// </summary>
    public class MultiObjectAnnotator : IAnnotator
    {
        class MergedValidationErrorAnnotation : IErrorAnnotation
        {
            readonly AnnotationCollection annotation;
            public MergedValidationErrorAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
            }

            public IEnumerable<string> Errors => annotation.Get<MergedValueAnnotation>().Merged
                .SelectMany(a => a.Get<ValidationErrorAnnotation>()?.Errors ?? Enumerable.Empty<string>())
                .Distinct();
        }
        class MergedAvailableValues : IAvailableValuesAnnotation
        {
            public IEnumerable AvailableValues
            {
                get
                {
                    var merged = annotation.Get<MergedValueAnnotation>();
                    if (merged == null) return Enumerable.Empty<object>();

                    Dictionary<object, int> counts = new Dictionary<object, int>();
                    int maxCount = 0;
                    foreach (var annotation in merged.Merged)
                    {
                        maxCount += 1;
                        var values = annotation.Get<IAvailableValuesAnnotation>()?.AvailableValues;
                        if (values != null)
                        {
                            foreach (var value in values)
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

            var validationErrors = annotation.Get<ValidationErrorAnnotation>();
            if (validationErrors != null)
            {
                annotation.Remove(validationErrors);
                annotation.Add(new MergedValidationErrorAnnotation(annotation));
            }
            
            var members = annotation.Get<IMembersAnnotation>();
            
            if (members != null)
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

        [ThreadStatic] static List<IAnnotator> Annotators;
        /// <summary> </summary>
        public AnnotationResolver()
        {
            var annotatorTypes = PluginManager.GetPlugins<IAnnotator>();
            if (Annotators == null || Annotators.Count != annotatorTypes.Count)
            {
                Annotators = annotatorTypes.Select(x => Activator.CreateInstance(x)).OfType<IAnnotator>().ToList();
                Annotators.Sort((x, y) => x.Priority.CompareTo(y.Priority));
            }

            annotators = Annotators;
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
        internal class MemberAnnotation : IMemberAnnotation, IReflectionAnnotation
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
        private List<IAnnotation> Annotations = new List<IAnnotation>(16);
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
            Read();
        }

        /// <summary> Updates the annotation based on that last specified source object. </summary>
        public void Read()
        {
            if (source == null) return;
            foreach (var annotation in Annotations)
            {
                if (annotation is IOwnedAnnotation owned)
                    owned.Read(source);
            }
        }

        /// <summary> Writes the annotation data to the last specified source object. </summary>
        public void Write()
        {
            if (source != null)
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
        /// <param name="object"></param>
        /// <param name="member"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public static AnnotationCollection Create(object @object, IReflectionData member, params IAnnotation[] extraAnnotations)
        {
            var annotation = new AnnotationCollection();

            if (member is IMemberData mem)
            {
                var memberAnnotation = new MemberAnnotation(mem);
                annotation.Add(memberAnnotation, new MemberValueAnnotation(annotation, memberAnnotation));
            }
            annotation.AddRange(extraAnnotations);
            var resolver = new AnnotationResolver();
            resolver.Iterate(annotation);
            if (@object != null)
                annotation.Read(@object);
            return annotation;
        }

        object source;
        /// <summary> Additional annotations added to the current one. </summary>
        public IAnnotation[] ExtraAnnotations = Array.Empty<IAnnotation>();

        /// <summary>
        /// Annotates an object.
        /// </summary>
        /// <param name="object"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public static AnnotationCollection Annotate(object @object, params IAnnotation[] extraAnnotations)
        {
            var annotation = new AnnotationCollection { source = @object, ExtraAnnotations = extraAnnotations ?? Array.Empty<IAnnotation>() };
            annotation.AddRange(extraAnnotations);
            annotation.Add(new ObjectValueAnnotation(@object));
            var resolver = new AnnotationResolver();
            resolver.Iterate(annotation);
            annotation.Read(@object);
            return annotation;
        }

        /// <summary> Annotates a member of the object annotated by this. </summary>
        /// <param name="member"></param>
        /// <param name="Source"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public AnnotationCollection AnnotateMember(IMemberData member, object Source, params IAnnotation[] extraAnnotations)
        {
            var annotation = new AnnotationCollection { ParentAnnotation = this, source = Source, ExtraAnnotations = extraAnnotations ?? Array.Empty<IAnnotation>() };
            annotation.Add(new MemberAnnotation(member));
            annotation.AddRange(extraAnnotations);
            var resolver = new AnnotationResolver();
            resolver.Iterate(annotation);
            return annotation;
        }

        /// <summary> Annotates a member of the object annotated by this. </summary>
        /// <param name="member"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public AnnotationCollection AnnotateMember(IMemberData member, params IAnnotation[] extraAnnotations)
        {
            return AnnotateMember(member, null, extraAnnotations);
        }

        /// <summary> Annotates a sub-object of the object annotated by this. </summary>
        /// <param name="reflect"></param>
        /// <param name="obj"></param>
        /// <param name="extraAnnotations"></param>
        /// <returns></returns>
        public AnnotationCollection AnnotateSub(ITypeData reflect, object obj, params IAnnotation[] extraAnnotations)
        {
            var cache = Get<AnnotationCache>();
            if (cache?.GetCached(obj) is AnnotationCollection cached)
                return cached;
            
            var annotation = new AnnotationCollection { ParentAnnotation = this, ExtraAnnotations = extraAnnotations ?? Array.Empty<IAnnotation>() };

            annotation.AddRange(extraAnnotations);
            annotation.Add(new ObjectValueAnnotation(obj, reflect));

            var resolver = new AnnotationResolver();
            resolver.Iterate(annotation);
            if (obj != null)
                annotation.Read(obj);
            cache?.Register(annotation);
            return annotation;
        }

        /// <summary> Print the display name of this level of the annotation.</summary>
        internal string Name
        {
            get
            {
                var disp = Get<IDisplayAnnotation>()?.Name ?? Get<IReflectionAnnotation>()?.ReflectionInfo?.Name;
                return disp ?? "?";
            }
        }
        /// <summary> Creates a string from this. This is useful for debugging.</summary>
        /// <returns></returns>
        public override string ToString()
        {
            // the wanted format is: "Delay Step / DelaySecs: 1.0 s"
            StringBuilder sb = new StringBuilder();
            sb.Append(Name);
            sb.Append(": ");
            sb.Append(Get<IStringValueAnnotation>()?.Value ?? Get<IObjectValueAnnotation>()?.Value?.ToString() ?? "?");
            var p = ParentAnnotation;
            while (p != null)
            {
                sb.Insert(0, " / ");
                sb.Insert(0, p.Name);
                p = p.ParentAnnotation;
            }

            return sb.ToString();
        } 

        /// <summary> Insert an annotation at a location. </summary>
        /// <param name="index"></param>
        /// <param name="v"></param>
        public void Insert(int index, IAnnotation v)
        {
            Annotations.Insert(index, v);
        }
    }

    /// <summary> Helper methods for working with annotations. </summary>
    internal static class AnnotationExtensions
    {
        /// <summary> Recurse to find member annotation 'X.Y.Z'</summary>
        public static AnnotationCollection GetMember(this AnnotationCollection col, string name)
        {
            var name2 = name;
            foreach (var mem in col.Get<IMembersAnnotation>().Members)
            {
                var memberName = mem.Get<IMemberAnnotation>()?.Member.Name;
                var found = memberName == name2;
                if (found) return mem;
            }
            foreach (var mem in col.Get<IMembersAnnotation>().Members)
            {
                if (mem.Name == name)
                    return mem;
            }
            return null;
        }

        /// <summary>  helper method to get the icon annotation collection. Will return null if the item could not be found. </summary>
        public static AnnotationCollection GetIcon(this AnnotationCollection col, string iconName)
        {
            return col.Get<MenuAnnotation>()?.MenuItems
                .FirstOrDefault(c => c.Get<IIconAnnotation>()?.IconName == iconName);
        }
        public static void ExecuteIcon(this AnnotationCollection col, string iconName)
        {
            var icon = col.GetIcon(iconName);
            if (!icon.Get<IEnabledAnnotation>().IsEnabled == true)
                throw new Exception("Icon action is not enabled");
            icon.Get<IMethodAnnotation>().Invoke();
        }

        public static void SetValue(this AnnotationCollection col, object value)
        {
            // this function could be extended to support more types if needed.
            var strVal = col.Get<IStringValueAnnotation>();
            if (strVal != null)
            {
                strVal.Value = StringConvertProvider.GetString(value);
                col.Write();
            }
            else throw new Exception("SetValue failed.");

        }
    }
}
