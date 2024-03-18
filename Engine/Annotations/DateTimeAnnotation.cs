using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenTap
{
    class DateTimeAnnotation : IStringValueAnnotation, ICopyStringValueAnnotation, IErrorAnnotation
    {
        private readonly AnnotationCollection annotation;
        private string currentError = null;
        public string Value
        {
            get
            {
                // date time format similar to what is being produces by logs (without ms).
                // The default invariant culture is MM/dd/yyyy ... which we dont use anywhere else.
                var dateTimeFormat = "yyyy-MM-dd HH:mm:ss";
                if (annotation.Get<IObjectValueAnnotation>(from: this).Value is DateTime dt)
                    
                    return dt.ToString( dateTimeFormat,CultureInfo.InvariantCulture);
                return "";
            }
            set
            {
                try
                {
                    annotation.Get<IObjectValueAnnotation>(from: this).Value = DateTime.Parse(value, DateTimeFormatInfo.InvariantInfo);
                    currentError = null;
                }
                catch (Exception ex)
                {
                    currentError = ex.Message;
                }
            }
        }

        public DateTimeAnnotation(AnnotationCollection annotation)
        {
            this.annotation = annotation;
        }

        public IEnumerable<string> Errors => currentError == null ? Array.Empty<string>() : new[] { currentError };
    }
}