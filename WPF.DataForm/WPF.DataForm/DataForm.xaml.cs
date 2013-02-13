﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Collections.ObjectModel;
using System.Windows.Media;

using WPF.DataForm;

using Xceed.Wpf.Toolkit;

namespace System.Windows.Controls
{
    /// <summary>
    /// Logique d'interaction pour DataForm.xaml
    /// </summary>
    public partial class DataForm : UserControl, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Membres

        public event PropertyChangedEventHandler PropertyChanged;

        private void FirePropertyChanged<TProp>(Expression<Func<DataForm, TProp>> propertySelector)
        {
            if (this.PropertyChanged == null)
            {
                return;
            }

            var memberExpression = propertySelector.Body as MemberExpression;

            if (memberExpression == null ||
                memberExpression.Member.MemberType != MemberTypes.Property)
            {
                var msg = string.Format("{0} is an invalid property selector.", propertySelector);
                throw new Exception(msg);
            }

            this.PropertyChanged(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
        }
        #endregion


        #region CurrentItem
        public object CurrentItem
        {
            get { return (object)GetValue(CurrentItemProperty); }
            set { SetValue(CurrentItemProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CurrentItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentItemProperty =
            DependencyProperty.Register("CurrentItem", typeof(object), typeof(DataForm), new UIPropertyMetadata(null, new PropertyChangedCallback(CurrentItemValueChanged)));

        private static void CurrentItemValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            DataForm df = sender as DataForm;
            if (df != null)
                df.CurrentItemChanged();
        }
        #endregion

        #region LabelSeparator
        private string m_labelSeparator = ":";
        public string LabelSeparator
        {
            get { return this.m_labelSeparator; }
            set
            {
                this.m_labelSeparator = value;
                this.FirePropertyChanged(p => p.Name);
            }
        }
        #endregion

        private ControlTemplate errorTemplate;
        private Dictionary<string, PropertyInfo> properties = new Dictionary<string, PropertyInfo>();
        private Dictionary<string, BindableAttribute> bindables = new Dictionary<string, BindableAttribute>();
        private Dictionary<string, BindingExpressionBase> bindings = new Dictionary<string, BindingExpressionBase>();
        private Dictionary<string, DisplayAttribute> displays = new Dictionary<string, DisplayAttribute>();
        private Dictionary<string, List<ValidationAttribute>> validations = new Dictionary<string, List<ValidationAttribute>>();
        private Dictionary<string, List<ValidationRule>> rules = new Dictionary<string, List<ValidationRule>>();
        private Dictionary<string, DependencyObject> controls = new Dictionary<string, DependencyObject>();



        public bool DefaultReadOnly
        {
            get { return (bool)GetValue(DefaultReadOnlyProperty); }
            set { SetValue(DefaultReadOnlyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DefaultReadOnly.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DefaultReadOnlyProperty =
            DependencyProperty.Register("DefaultReadOnly", typeof(bool), typeof(DataForm), new UIPropertyMetadata(false, OnDefaultReadOnlyChanged));

        private static void OnDefaultReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DataForm df = d as DataForm;
            if (df != null)
            {
                df.DiscoverObject();
            }
        }

        public DataForm()
        {
            InitializeComponent();

            this.errorTemplate = this.Resources["errorTemplate"] as ControlTemplate;
        }

        private void CurrentItemChanged()
        {
            this.InvalidateForm();
        }

        private void DiscoverObject()
        {
            this.displays.Clear();
            this.properties.Clear();
            this.bindables.Clear();
            this.bindings.Clear();
            this.rules.Clear();
            this.controls.Clear();
            this.validations.Clear();

            if (this.CurrentItem == null)
                return;

            Type dataType = this.CurrentItem.GetType();
            PropertyInfo[] properties;

            //Code for dynamic Types....
            /*var meta = this.CurrentItem as IDynamicMetaObjectProvider;
            if (meta != null)
            {
                List<PropertyInfo> tmpLst = new List<PropertyInfo>();
                var metaObject = meta.GetMetaObject(System.Linq.Expressions.Expression.Constant(this.CurrentItem));
                var membernames = metaObject.GetDynamicMemberNames();
                foreach (var membername in membernames)
                {
                    var binder = Microsoft.CSharp.RuntimeBinder.Binder.GetMember(CSharpBinderFlags.None, membername, dataType, new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
                    var callsite = CallSite<Func<CallSite, object, object>>.Create(binder);
                    var item = callsite.Target(callsite, this.CurrentItem);                    
                }
            }
            else*/
            {
                properties = dataType.GetProperties();
            }

            BindableAttribute bindable = ((BindableAttribute[])dataType.GetCustomAttributes(typeof(System.ComponentModel.BindableAttribute), true)).FirstOrDefault();
            
            foreach (PropertyInfo property in properties)
            {
                BindableAttribute propBindable = ((BindableAttribute[])property.GetCustomAttributes(typeof(System.ComponentModel.BindableAttribute), true)).FirstOrDefault();

                // Check if Readable
                if (!property.CanRead)
                    continue;

                // Check if Bindable
                if ((bindable != null && !bindable.Bindable && propBindable == null) || (bindable != null && !bindable.Bindable && propBindable != null && !propBindable.Bindable) || (propBindable != null && !propBindable.Bindable))
                    continue;

                DisplayAttribute propDisplay = ((DisplayAttribute[])property.GetCustomAttributes(typeof(DisplayAttribute), false)).FirstOrDefault();
                EditableAttribute propEditable = ((EditableAttribute[])property.GetCustomAttributes(typeof(EditableAttribute), false)).FirstOrDefault();
                List<ValidationAttribute> validations = new List<ValidationAttribute>((ValidationAttribute[])property.GetCustomAttributes(typeof(ValidationAttribute), true));

                if (propDisplay == null)
                    propDisplay = new DisplayAttribute() {AutoGenerateField = true, Name = property.Name, ShortName = property.Name, Order = 10000, Prompt = null, GroupName = null, Description = null };
                if (propEditable == null)
                    propEditable = new EditableAttribute(!this.DefaultReadOnly) { AllowInitialValue = true };

                bool setPrivate = true;
                if (property.GetSetMethod(true)!=null)
                    setPrivate = !property.GetSetMethod(true).IsPublic;

                if (propDisplay.GetAutoGenerateField().HasValue && !propDisplay.AutoGenerateField)
                    continue;

                if (bindable != null && propBindable == null)
                {
                    if ((!property.CanWrite || !propEditable.AllowEdit || setPrivate) && bindable.Direction == BindingDirection.TwoWay)
                        this.bindables.Add(property.Name, new BindableAttribute(true, BindingDirection.OneWay));
                    else
                        this.bindables.Add(property.Name, new BindableAttribute(true, bindable.Direction));
                }
                else if (propBindable != null)
                {
                    if ((!property.CanWrite || !propEditable.AllowEdit || setPrivate) && propBindable.Direction == BindingDirection.TwoWay)
                        this.bindables.Add(property.Name, new BindableAttribute(true, BindingDirection.OneWay));
                    else
                        this.bindables.Add(property.Name, new BindableAttribute(true, propBindable.Direction));
                }
                else
                {
                    if (!this.bindables.ContainsKey(property.Name))
                        if (!property.CanWrite || !propEditable.AllowEdit || setPrivate)
                            this.bindables.Add(property.Name, new BindableAttribute(true, BindingDirection.OneWay));
                        else
                            this.bindables.Add(property.Name, new BindableAttribute(true, BindingDirection.TwoWay));
                }

                if (!this.validations.ContainsKey(property.Name))
                {
                    this.validations.Add(property.Name, validations);
                    this.displays.Add(property.Name, propDisplay);
                    this.properties.Add(property.Name, property);
                }
            }
        }

        public void InvalidateForm()
        {
            this.Layout.Children.Clear();
            this.DiscoverObject();

            Grid grid1 = new Grid();
            grid1.Margin = new Thickness(5);
            grid1.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid1.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(2, GridUnitType.Star) });

            int row = 0;

            var listProperties = from p in this.displays orderby (p.Value.GetOrder() ?? 0) select this.properties[p.Key];

            foreach (PropertyInfo property in listProperties)
            {
                TextBlock lbl = new TextBlock();
                lbl.Text = String.Format("{0} {1}",  displays[property.Name].GetName(), this.m_labelSeparator);
                lbl.ToolTip = displays[property.Name].GetDescription();
                lbl.TextAlignment = TextAlignment.Right;
                lbl.Margin = new Thickness(5, 0, 5, 0);
                lbl.HorizontalAlignment = HorizontalAlignment.Stretch;
                lbl.VerticalAlignment = VerticalAlignment.Center;

                // Binding Creation
                Binding binding = new Binding(property.Name);
                binding.Source = this.CurrentItem;
                binding.Mode = (bindables[property.Name].Direction == BindingDirection.TwoWay ? BindingMode.TwoWay : BindingMode.OneWay);
                binding.ValidatesOnDataErrors = true;
                binding.ValidatesOnExceptions = true;
                binding.NotifyOnValidationError = true;
                binding.NotifyOnTargetUpdated = true;
                binding.NotifyOnSourceUpdated = true;
                binding.IsAsync = true;

                foreach (ValidationAttribute attribs in this.validations[property.Name])
                {
                    ValidationRule rule = new AttributeValidationRule(attribs, property.Name);
                    binding.ValidationRules.Add(rule);
                    if (!this.rules.ContainsKey(property.Name))
                        this.rules.Add(property.Name, new List<ValidationRule>());
                    this.rules[property.Name].Add(rule);                    
                }

                // Control creation
                Control editorControl = this.GetControlFromProperty(property, binding);

                if (editorControl != null)
                {
                    editorControl.ToolTip = displays[property.Name].GetDescription();
                    Validation.SetErrorTemplate(editorControl, errorTemplate);
                }

                if (editorControl == null)
                    continue;

                // Add to view
                grid1.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(28) });
                Grid.SetColumn(lbl, 0);
                Grid.SetRow(lbl, row);
                Grid.SetColumn(editorControl, 1);
                Grid.SetRow(editorControl, row);

                grid1.Children.Add(lbl);
                grid1.Children.Add(editorControl);
                this.controls.Add(property.Name, editorControl);

                row++;
            }

            this.Layout.Children.Add(grid1);
        }

        private Control GetControlFromProperty(PropertyInfo property, Binding binding)
        {
            if (property.PropertyType == typeof(bool))
            {
                CheckBox checkBox = new CheckBox() { VerticalAlignment = VerticalAlignment.Center};
                checkBox.IsEnabled = (bindables[property.Name].Direction == BindingDirection.TwoWay);
                this.bindings.Add(property.Name, checkBox.SetBinding(CheckBox.IsCheckedProperty, binding));
                return checkBox;
            }
            else if (property.PropertyType == typeof(bool?))
            {
                CheckBox checkBox = new CheckBox() { VerticalAlignment = VerticalAlignment.Center };
                checkBox.IsThreeState = true;
                checkBox.IsEnabled = (bindables[property.Name].Direction == BindingDirection.TwoWay);
                this.bindings.Add(property.Name, checkBox.SetBinding(CheckBox.IsCheckedProperty, binding));

                return checkBox;
            }
            else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
            {
                DatePicker control = new DatePicker() { Margin = new Thickness(0, 2, 22, 2) };
                this.bindings.Add(property.Name, control.SetBinding(DatePicker.SelectedDateProperty, binding));

                return control;
            }
            else if (property.PropertyType.IsEnum)
            {
                ComboBox comboBox = new ComboBox() { Margin = new Thickness(0, 2, 18, 2) };
                comboBox.ItemsSource = Enum.GetValues(property.PropertyType);
                comboBox.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);
                this.bindings.Add(property.Name, comboBox.SetBinding(ComboBox.SelectedItemProperty, binding));

                return comboBox;
            }
            else if (property.PropertyType == typeof(string))
            {
                WatermarkTextBox txtBox = new WatermarkTextBox() { Margin = new Thickness(0, 3, 18, 3), Watermark = displays[property.Name].GetPrompt() };
                txtBox.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);
                
                // Binding
                this.bindings.Add(property.Name, txtBox.SetBinding(TextBox.TextProperty, binding));

                return txtBox;
            }
            else if (property.PropertyType == typeof(Int32) || property.PropertyType == typeof(UInt32) || property.PropertyType == typeof(Int16) || property.PropertyType == typeof(UInt16) || property.PropertyType == typeof(Int64) || property.PropertyType == typeof(UInt64) || property.PropertyType == typeof(byte) || property.PropertyType == typeof(sbyte))
            {
                IntegerUpDown integerUpDown = new IntegerUpDown() { Margin = new Thickness(0, 3, 18, 3) };
                integerUpDown.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

                // Binding
                this.bindings.Add(property.Name, integerUpDown.SetBinding(IntegerUpDown.ValueProperty, binding));

                return integerUpDown;
            }
            else if (property.PropertyType == typeof(Decimal))
            {
                DecimalUpDown decimalUpDown = new DecimalUpDown() { Margin = new Thickness(0, 3, 18, 3) };
                decimalUpDown.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

                // Binding
                this.bindings.Add(property.Name, decimalUpDown.SetBinding(DecimalUpDown.ValueProperty, binding));

                return decimalUpDown;
            }
            else if (property.PropertyType == typeof(Single) || property.PropertyType == typeof(Double))
            {
                CalculatorUpDown calculatorUpDown = new CalculatorUpDown() { Margin = new Thickness(0, 3, 18, 3) };
                calculatorUpDown.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

                // Binding
                this.bindings.Add(property.Name, calculatorUpDown.SetBinding(CalculatorUpDown.ValueProperty, binding));

                return calculatorUpDown;
            }
            else if (property.PropertyType == typeof(Color))
            {
                ColorPicker colorPicker = new ColorPicker() { Margin = new Thickness(0, 3, 18, 3) };
                colorPicker.IsEnabled = (bindables[property.Name].Direction == BindingDirection.TwoWay);

                // Binding
                this.bindings.Add(property.Name, colorPicker.SetBinding(ColorPicker.SelectedColorProperty, binding));

                return colorPicker;
            }
            else
            {
                return null;
            }
        }

        private UIElement GetLabelFromProperty(PropertyInfo prop)
        {
            object[] attrs = prop.GetCustomAttributes(typeof(DisplayAttribute), false);
            DisplayAttribute display = null;
            if (attrs.Length == 1)
                display = (DisplayAttribute)attrs[0];
            else
                display = new DisplayAttribute() { Name = prop.Name,  };

            TextBlock lbl = new TextBlock();

            string labelText = prop.Name;
            
            lbl.Text = String.Format("{0} {1}", prop.Name, this.m_labelSeparator);
                        
            lbl.TextAlignment = TextAlignment.Right;
            lbl.Margin = new Thickness(5, 0, 5, 0);
            lbl.HorizontalAlignment = HorizontalAlignment.Stretch;
            lbl.VerticalAlignment = VerticalAlignment.Center;

            return lbl;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
        {
            bool result = true;
            foreach (KeyValuePair<string, List<ValidationRule>> kvp in this.rules)
            {
                foreach (ValidationRule rule in kvp.Value)
                {
                    System.Windows.Controls.ValidationResult vresult = rule.Validate(this.properties[kvp.Key].GetValue(this.CurrentItem, null), System.Globalization.CultureInfo.CurrentUICulture);
                    if (vresult.IsValid == false)
                    {
                        result = false;
                        if (!Validation.GetHasError(this.controls[kvp.Key]))
                            Validation.MarkInvalid(this.bindings[kvp.Key], new ValidationError(rule, this.bindings[kvp.Key].ParentBindingBase, vresult.ErrorContent, null));
                    }
                }
            }

            return result ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ReadOnlyObservableCollection<ValidationError> GetErrors()
        {
            ObservableCollection<ValidationError> errors = new ObservableCollection<ValidationError>();

            foreach (KeyValuePair<string, DependencyObject> kvp in this.controls)
            {
                ReadOnlyObservableCollection<ValidationError> cerrs = Validation.GetErrors(kvp.Value);
                foreach (ValidationError error in cerrs)
                    errors.Add(error);
            }

            return new ReadOnlyObservableCollection<ValidationError>(errors);
        }
    }
}