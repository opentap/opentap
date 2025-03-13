using System;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Class for generic cloning of values.
    /// This is done in multiple ways.
    /// - First check IStringValueConvert
    /// - Then check if its ICloneable
    /// - Finally check if its XML cloneable.
    /// </summary>
    class ObjectCloner
    {
        readonly object value;
        readonly ITypeData typeOfValue;

        readonly bool valueType;
        // the fields below are set up as needed. 
        bool? strConvertSuccess;
        TapSerializer serializer;
        string convertString;
        string xmlString;
        
        public ObjectCloner(object value)
        {
            this.value = value;
            typeOfValue = TypeData.GetTypeData(value);
            if (value == null || value is string || (typeOfValue.AsTypeData()?.IsValueType ?? false))
                valueType = true;
        }

        public bool CanClone(object context, ITypeData targetType = null)
        {
            return TryClone(context, targetType ?? typeOfValue, false, out object _);
        }
        
        public bool TryClone(object context, ITypeData targetType, bool skipIfPossible, out object clone)
        {
            if (ReferenceEquals(targetType, typeOfValue) && valueType || value == null)
            {
                clone = value;
                return true;
            }

            if (skipIfPossible == false || typeOfValue.DescendsTo(targetType) == false)
                // let's just set the value on the first property.
            {
                try
                {
                    if (strConvertSuccess == null)
                        strConvertSuccess = StringConvertProvider.TryGetString(value, out convertString);
                    if (strConvertSuccess == true)
                    {
                        clone = StringConvertProvider.FromString(convertString, targetType, context);
                        return true;
                    }

                    if (value is ICloneable cloneable)
                    {
                        clone = cloneable.Clone();
                        return true;
                    }

                    if (serializer == null)
                        serializer = new TapSerializer {IgnoreErrors = true}; // dont emit errors.

                    if (xmlString == null)
                        xmlString = serializer.SerializeToString(value);
                    if (xmlString != null)
                    {
                        clone = serializer.DeserializeFromString(xmlString, targetType);
                        if (serializer.Errors.Any())
                            return false;
                        return true;
                    }
                }
                catch
                {
                    // catch any error. this means cloning is not possible.
                    clone = null;
                    return false;
                }
            }else if(skipIfPossible && typeOfValue.DescendsTo(targetType))
            {
                clone = value;
                return true;
            }
            clone = value;
            return false;
        }

        public object Clone(bool skipIfPossible, object context, ITypeData targetType)
        {
            if (TryClone(context, targetType, skipIfPossible, out var clone) == false)
            {
                throw new InvalidCastException($"Failed cloning '{value}' as '{targetType.Name}'.");
            }
            return clone;
        }
    }
}