using System;

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
        readonly ITypeData valueType;
        // the fields below are set up as needed. 
        bool? strConvertSuccess;
        TapSerializer serializer;
        string convertString;
        string xmlString;
        
        public ObjectCloner(object value)
        {
            this.value = value;
            valueType = TypeData.GetTypeData(value);
        }

        public bool CanClone(object context, ITypeData targetType = null)
        {
            return TryClone(context, targetType ?? valueType, false, out object _);
        }
        
        public bool TryClone(object context, ITypeData targetType, bool skipIfPossible, out object clone )
        {
            if (skipIfPossible == false || valueType.DescendsTo(targetType) == false)
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
                        serializer = new TapSerializer();

                    if (xmlString == null)
                        xmlString = serializer.SerializeToString(value);
                    if (xmlString != null)
                    {
                        clone = serializer.DeserializeFromString(xmlString);
                        return true;
                    }
                }
                catch
                {
                }
            }

            clone = value;
            return false;
        }

        public object Clone(bool skipIfPossible, object context, ITypeData targetType)
        {
            TryClone(context, targetType, skipIfPossible, out object clone);
            return clone;
        }
    }
}