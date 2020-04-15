//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace OpenTap
{
    /// <remarks>
    /// Only works on properties, not fields. The functionality is implemented primarily with a GUI in mind.
    /// </remarks>
    /// <summary>
    /// Runtime property value error-checking system. 
    /// Can be used to validate an object before run or to display messages through a GUI.
    /// </summary>
    public abstract class ValidatingObject : IDataErrorInfo, INotifyPropertyChanged, IValidatingObject
    {
        /// <summary>
        /// All the validation rules. Add new rules to this in order to get runtime value validation.
        /// </summary>
        [AnnotationIgnore]
        public ValidationRuleCollection Rules
        {
            get
            {
                if (_Rules == null) _Rules = new ValidationRuleCollection();
                return _Rules;
            }
        }
        private ValidationRuleCollection _Rules;

        // thread static to avoid locking everything and having a HashSet on each ValidationObject
        [ThreadStatic]
        static HashSet<object> traversed = null;
        string getError(string propertyName = null)
        {
            List<string> errors = null;
            void pushError(string error)
            {
                if (errors == null) errors = new List<string>();
                if (!errors.Contains(error))
                    errors.Add(error);
            }

            foreach (var rule in Rules)
            {
                try
                {
                    if (propertyName != null && rule.PropertyName != propertyName) continue;
                    if (rule.IsValid()) continue;
                    var error = rule.ErrorMessage;
                    if (string.IsNullOrEmpty(error)) continue;
                    pushError(error);
                }catch(Exception ex)
                {
                    pushError(ex.Message);
                }
            }
            foreach (var fwd in Rules.ForwardedRules)
            {
                if (propertyName != null && fwd.Name != propertyName) continue;
                var obj = fwd.GetValue(this) as IValidatingObject;
                if (obj == null) continue;
                if (traversed == null)
                {
                    traversed = new HashSet<object>();
                }
                else if (traversed.Contains(obj)) continue;
                traversed.Add(obj);
                traversed.Add(this);

                try
                {
                    var err = obj.Error;
                    
                    if (string.IsNullOrWhiteSpace(err)) continue;
                    err = err.TrimEnd();

                    if (errors == null) errors = new List<string>();
                    if (!errors.Contains(err))
                        errors.Add(err);
                }
                finally
                {
                    traversed.Remove(obj);
                }
            }
            if (errors == null)
                return "";
            return string.Join(Environment.NewLine, errors).TrimEnd();
        }

        /// <summary>
        /// Gets the error messages for each invalid rule and joins them with a newline.
        /// </summary>
        public string Error => getError(null);

        /// <summary>
        /// Gets the error(s) for a given property as a concatenated string.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns>string concatenated errors.</returns>
        string IDataErrorInfo.this[string propertyName] => getError(propertyName);

        /// <summary>
        /// Checks all validation rules on this object (<see cref="Rules"/>) and throws an AggregateException on errors.
        /// </summary>
        /// <param name="ignoreDisabledProperties">If true, ignores <see cref="Rules"/> related to properties that are disabled or hidden as a result of <see cref="EnabledIfAttribute"/> or <see cref="Enabled{T}"/>.</param>
        /// <exception cref="AggregateException">Thrown when any <see cref="Rules"/> on this object are invalid. This exception contains an ArgumentException for each invalid setting.</exception>
        protected void ThrowOnValidationError(bool ignoreDisabledProperties)
        {
            List<Exception> errors = new List<Exception>();
            List<string> propertyNames = TestStepExtensions.GetObjectSettings(new object[] { this }, ignoreDisabledProperties, (object o, PropertyInfo pi) => pi.Name);
            foreach (ValidationRule rule in Rules)
            {
                if (!rule.IsValid() && propertyNames.Contains(rule.PropertyName))
                {
                    throw new Exception($"The Property [{rule.PropertyName}] is invalid. Details: {rule.ErrorMessage}");
                }
            }
        }

        #region OnPropertyChanged
        /// <summary>
        /// Standard PropertyChanged event object.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        
        /// <summary>
        /// Triggers the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">string name of which property has been changed.</param>
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            UserInput.NotifyChanged(this, propertyName);
        }

        #endregion
    }
    /// <summary>
    /// Delegate for checking validation rule.
    /// </summary>
    /// <returns>True if valid, false if not.</returns>
    public delegate bool IsValidDelegateDefinition();
    /// <summary>
    /// Delegate for returning a custom error message from a validation rule.
    /// </summary>
    /// <returns></returns>
    public delegate string CustomErrorDelegateDefinition();

    /// <summary>
    /// Validates settings at runtime. A validation rule is attached to an object of type <see cref="ValidatingObject"/> and is used to validate the property value of that object. 
    /// Also see <see cref="ValidatingObject.Rules"/>
    /// </summary>
    public class ValidationRule
    {
        /// <summary>
        /// Name of the property affected by this rule.
        /// </summary>
        public string PropertyName { get; set; }
        /// <summary>
        /// Error message to use if the property does not follow the rule.  
        /// </summary>
        public virtual string ErrorMessage { get; set; }

        /// <summary>
        /// Rule function following the signature () -> bool.  
        /// </summary>
        public IsValidDelegateDefinition IsValid { get; set; }

        /// <summary>
        /// </summary>
        /// <param name="isValid">Property IsValid</param>
        /// <param name="errorMessage">Property ErrorMessage-</param>
        /// <param name="propertyName">Property PropertyName</param>
        public ValidationRule(IsValidDelegateDefinition isValid, string errorMessage, string propertyName)
        {
            ErrorMessage = errorMessage;
            PropertyName = propertyName;
            IsValid = isValid;
        }
    }

    /// <summary>
    /// Validation rule that takes a delegate as an argument.  Used for writing error messages.
    /// </summary>
    public class DelegateValidationRule : ValidationRule
    {
        readonly static TraceSource log =  OpenTap.Log.CreateSource("Validation");
        /// <summary>
        /// The error calculated from ErrorDelegate.
        /// </summary>
        public override string ErrorMessage
        {
            get
            {
                try
                {
                    return ErrorDelegate();
                }
                catch (Exception e)
                {
                    log.Error("Exception caught from error handling function.");
                    log.Debug(e);
                    return "Exception caught from error handling function.";
                }
            }
            set
            {

            }
        }

        /// <summary>
        /// The delegate producing the error message.
        /// </summary>
        public CustomErrorDelegateDefinition ErrorDelegate;

        /// <summary>
        /// Constructor for DelegateValidationRule.
        /// </summary>
        /// <param name="isValid">Validation delegate.</param>
        /// <param name="propertyName">Target property.</param>
        /// <param name="errorDelegate">Function creating the error message.</param>
        public DelegateValidationRule(IsValidDelegateDefinition isValid, string propertyName, CustomErrorDelegateDefinition errorDelegate) : base(isValid, "", propertyName)
        {
            ErrorDelegate = errorDelegate;
        }

    }
    /// <summary>
    /// Collection of validation rules.
    /// Simplifies adding new rules by abstracting the use of ValidationRule objects.
    /// </summary>
    public class ValidationRuleCollection : Collection<ValidationRule>
    {
        /// <summary>
        /// Add a new rule to the collection.
        /// </summary>
        /// <param name="isValid">Rule checking function.</param>
        /// <param name="errorMessage"> Error if rule checking function returns false.</param>
        /// <param name="propertyName">Name of the property it affects.</param>
        public void Add(IsValidDelegateDefinition isValid, string errorMessage, string propertyName)
        {
            this.Add(new ValidationRule(isValid, errorMessage, propertyName));
        }

        internal readonly List<IMemberData> ForwardedRules = new List<IMemberData>(); 

        /// <summary>
        /// Dynamically adds a sub-objects rules to the collection of rules.
        /// </summary>
        /// <param name="member"></param>
        internal void Forward(IMemberData member) => ForwardedRules.Add(member);
        
        /// <summary>
        /// Adds a new rule to the collection.
        /// </summary>
        /// <param name="isValid"></param>
        /// <param name="errorDelegate"></param>
        /// <param name="propertyName"></param>
        public void Add(IsValidDelegateDefinition isValid, CustomErrorDelegateDefinition errorDelegate, string propertyName)
        {
            this.Add(new DelegateValidationRule(isValid, propertyName, errorDelegate));
        }

        /// <summary>
        /// Add a new rule to the collection for multiple properties. 
        /// Internally a new rule is created for each property.
        /// </summary>
        /// <param name="isValid">Rule checking function.</param>
        /// <param name="errorMessage"> Error if rule checking function returns false.</param>
        /// <param name="propertyNames">Names of the properties it affects.</param>
        public void Add(IsValidDelegateDefinition isValid, string errorMessage, params string[] propertyNames)
        {
            if (propertyNames == null)
                throw new ArgumentNullException("propertyNames");
            foreach (string propertyName in propertyNames)
            {
                this.Add(isValid, errorMessage, propertyName);
            }
        }

        /// <summary>
        /// Adds a new rule to the collection.
        /// </summary>
        /// <param name="isValid"></param>
        /// <param name="errorDelegate"></param>
        /// <param name="propertyNames">Names of the properties it affects.</param>
        public void Add(IsValidDelegateDefinition isValid, CustomErrorDelegateDefinition errorDelegate, params string[] propertyNames)
        {
            if (propertyNames == null)
                throw new ArgumentNullException("propertyNames");
            foreach (string propertyName in propertyNames)
            {
                this.Add(isValid, errorDelegate, propertyName);
            }
        }
    }
}
