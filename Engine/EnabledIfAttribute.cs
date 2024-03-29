//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Identifies settings, properties, or methods that should only be valid/enabled when another property or setting has a certain value. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class EnabledIfAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets if the property should be hidden when disabled.
        /// </summary>
        public bool HideIfDisabled { get; set; }

        /// <summary> Gets or sets if the enabling value is individual flags from an enum. </summary>
        public bool Flags { get; set; }
        
        /// <summary>  Gets or sets if the value should enable or disable(inverted) the setting. </summary>
        public bool Invert { get; set; }

        static readonly TraceSource log = Log.CreateSource("EnabledIf");

        /// <summary>
        /// Name of the property to enable. Must exactly match a name of a property in the current class. 
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Value(s) the property must have for the item to be valid/enabled. If multiple values are specified, the item is enabled if just one value is equal. 
        /// If no values are specified, 'true' is the assumed value.
        /// </summary>
        // note, IComparable is not for equality comparing. It is meant for sorting, hence it does not really matter here.
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [Obsolete("Use Values instead.")]
        public IComparable[] PropertyValues => Values.OfType<IComparable>().ToArray();
        
        /// <summary>
        /// Value(s) the property must have for the item to be valid/enabled. If multiple values are specified, the item is enabled if just one value is equal. 
        /// If no values are specified, 'true' is the assumed value.
        /// </summary>
        public object[] Values { get; }
        /// <summary>
        /// Identifies settings, properties, or methods that are only valid/enabled when another property or setting has a certain value. 
        /// </summary>
        /// <param name="propertyName">Name of the property to enable. Must exactly match a name of a property in the current class. </param>
        /// <param name="propertyValues">Value(s) the property must have for the item to be valid/enabled. If multiple values are specified, the item is enabled if just one value is equal. 
        /// If no values are specified, 'true' is the assumed value.</param>
        public EnabledIfAttribute(string propertyName, params object[] propertyValues)
        {
            PropertyName = propertyName;
            if ((propertyValues == null) || (propertyValues.Length <= 0))
                Values = new object[] {true};
            else
                Values = propertyValues;
        }

        internal virtual (bool enabled, bool hidden) IsEnabled(object instance, ITypeData instanceType, out IMemberData dependentProp)
        {
            bool newEnabled = true;
            dependentProp = instanceType.GetMember(PropertyName);

            if (dependentProp == null)
            {
                // We cannot be sure that the step developer has used this attribute correctly
                // (could just be a typo in the (weakly typed) property name), thus we need to 
                // provide a good error message that leads the developer to where the error is.
                log.Warning("Could not find property '{0}' on '{1}'. EnabledIfAttribute can only refer to properties of the same class as the property it is decorating.", PropertyName, instanceType.Name);
                return (false, false);
            }

            var depValue = dependentProp.GetValue(instance);
                
            try
            {
                newEnabled = calcEnabled(this, depValue);
            }
            catch (ArgumentException)
            {
                // CompareTo throws ArgumentException when obj is not the same type as this instance.
                newEnabled = false;
            }
            bool hidden = false;
            if (HideIfDisabled)
                hidden = !newEnabled;
            return (newEnabled, hidden);
        }

        /// <summary> Returns true if a member is enabled. </summary>
        public static bool IsEnabled(IMemberData property, object instance,
            out IMemberData dependentProp, out IComparable dependentValue, out bool hidden)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            ITypeData instanceType = TypeData.GetTypeData(instance);
            var dependencyAttrs = property.GetAttributes<EnabledIfAttribute>();
            dependentProp = null;
            dependentValue = 0;
            hidden = false;
            bool enabled = true;
            foreach (var at in dependencyAttrs)
            {
                var (enabled2, hidden2) = at.IsEnabled(instance, instanceType, out dependentProp);
                
                enabled &= enabled2;
                if (hidden2)
                    hidden = true;
            }
            return enabled;
        }

        static bool calcEnabled(EnabledIfAttribute at, object value)
        {
            if(at.Invert)
                return !calcEnabled2(at, value);
            return calcEnabled2(at, value);
        }

        /// <summary> Calculate if an enabled if is enabled by a given value. </summary>
        static bool calcEnabled2(EnabledIfAttribute at, object depValue)
        {
            if (at.Flags)
            {
                int value = (int)Convert.ChangeType(depValue, TypeCode.Int32);
                foreach (var flag in at.Values)
                {
                    int flagCode = (int) Convert.ChangeType(flag, TypeCode.Int32);
                    if ((value & flagCode) != 0)
                        return true;
                }
                return false;
            }

            if (depValue is IEnabled e)
                depValue = e.IsEnabled;

            foreach (var val in at.Values)
            {
                if (Equals(val, depValue))
                    return true;
            }
            
            return false;
        }        

        /// <summary>
        /// Checks whether a given property is enabled according to the <see cref="EnabledIfAttribute"/>.
        /// </summary>
        /// <param name="at">The attribute enabling this property.</param>
        /// <param name="instance">Instance of the object that has 'property'.</param>
        /// <returns>true if property dependent property has the correct value.</returns>
        internal static bool IsEnabled(EnabledIfAttribute at, object instance)
        {
            IMemberData dependentProp = TypeData.GetTypeData(instance).GetMember(at.PropertyName);
            if (dependentProp == null)
            {
                // We cannot be sure that the step developer has used this attribute correctly
                // (could just be a typo in the (weakly typed) property name), thus we need to 
                // provide a good error message that leads the developer to where the error is.
                log.Warning(
                    $"Could not find property '{at.PropertyName}' on '{instance.GetType().Name}'. " +
                    $"EnabledIfAttribute can only refer to properties of the same class as the property it is decorating.");
                return false;
            }

            var depValue = dependentProp.GetValue(instance);
            try
            {
                return calcEnabled(at, depValue);
            }
            catch (ArgumentException)
            {
                // CompareTo throws ArgumentException when obj is not the same type as this instance.
                return false;
            }
        }


        /// <summary>
        /// Checks whether a given property is enabled according to the <see cref="EnabledIfAttribute"/>.
        /// </summary>
        /// <param name="instance">Instance that has property.</param>
        /// <param name="property">Property to be checked for if it is enabled.</param>
        /// <returns>True if property is enabled.</returns>
        public static bool IsEnabled(IMemberData property, object instance)
        {
            return IsEnabled(property, instance, out _, out _, out _);
        }
    }
}
