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

    /// <summary> Specifies how available values proxies are implemented. This class should rarely be implemented. Consider implementing just IAvailableValuesAnnotation instead.</summary>
    public interface IAvailableValuesAnnotationProxy : IAnnotation
    {
        /// <summary> Annotated available values. </summary>
        IEnumerable<AnnotationCollection> AvailableValues { get; }
        /// <summary> Annotated selected value. Not this should belong to the set of AvailableValues as well.</summary>
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

    class MembersAnnotation : INamedMembersAnnotation, IMembersAnnotation, IOwnedAnnotation
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
            if (members.Count == 0)
            {
                members = new Dictionary<IMemberData, AnnotationCollection>(_members.Count());
            }

            var members2 = val.ReflectionInfo.GetMembers();
            foreach (var item in members2)
            {
                GetMember(item);
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
                doRead();
                if (string.IsNullOrWhiteSpace(error) == false)
                    return new[] { error };
                return Array.Empty<string>();
            }
        }

        private IDataErrorInfo source;
        void doRead()
        {
            var source = this.source;
            var mem = this.mem.Member;
            
            if (mem is EmbeddedMemberData m2)
            {   // Special case to add support for EmbeddedMemberData.
                if (source == null) return;
                source = m2.OwnerMember.GetValue(source) as IDataErrorInfo;
                mem = m2.InnerMember;
            }

            if (source == null) return;
            {
                try
                {
                    error = source[mem.Name];
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
                // set source to null to signal that errors has been read this time.
                this.source = null; 
            }
        }

        public void Read(object source)
        {
            this.source = source as IDataErrorInfo;
        }

        public void Write(object source)
        {

        }
    }


    class NumberAnnotation : IStringValueAnnotation, IErrorAnnotation
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

    class TimeSpanAnnotation : IStringValueAnnotation
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

    class BooleanValueAnnotation : IStringValueAnnotation
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
            set => annotation.Get<IObjectValueAnnotation>().Value = parseBool((string)value);
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
            foreach (var annotation in merged)
            {
                annotation.Write();
            }
        }
    }

    /// <summary>
    /// Marker interface that indicates that an IAnnotation does not support multi selecting. 
    /// When multiselecting, the UI should not show properties annotated with this. 
    /// </summary>
    // Used by ManyToOneAnnotation
    public interface IHideOnMultiSelectAnnotation : IAnnotation { }

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
                Dictionary<string, AnnotationCollection>[] dicts = mems.Select(x =>
                {
                    var dict = new Dictionary<string, AnnotationCollection>(x.Length);
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
    class DefaultValueAnnotation : IObjectValueAnnotation, IOwnedAnnotation, IErrorAnnotation
    {
        AnnotationCollection annotation;
        object currentValue;

        bool wasRead = false;
        bool wasSet = false;
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
                wasRead = true;
                currentValue = value;
            }
        }

        public DefaultValueAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }
        void read()
        {
            if (annotation.Source == null) return;
            var m = annotation.Get<IMemberAnnotation>();
            try
            {
                currentValue = m.Member.GetValue(annotation.Source);
            }
            catch (System.Reflection.TargetInvocationException)
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
            if (wasSet == false) return;
            var m = annotation.Get<IMemberAnnotation>();
            if (m.Member.Writable == false) return;
            error = null;
            try
            {
                if (object.Equals(currentValue, m.Member.GetValue(source)) == false)
                    m.Member.SetValue(source, currentValue);
            }
            catch (Exception _e)
            {
                error = _e.GetInnerMostExceptionMessage();
            }
        }

        string error = null;

        public IEnumerable<string> Errors => error == null ? Array.Empty<string>() : new[] { error };
    }
    class ObjectValueAnnotation : IObjectValueAnnotation, IReflectionAnnotation
    {
        public object Value { get; set; }

        public ITypeData ReflectionInfo { get; }

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


        class InputStepAnnotation : IAvailableValuesSelectedAnnotation, IOwnedAnnotation
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
                    if (context is IEnumerable enumerable_context)
                    {
                        step = enumerable_context.OfType<ITestStep>().FirstOrDefault();
                    }
                    if (step == null) return Enumerable.Empty<object>();
                    while (step.Parent != null)
                        step = step.Parent;

                    var steps = Utils.FlattenHeirarchy(step.ChildTestSteps, x => x.ChildTestSteps);

                    List<InputThing> accepted = new List<InputThing>();
                    accepted.Add(new InputThing() { });
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

            IInput getInput() => annotation.GetAll<IObjectValueAnnotation>().FirstOrDefault(x => x != this && x.Value is IInput)?.Value as IInput;

            public void Read(object source)
            {
                setValue = null;
            }

            public void Write(object source)
            {
                if (setValue is InputThing v)
                {
                    var inp = getInput();
                    if (inp != null)
                    {
                        inp.Step = v.Step;
                        inp.Property = v.Member;
                    }
                }
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
            public InputStepAnnotation(AnnotationCollection annotation)
            {
                this.annotation = annotation;
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
                        bool isBrowsable(Enum e)
                        {
                            var mem = enumType.GetMember(e.ToString()).FirstOrDefault();
                            if (mem != null)
                                return mem.IsBrowsable();
                            return true;
                        }

                        availableValues = Enum.GetValues(enumType).Cast<Enum>().Where(isBrowsable).ToArray();
                    }
                    return availableValues;
                }
            }


            Type enumType;
            AnnotationCollection a;
            public EnumValuesAnnotation(Type enumType, AnnotationCollection a)
            {
                this.enumType = enumType;
                this.a = a;
            }
        }

        class EnumStringAnnotation : IStringValueAnnotation, IValueDescriptionAnnotation
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
            
            string enumToDescription(Enum value)
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
                return mem.GetDisplayAttribute().Description;
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
                    if (newvalue == null)
                    {
                        newvalue = values.FirstOrDefault(x => enumToString(x).ToLower() == value.ToLower());
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
                    return enumToDescription(e);

                return null;
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

            IObjectValueAnnotation val => annotation.Get<IObjectValueAnnotation>();
            Type enumType;
            AnnotationCollection annotation;

            public FlagEnumAnnotation(AnnotationCollection annotation, Type enumType)
            {
                this.annotation = annotation;
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
                    ITestStep value;
                    if (member)
                    {
                        value = (ITestStep) annotation.ParentAnnotation.Get<IObjectValueAnnotation>().Value;
                    }
                    else
                    {
                        value = (ITestStep)annotation.Get<IObjectValueAnnotation>().Value;
                    }

                    return value.GetFormattedName();
                }
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
                        lst2.Clear();
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
                                    var item = lst2[i3];
                                    lst2.RemoveAt(i3);
                                    break;
                                }
                            }
                            lst2.Insert(i2, values[i1]);
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
                public bool IsReadOnly => (parentAnnotation.Get<IObjectValueAnnotation>().Value as IEnabled)?.IsEnabled == false;

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
                    if (annotation.Get<DirectoryPathAttribute>() is DirectoryPathAttribute d)
                        extra.Add(d);
                    if (annotation.Get<FilePathAttribute>() is FilePathAttribute f)
                        extra.Add(f);
                    if (annotation.Get<ISuggestedValuesAnnotation>() is ISuggestedValuesAnnotation s)
                        extra.Add(s);

                    extra.Add(new EnabledAccessAnnotation(annotation));
                    var src = annotation.Get<IObjectValueAnnotation>().Value;
                    var newValueMember = annotation.AnnotateMember(valueMember.Get<IMemberAnnotation>().Member, src, extra.ToArray());

                    if (src != null)
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
                    foreach (var member in Members)
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

        class TestStepSelectAnnotation : IAvailableValuesAnnotation, IStringValueAnnotation
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
                    if (member.TypeDescriptor is TypeData cst)
                    {
                        var currentType = TypeData.GetTypeData(currentValue);
                        List<object> selection = new List<object>();
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

            if (mem != null)
            {
                if (annotation.Get<IObjectValueAnnotation>() == null)
                    annotation.Add(new DefaultValueAnnotation(annotation));

                annotation.Add(mem.Member.GetDisplayAttribute());

                if (mem.Member.GetAttribute<SuggestedValuesAttribute>() is SuggestedValuesAttribute suggested)
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
                if (mem.Member.GetAttribute<FilePathAttribute>() is FilePathAttribute fp)
                    annotation.Add(fp);
                if (mem.Member.GetAttribute<DirectoryPathAttribute>() is DirectoryPathAttribute dp)
                    annotation.Add(dp);
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
                if (mem.Member.DeclaringType.DescendsTo(typeof(IValidatingObject)))
                {
                    annotation.Add(new ValidationErrorAnnotation(mem));
                }

                if (mem.Member.HasAttribute<EnabledIfAttribute>())
                {
                    annotation.Add(new EnabledIfAnnotation(mem));
                }
                if (mem.Member.Writable == false)
                {
                    annotation.Add(new ReadOnlyMemberAnnotation());
                }
            }

            if (reflect?.ReflectionInfo is TypeData csharpType)
            {
                var type = csharpType.Load();
                bool isNullable = type.IsPrimitive == false && type.IsGenericType && type.IsValueType && type.DescendsTo(typeof(Nullable<>));
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
                            annotation.Add(new GenericSequenceAnnotation(annotation));
                            if (innerType.DescendsTo(typeof(IResource)))
                            {
                                annotation.Add(new ResourceAnnotation(annotation, innerType.Type));
                                annotation.Add(new MultiResourceSelector(annotation, innerType.Type));
                            }
                            else if (innerType.DescendsTo(typeof(ITestStep)))
                            {
                                annotation.Add(new TestStepSelectAnnotation(annotation));
                                annotation.Add(new TestStepMultiSelectAnnotation(annotation));
                            }
                            else if (innerType.DescendsTo(typeof(ViaPoint)))
                                annotation.Add(new ViaPointAnnotation(annotation));
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
                            annotation.Add(new EnumValuesAnnotation(type, annotation));
                            annotation.Add(new EnumStringAnnotation(type, annotation));

                            if (csharpType.HasFlags())
                            {
                                annotation.Add(new FlagEnumAnnotation(annotation, type));
                            }
                        }
                    }

                    if (type.IsValueType == false && type.DescendsTo(typeof(IResource)))
                        annotation.Add(new ResourceAnnotation(annotation, type));
                    else if (type.IsValueType == false && type.DescendsTo(typeof(ITestStep)))
                        annotation.Add(new TestStepSelectAnnotation(annotation));
                }
            }

            if (mem != null)
            {
                if (mem.Member.GetAttribute<AvailableValuesAttribute>() is AvailableValuesAttribute avail)
                {
                    if (mem.Member.TypeDescriptor.DescendsTo(typeof(IEnumerable<>)) && mem.Member.TypeDescriptor.IsA(typeof(string)) == false)
                    {
                        annotation.Add(new MultipleAvailableValuesAnnotation(annotation, avail.PropertyName));
                    }
                    else
                    {
                        annotation.Add(new AvailableValuesAnnotation(annotation, avail.PropertyName));
                    }
                }
                
                if (mem.Member.TypeDescriptor.DescendsTo(typeof(IInput)))
                {
                    annotation.Add(new InputStepAnnotation(annotation));
                }
            }

                if (reflect?.ReflectionInfo is ITypeData tp)
            {
                if (tp.DescendsTo(typeof(ITestStep)))
                    annotation.Add(new StepNameStringValue(annotation, member: mem != null && mem.ReflectionInfo == tp));
                
                bool csharpPrimitive = tp is TypeData cst && (cst.Type.IsPrimitive || cst.Type == typeof(string));
                if (tp.GetMembers().Any() && !csharpPrimitive)
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


                        if (a?.Get<MergedValueAnnotation>() is MergedValueAnnotation merged)
                        {
                            // Merged available value fields are the common of all the original available values.
                            HashSet<object> values2 = null;
                            foreach (var m in merged.Merged)
                            {
                                var e = (m.Get<IAvailableValuesAnnotation>()?.AvailableValues as IEnumerable)?.Cast<object>();
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

                    return a.AnnotateSub(TypeData.GetTypeData(current), current, new ReadOnlyMemberAnnotation(), new AvailableMemberAnnotation(a));
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

            public void Read(object source)
            {
                if (annotations == null) return;
                var values = a.Get<IAvailableValuesAnnotation>()?.AvailableValues;
                if (Enumerable.SequenceEqual(prevValues.Cast<object>(), values.Cast<object>()) == false)
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
                doRead();
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
            var members = annotation.Get<IMembersAnnotation>();

            if (members != null && merged != null)
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
            foreach (var annotation in Annotations)
            {
                if (annotation is IOwnedAnnotation owned)
                    owned.Read(source);
            }
        }

        /// <summary> Updates the annotation based on that last specified source object. </summary>
        public void Read()
        {
            if (source != null)
                Read(source);
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

    /// <summary> Helper methods for working with annotations. </summary>
    internal static class AnnotationExtensions
    {
        /// <summary> Recurse to find member annotation 'X.Y.Z'</summary>
        public static AnnotationCollection GetMember(this AnnotationCollection col, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return col;
            int index = name.IndexOf('.');
            string rest = null;
            if (index == -1)
                index = name.Length;
            else
                rest = name.Substring(index + 1);
            var name2 = name.Substring(0, index);
            
            var sub = col.Get<IMembersAnnotation>().Members.FirstOrDefault(x => x.Get<IMemberAnnotation>()?.Member.Name == name2);
            return sub?.GetMember(rest);
        }
    }
}
