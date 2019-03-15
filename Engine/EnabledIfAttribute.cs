//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Reflection;

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

        private static readonly TraceSource log = Log.CreateSource("EnabledIf");

        /// <summary>
        /// Name of the property to enable. Must exactly match a name of a property in the current class. 
        /// </summary>
        public string PropertyName { get; private set; }
        /// <summary>
        /// Value(s) the property must have for the item to be valid/enabled. If multiple values are specified, the item is enabled if just one value is equal. 
        /// If no values are specified, 'true' is the assumed value.
        /// </summary>
        public IComparable[] PropertyValues { get; private set; }

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
                PropertyValues = new IComparable[1] { true };
            else
                PropertyValues = propertyValues.Cast<IComparable>().ToArray();
        }

        /// <summary>
        /// Returns tru if a member is enabled.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="instance"></param>
        /// <param name="dependentProp"></param>
        /// <param name="dependentValue"></param>
        /// <param name="hidden"></param>
        /// <returns></returns>
        public static bool IsEnabled(IMemberData property, object instance,
            out IMemberData dependentProp, out IComparable dependentValue, out bool hidden)
        {
            if (property == null)
                throw new ArgumentNullException("property");
            if (instance == null)
                throw new ArgumentNullException("instance");
            ITypeData instanceType = TypeData.GetTypeData(instance);
            var dependencyAttrs = property.GetAttributes<EnabledIfAttribute>();
            dependentProp = null;
            dependentValue = 0;
            hidden = false;
            bool enabled = true;
            foreach (var at in dependencyAttrs)
            {
                bool newEnabled = true;
                dependentProp = instanceType.GetMember(at.PropertyName);

                if (dependentProp == null)
                {
                    // We cannot be sure that the step developer has used this attribute correctly
                    // (could just be a typo in the (weakly typed) property name), thus we need to 
                    // provide a good error message that leads the developer to where the error is.
                    log.Warning("Could not find property '{0}' on '{1}'. EnabledIfAttribute can only refer to properties of the same class as the property it is decorating.", at.PropertyName, instanceType.Name);
                    enabled = false;
                    return false;
                }

                var depValue = dependentProp.GetValue(instance);
                dependentValue = depValue as IComparable;
                try
                {
                    if (depValue is IEnabled)
                    {
                        var isEnabled = ((IEnabled)depValue).IsEnabled;
                        if (!at.PropertyValues.Any(testValue => testValue.CompareTo(isEnabled) == 0))
                        {
                            newEnabled = false;
                        }
                    }
                    else if (!at.PropertyValues.Any(testValue => testValue.CompareTo(depValue) == 0) && dependentValue != null)
                    {
                        newEnabled = false;

                    }
                }
                catch (ArgumentException)
                {
                    // CompareTo throws ArgumentException when obj is not the same type as this instance.
                    newEnabled = false;
                }
                if (!newEnabled && at.HideIfDisabled)
                    hidden = true;
                enabled &= newEnabled;
            }
            return enabled;
        }


            /// <summary>
            /// Checks whether a given property is enabled according to the <see cref="EnabledIfAttribute"/>.
            /// </summary>
            /// <param name="at">The attribute enabling this property.</param>
            /// <param name="instance">Instance of the object that has 'property'.</param>
            /// <param name="property">The property to be checked.</param>
            /// <returns>true if property dependent property has the correct value.</returns>
            internal static bool IsEnabled(MemberInfo property, EnabledIfAttribute at, object instance)
        {
            PropertyInfo depedentProp = instance.GetType().GetProperty(at.PropertyName);
            if (depedentProp == null)
            {
                // We cannot be sure that the step developer has used this attribute correctly
                // (could just be a typo in the (weakly typed) property name), thus we need to 
                // provide a good error message that leads the developer to where the error is.
                log.Warning("Could not find property '{0}' on '{1}'. EnabledIfAttribute can only refer to properties of the same class as the property it is decorating.", at.PropertyName, instance.GetType().Name);
                return false;
            }

            var depValue = depedentProp.GetValue(instance, null);
            IComparable dependentValue = depValue as IComparable;
            try
            {
                if (depValue is IEnabled)
                {
                    var isEnabled = ((IEnabled)depValue).IsEnabled;
                    if (!at.PropertyValues.Any(testValue => testValue.CompareTo(isEnabled) == 0))
                        return false;
                }
                else if (!at.PropertyValues.Any(testValue => testValue.CompareTo(depValue) == 0) && dependentValue != null)
                {
                    return false;
                }
            }
            catch(ArgumentException)
            {
                // CompareTo throws ArgumentException when obj is not the same type as this instance.
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks whether a given property is enabled according to the <see cref="EnabledIfAttribute"/>.
        /// </summary>
        /// <param name="instance">Instance that has property.</param>
        /// <param name="property">Property to be checked for if it is enabled.</param>
        /// <returns>True if property is enabled.</returns>
        public static bool IsEnabled(IMemberData property, object instance)
        {
            IMemberData depedentProp;
            IComparable dependentValue;
            return IsEnabled(property, instance, out depedentProp, out dependentValue, out bool hidden);
        }
    }
}
