//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using System;
using System.Globalization;

namespace OpenTap.Plugins.BasicSteps
{

    /// <summary> Inputs that can be any numbers. </summary>
    public class NumberInput : Input<IConvertible>, IInputTypeRestriction
    {
        /// <summary> Implementation of input type restrictions. This returns true if its a number type. </summary>
        /// <param name="concreteType"></param>
        /// <returns></returns>
        public override bool SupportsType(ITypeData concreteType)
        {
            if (concreteType is TypeData cst)
            {
                if (!base.SupportsType(concreteType)) return false;
                if (cst.Type.IsEnum) return false;

                if (cst.Type.IsNumeric())
                    return true;
                else
                    return false;
            }
            return false;
        }
    }


    // TODO: deside if we want this step in (it is private for now)
    [Display("Limit Check", Group: "Basic Steps", Description: "Checks the input if its within the specified limit. If Upper Limit and Lower Limit has the same value it is the same as checking for equality.")]
    class LimitCheckStep: TestStep
    {
        [Display("Input Value", Order: 1, Description: "Value to be check within the limits.")]
        public NumberInput InputValue { get; set; }

        [Display("Upper Limit", Order: 2, Description: "Maximum limit to be check.")]
        public Enabled<string> UpperLimit { get; set; }

        [Display("Lower Limit", Order: 3, Description: "Minimum limit to be check.")]
        public Enabled<string> LowerLimit { get; set; }

        public LimitCheckStep()
        {
            LowerLimit = new Enabled<string> { Value = string.Empty };
            UpperLimit = new Enabled<string> { Value = string.Empty };
            string getError(Enabled<string> limit)
            {
                if (limit.IsEnabled)
                {
                    if (string.IsNullOrWhiteSpace(limit.Value))
                        return "A numeric value is required.";

                    try
                    {
                        var nf = GetNumberFormatter();
                        if (nf.ParseNumber(limit.Value, typeof(IComparable)) == null)
                            return "Unable to parse number.";
                    }
                    catch(Exception ex)
                    {
                        return ex.Message;
                    }
                }
                return null;
            }

            Rules.Add(() => getError(UpperLimit) == null, () => getError(UpperLimit), nameof(UpperLimit));
            Rules.Add(() => getError(LowerLimit) == null, () => getError(LowerLimit), nameof(LowerLimit));
            Rules.Add(() => (UpperLimit.IsEnabled || LowerLimit.IsEnabled), "At least one limit must be enabled and contains a value.", nameof(LowerLimit), nameof(UpperLimit));
            Rules.Add(() =>  InputValue?.Step != null && InputValue?.Property != null, "Input for limit check required.", nameof(InputValue));
            Rules.Add(() =>
            {
                var nf = GetNumberFormatter();
                IComparable parse(string val)
                {
                    return (IComparable)nf.ParseNumber(val, typeof(IComparable));
                }

                IComparable upperLimit = infinity;
                IComparable lowerLimit = negativeInfinity;

                try
                {
                    if (LowerLimit.IsEnabled && !string.IsNullOrWhiteSpace(LowerLimit.Value))
                        lowerLimit = parse(LowerLimit.Value);

                    if (UpperLimit.IsEnabled && !string.IsNullOrWhiteSpace(UpperLimit.Value))
                        upperLimit = parse(UpperLimit.Value);

                    return lowerLimit.CompareTo(upperLimit) <= 0;
                }
                catch
                {
                    return false;
                }
            }, "Lower limit must be less than upper limit.", nameof(LowerLimit), nameof(UpperLimit));
        }

        NumberFormatter GetNumberFormatter()
        {
            UnitAttribute unit = null;
            if (InputValue?.Property != null)
            {
                unit = InputValue.Property.GetAttribute<UnitAttribute>();
            }
            return new NumberFormatter(CultureInfo.CurrentCulture, unit);
        }

        static IComparable infinity,negativeInfinity;
        
        static LimitCheckStep()
        {
            var nf = new NumberFormatter(CultureInfo.CurrentCulture);
            IComparable parse(string val)
            {
                return (IComparable)nf.ParseNumber(val, typeof(IComparable));
            }
            
            infinity = parse("Infinity");
            negativeInfinity = parse("-Infinity");   
        }

        public override void Run()
        {
            var nf = GetNumberFormatter();
            IComparable parse(string val)
            {
                return (IComparable)nf.ParseNumber(val, typeof(IComparable));
            }

            var inputvalue = parse(InputValue.Value.ToString(CultureInfo.CurrentCulture));

            IComparable upperLimit = infinity;
            IComparable lowerLimit = negativeInfinity;

            if (LowerLimit.IsEnabled)
                lowerLimit = parse(LowerLimit.Value);
            
            if (UpperLimit.IsEnabled)
                upperLimit = parse(UpperLimit.Value);
            
            if(lowerLimit.CompareTo(upperLimit) > 0)
            {
                throw new InvalidOperationException("Lower limit must be bigger than upper limit");
            }

            var result = lowerLimit.CompareTo(inputvalue) <= 0 && upperLimit.CompareTo(inputvalue) >= 0;


            if (result)
            {
                UpgradeVerdict(Verdict.Pass);
            }
            else
            {
                UpgradeVerdict(Verdict.Fail);
            }
        }
    }
}
