﻿/*
 * HelperClasses.cs - part of CNC Controls library for Grbl
 *
 * v0.02 / 2019-10-31 / Io Engineering (Terje Io)
 *
 */

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Windows.Markup;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Diagnostics.Contracts;
using CNC.GCode;

namespace CNC.Core
{
    public class ViewModelBase : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly Dictionary<string, ICollection<string>> _validationErrors = new Dictionary<string, ICollection<string>>();

        public event PropertyChangedEventHandler PropertyChanged;

        //protected void OnPropertyChanged(string propertyName)
        //{
        //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        //}

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region INotifyDataErrorInfo members

        public void ClearErrors()
        {
            List<string> properties = new List<string>();

            foreach (var error in _validationErrors)
                if (!properties.Contains(error.Key))
                    properties.Add(error.Key);

            _validationErrors.Clear();

            foreach (var property in properties)
                if (!string.IsNullOrEmpty(property))
                    RaiseErrorsChanged(property);
        }
        public void SetError(string message)
        {
            _validationErrors.Add(string.Empty, new List<string> { message });
        }
        public void SetError(string property, string message)
        {
            _validationErrors.Add(property, new List<string> { message });

            RaiseErrorsChanged(property);
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
        private void RaiseErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || !_validationErrors.ContainsKey(propertyName))
                return null;

            return _validationErrors[propertyName];
        }

        public bool HasErrors
        {
            get { return _validationErrors.Count > 0; }
        }

        #endregion
    }

    public static class dbl
    {
        public static string ToInvariantString(this double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
        public static string ToInvariantString(this double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        public static bool Assign(double value, ref double holder)
        {
            bool changed;

            if ((changed = double.IsNaN(value) ? !double.IsNaN(holder) : holder != value))
                holder = value;

            return changed;
        }

        public static double[] ParseList(string s)
        {
            string[] v = s.Split(',');
            double[] values = new double[v.Length];

            for (int i = 0; i < v.Length; i++)
            {
                if (!double.TryParse(v[i], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i]))
                    values[i] = 0.0d;
            }

            return values;
        }

        public static double Parse(string value)
        {
            double result = double.NaN;

            if (value != null)
            {
                value = value.Trim();

                if (value.Length == 0 || !double.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
                    result = double.NaN;
            }

            return result;
        }
    }

    // by Nick : https://stackoverflow.com/questions/326802/how-can-you-two-way-bind-a-checkbox-to-an-individual-bit-of-a-flags-enumeration
    public class EnumFlags<T> : ViewModelBase where T : struct, IComparable, IFormattable, IConvertible
    {
        private T value;

        private int Foo<TEnum>(TEnum value) where TEnum : struct  // C# does not allow enum constraint
        {
            return (int)(ValueType)value;
        }

        public EnumFlags(T t)
        {
            if (!typeof(T).IsEnum) throw new ArgumentException($"{nameof(T)} must be an enum type"); // I really wish they would just let me add Enum to the generic type constraints
            value = t;
        }

        public T Value
        {
            get { return value; }
            set
            {
                if (!this.value.Equals(value))
                {
                    this.value = value;
                    OnPropertyChanged("Item[]");
                }
            }
        }

        [IndexerName("Item")]
        public bool this[T key]
        {
            get
            {
                // .net does not allow us to specify that T is an enum, so it thinks we can't cast T to int.
                // to get around this, cast it to object then cast that to int.
                return (((int)(object)value & (int)(object)key) == (int)(object)key);
            }
            set
            {
                if ((((int)(object)this.value & (int)(object)key) == (int)(object)key) == value) return;

                this.value = (T)(object)((int)(object)this.value ^ (int)(object)key);

                OnPropertyChanged("Item[]");
            }
        }
    }

    public static class FileUtils
    {
        public static bool IsAllowedFile (string filename, string extensions)
        {
            int pos = filename.LastIndexOf('.');

            return pos > 0 && ("," + extensions + ",").Contains("," + filename.Substring(pos + 1).ToLower() + ",");
        }
        public static string ExtensionsToFilter(string extensions)
        {
            string[] filetypes = extensions.Split(',');

            for (int i = 0; i < filetypes.Length; i++)
                filetypes[i] = "*." + filetypes[i];

            return string.Join(";", filetypes);
        }
    }

    // https://stackoverflow.com/questions/17794530/accessing-an-array-in-xaml-with-enums
    public static class StringEnumConversion
    {
        public static int ConvertToEnum<T>(object value)
        {
            Contract.Requires(typeof(T).IsEnum);
            Contract.Requires(value != null);
            Contract.Requires(Enum.IsDefined(typeof(T), value.ToString()));
            return (int)Enum.Parse(typeof(T), value.ToString());
        }
    }

    [ContentProperty("Parameters")]
    public class PathConstructor : MarkupExtension
    {
        public string Path { get; set; }
        public IList Parameters { get; set; }

        public PathConstructor()
        {
            Parameters = new List<object>();
        }

        public PathConstructor(string b, object p0)
        {
            Path = b;
            Parameters = new[] { p0 };
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
        //    return new PropertyPath(Path, Parameters.Cast<object>().ToArray());
            return new PropertyPath(String.Format("{0}[{1}]", Path, StringEnumConversion.ConvertToEnum<SpindleState>(Parameters[0])));
        }
    }

    public class Copy
    {
        public static void Properties<T>(T source, T target)
        {
            var type = typeof(T);
            foreach (var sourceProperty in type.GetProperties())
            {
                var targetProperty = type.GetProperty(sourceProperty.Name);
                targetProperty.SetValue(target, sourceProperty.GetValue(source, null), null);
            }
            //foreach (var sourceField in type.GetFields())
            //{
            //    var targetField = type.GetField(sourceField.Name);
            //    targetField.SetValue(target, sourceField.GetValue(source));
            //}
        }
    }
}