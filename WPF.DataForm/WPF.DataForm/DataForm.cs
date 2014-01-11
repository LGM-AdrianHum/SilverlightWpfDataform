﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
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

#if !SILVERLIGHT
using Xceed.Wpf.Toolkit;
#endif

namespace System.Windows.Controls
{
    public class WPFDataForm : Control, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Membres

        public event PropertyChangedEventHandler PropertyChanged;

        private void FirePropertyChanged<TProp>(Expression<Func<WPFDataForm, TProp>> propertySelector)
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



        public ControlTemplate ErrorTemplate
        {
            get { return (ControlTemplate)GetValue(ErrorTemplateProperty); }
            set { SetValue(ErrorTemplateProperty, value); }
        }

        public static readonly DependencyProperty ErrorTemplateProperty =
            DependencyProperty.Register("ErrorTemplate", typeof(ControlTemplate), typeof(WPFDataForm), new PropertyMetadata(null));

        

        #region CurrentItem
        public object CurrentItem
        {
            get { return (object)GetValue(CurrentItemProperty); }
            set { SetValue(CurrentItemProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CurrentItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentItemProperty =
            DependencyProperty.Register("CurrentItem", typeof(object), typeof(WPFDataForm), new PropertyMetadata(null, new PropertyChangedCallback(CurrentItemValueChanged)));

        private static void CurrentItemValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            WPFDataForm df = sender as WPFDataForm;
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

        #region Events

        public event EventHandler<DataFormAutoGeneratingFieldEventArgs> AutoGeneratingField;

        #endregion
        private Dictionary<string, PropertyInfo> properties = new Dictionary<string, PropertyInfo>();
        private Dictionary<string, BindableAttribute> bindables = new Dictionary<string, BindableAttribute>();
        protected Dictionary<string, BindingExpressionBase> bindings = new Dictionary<string, BindingExpressionBase>();
        private Dictionary<string, DisplayAttribute> displays = new Dictionary<string, DisplayAttribute>();
        private Dictionary<string, List<ValidationAttribute>> validations = new Dictionary<string, List<ValidationAttribute>>();
#if !SILVERLIGHT
        private Dictionary<string, List<ValidationRule>> rules = new Dictionary<string, List<ValidationRule>>();
#endif
        private Dictionary<string, DependencyObject> controls = new Dictionary<string, DependencyObject>();

        private Grid partGrid;

        public bool DefaultReadOnly
        {
            get { return (bool)GetValue(DefaultReadOnlyProperty); }
            set { SetValue(DefaultReadOnlyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DefaultReadOnly.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DefaultReadOnlyProperty =
            DependencyProperty.Register("DefaultReadOnly", typeof(bool), typeof(WPFDataForm), new PropertyMetadata(false, OnDefaultReadOnlyChanged));

        private static void OnDefaultReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            WPFDataForm df = d as WPFDataForm;
            if (df != null)
            {
                df.DiscoverObject();
            }
        }

#if !SILVERLIGHT
        static WPFDataForm()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WPFDataForm), new FrameworkPropertyMetadata(typeof(WPFDataForm)));
        }
#endif

        public WPFDataForm()
        {
#if SILVERLIGHT 
            this.DefaultStyleKey = typeof(WPFDataForm);           
#endif
        }

        public override void OnApplyTemplate()
        {
            this.partGrid = GetTemplateChild("PART_Grid") as Grid;

            InvalidateForm();
        }


        private void CurrentItemChanged()
        {
            this.InvalidateForm();
        }

        protected virtual PropertyInfo[] GetPropertyInfos(Type type)
        {
            return type.GetProperties();
        }

        private void DiscoverObject()
        {
            this.displays.Clear();
            this.properties.Clear();
            this.bindables.Clear();
            this.bindings.Clear();
#if !SILVERLIGHT
            this.rules.Clear();
#endif
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
                properties = GetPropertyInfos(dataType);
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

                
                DisplayAttribute propDisplay = ((DisplayAttribute[])Attribute.GetCustomAttributes(property, typeof(DisplayAttribute), true)).FirstOrDefault();
                EditableAttribute propEditable = ((EditableAttribute[])Attribute.GetCustomAttributes(property, typeof(EditableAttribute), true)).FirstOrDefault();
                List<ValidationAttribute> validations = new List<ValidationAttribute>((ValidationAttribute[])Attribute.GetCustomAttributes(property, typeof(ValidationAttribute), true));

                if (propDisplay == null)
                    propDisplay = new DisplayAttribute() { AutoGenerateField = true, Name = property.Name, ShortName = property.Name, Order = 10000, Prompt = null, GroupName = null, Description = null };
                if (propEditable == null)
                    propEditable = new EditableAttribute(!this.DefaultReadOnly) { AllowInitialValue = true };

                bool setPrivate = true;
                if (property.GetSetMethod(true) != null)
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

        protected virtual FrameworkElement GetLabelTextBlock(string name, string toolTip)
        {
            TextBlock lbl = new TextBlock();
            
            lbl.Text = String.Format("{0} {1}", name, this.m_labelSeparator);
            ToolTipService.SetToolTip(lbl, toolTip);
            lbl.TextAlignment = TextAlignment.Right;
            lbl.Margin = new Thickness(5, 0, 5, 0);
            lbl.HorizontalAlignment = HorizontalAlignment.Stretch;
            lbl.VerticalAlignment = VerticalAlignment.Center;

            return lbl;
        }
        public void InvalidateForm()
        {
            if (partGrid != null)
            {
                partGrid.Children.Clear();
                this.DiscoverObject();

                Grid grid1 = new Grid();
                grid1.Margin = new Thickness(5);
                grid1.ColumnDefinitions.Add(new ColumnDefinition() {Width = new GridLength(1, GridUnitType.Auto)});
                grid1.ColumnDefinitions.Add(new ColumnDefinition() {Width = new GridLength(1, GridUnitType.Auto)});

                int row = 0;

                var listProperties = from p in this.displays
                    orderby (p.Value.GetOrder() ?? 0)
                    select this.properties[p.Key];

                foreach (PropertyInfo property in listProperties)
                {
                    var nm = displays[property.Name].GetName();
                    if (string.IsNullOrEmpty(nm))
                        nm = property.Name;
                    var tooltip = displays[property.Name].GetDescription();

                    var lbl = GetLabelTextBlock(nm, tooltip);
                    
                    // Binding Creation
                    Binding binding = new Binding(property.Name);
                    binding.Source = this.CurrentItem;
                    binding.Mode = (bindables[property.Name].Direction == BindingDirection.TwoWay
                        ? BindingMode.TwoWay
                        : BindingMode.OneWay);
                    binding.ValidatesOnDataErrors = true;
                    binding.ValidatesOnExceptions = true;
                    binding.NotifyOnValidationError = true;

#if !SILVERLIGHT
                    //binding.NotifyOnTargetUpdated = true;
                    //binding.NotifyOnSourceUpdated = true;
                    //binding.IsAsync = true;
#endif

#if !SILVERLIGHT
                    foreach (ValidationAttribute attribs in this.validations[property.Name])
                    {
                        ValidationRule rule = new AttributeValidationRule(attribs, property.Name);
                        binding.ValidationRules.Add(rule);
                        if (!this.rules.ContainsKey(property.Name))
                            this.rules.Add(property.Name, new List<ValidationRule>());
                        this.rules[property.Name].Add(rule);
                    }
#endif

                    // Control creation
                    Control editorControl = this.GetControlFromProperty(property, binding);

                    if (editorControl == null)
                        continue;

                    var df = new DataField() {Content = editorControl, Label = lbl};

                    DataFormAutoGeneratingFieldEventArgs e = new DataFormAutoGeneratingFieldEventArgs(property.Name,
                        property.PropertyType, df);
                    EventHandler<DataFormAutoGeneratingFieldEventArgs> eventHandler = this.AutoGeneratingField;
                    if (eventHandler != null)
                        eventHandler(this, e);

                    if (e.Cancel)
                        continue;

                    ToolTipService.SetToolTip(df.Content, displays[property.Name].GetDescription());  
#if !SILVERLIGHT
                    Validation.SetErrorTemplate(df.Content, ErrorTemplate);
#endif
                    //df.Content.HorizontalAlignment = Windows.HorizontalAlignment.Stretch;

                    // Add to view
                    RowDefinition newRow = new RowDefinition() {Height = new GridLength(28)};
                    grid1.RowDefinitions.Add(newRow);
                    if (df.Content.Height.CompareTo(Double.NaN) != 0)
                    {
                        newRow.Height = new GridLength(df.Content.Height);
                    }
                    Grid.SetColumn(df.Label, 0);
                    Grid.SetRow(df.Label, row);
                    Grid.SetColumn(df.Content, 1);
                    Grid.SetRow(df.Content, row);

                    grid1.Children.Add(df.Label);
                    grid1.Children.Add(df.Content);
                    this.controls.Add(property.Name, df.Content);

                    row++;
                }

                partGrid.Children.Add(grid1);
            }
        }

        #region Control Generators

        private Control GenerateCheckBox(PropertyInfo property, Binding binding)
        {
            CheckBox checkBox = new CheckBox() { VerticalAlignment = VerticalAlignment.Center };
            checkBox.IsEnabled = (bindables[property.Name].Direction == BindingDirection.TwoWay);
            this.bindings.Add(property.Name, checkBox.SetBinding(CheckBox.IsCheckedProperty, binding));
            return checkBox;
        }
        private Control GenerateThreeStateCheckBox(PropertyInfo property, Binding binding)
        {
            CheckBox checkBox = new CheckBox() { VerticalAlignment = VerticalAlignment.Center };
            checkBox.IsThreeState = true;
            checkBox.IsEnabled = (bindables[property.Name].Direction == BindingDirection.TwoWay);
            this.bindings.Add(property.Name, checkBox.SetBinding(CheckBox.IsCheckedProperty, binding));
            return checkBox;
        }

        private Control GenerateDatePicker(PropertyInfo property, Binding binding)
        {
            DatePicker control = new DatePicker() { Margin = new Thickness(0, 2, 22, 2) };
            this.bindings.Add(property.Name, control.SetBinding(DatePicker.SelectedDateProperty, binding));
            return control;
        }

        private Control GenerateComboBox(PropertyInfo property, Binding binding)
        {

            ComboBox comboBox = new ComboBox() { Margin = new Thickness(0, 2, 18, 2) };
            comboBox.ItemsSource = Enum.GetValues(property.PropertyType);
#if !SILVERLIGHT
            comboBox.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);
#else
            comboBox.IsEnabled = !(bindables[property.Name].Direction == BindingDirection.TwoWay);
#endif
            this.bindings.Add(property.Name, comboBox.SetBinding(ComboBox.SelectedItemProperty, binding));

            return comboBox;
        }

        private Control GenerateWaterMarkedTextBox(PropertyInfo property, Binding binding)
        {
#if !SILVERLIGHT
            WatermarkTextBox txtBox = new WatermarkTextBox() { Margin = new Thickness(0, 3, 18, 3), Watermark = displays[property.Name].GetPrompt() };
            txtBox.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, txtBox.SetBinding(TextBox.TextProperty, binding));
#else
            TextBox txtBox = new TextBox() { Margin = new Thickness(0, 3, 18, 3) };
            txtBox.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, txtBox.SetBinding(TextBox.TextProperty, binding));
#endif
            return txtBox;
        }

        private Control GenerateIntegerUpDow(PropertyInfo property, Binding binding)
        {
#if !SILVERLIGHT
            IntegerUpDown integerUpDown = new IntegerUpDown() { Margin = new Thickness(0, 3, 18, 3) };
            integerUpDown.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, integerUpDown.SetBinding(IntegerUpDown.ValueProperty, binding));
#else
            NumericUpDown integerUpDown = new NumericUpDown() { Margin = new Thickness(0, 3, 18, 3) };
            integerUpDown.IsEditable = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, integerUpDown.SetBinding(NumericUpDown.ValueProperty, binding));
#endif
            return integerUpDown;
        }

        private Control GenerateDecimalUpDown(PropertyInfo property, Binding binding)
        {
#if !SILVERLIGHT
            DecimalUpDown decimalUpDown = new DecimalUpDown() { Margin = new Thickness(0, 3, 18, 3) };
            decimalUpDown.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, decimalUpDown.SetBinding(DecimalUpDown.ValueProperty, binding));
#else
            NumericUpDown decimalUpDown = new NumericUpDown() { Margin = new Thickness(0, 3, 18, 3) };
            decimalUpDown.IsEditable = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, decimalUpDown.SetBinding(NumericUpDown.ValueProperty, binding));
#endif
            return decimalUpDown;
        }

        private Control GenerateCalculator(PropertyInfo property, Binding binding)
        {
#if !SILVERLIGHT
            CalculatorUpDown calculatorUpDown = new CalculatorUpDown() { Margin = new Thickness(0, 3, 18, 3) };
            calculatorUpDown.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, calculatorUpDown.SetBinding(CalculatorUpDown.ValueProperty, binding));
#else
            NumericUpDown calculatorUpDown = new NumericUpDown() { Margin = new Thickness(0, 3, 18, 3) };
            calculatorUpDown.IsEditable = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, calculatorUpDown.SetBinding(NumericUpDown.ValueProperty, binding));
#endif
            return calculatorUpDown;
        }

        private Control GenerateColorPicker(PropertyInfo property, Binding binding)
        {
#if !SILVERLIGHT
            ColorPicker colorPicker = new ColorPicker() { Margin = new Thickness(0, 3, 18, 3) };
            colorPicker.IsEnabled = (bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, colorPicker.SetBinding(ColorPicker.SelectedColorProperty, binding));
#else
            Control colorPicker = null;
#endif
            return colorPicker;
        }
        private Control GenerateMultiLineTextBox(PropertyInfo property, Binding binding, int? PreferredHeight)
        {
            TextBox txtBox = new TextBox() { Margin = new Thickness(0, 3, 18, 3), TextWrapping = TextWrapping.Wrap };
            if (PreferredHeight != null)
            {
                txtBox.Height = (int)PreferredHeight;
            }
            else
            {
                txtBox.Height = 56;
            }

            txtBox.IsReadOnly = !(bindables[property.Name].Direction == BindingDirection.TwoWay);

            // Binding
            this.bindings.Add(property.Name, txtBox.SetBinding(TextBox.TextProperty, binding));

            return txtBox;
        }

        #endregion

        protected virtual Control GetControlFromProperty(PropertyInfo property, Binding binding)
        {
            // check attribute on this property to determine if we use defaults
            object[] attrs = property.GetCustomAttributes(typeof(InputTypeAttribute), false);
            InputTypeAttribute display;
            if (attrs.Length == 1)
                display = (InputTypeAttribute)attrs[0];
            else
                display = new InputTypeAttribute() { FormType = InputTypeAttribute.FormTypes.@default };

            Control control = null;
            if (display.FormType == InputTypeAttribute.FormTypes.@default)
            {
                if (property.PropertyType == typeof(bool))
                {
                    control = GenerateCheckBox(property, binding);
                }
                else if (property.PropertyType == typeof(bool?))
                {
                    control = GenerateThreeStateCheckBox(property, binding);
                }
                else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                {
                    control = GenerateDatePicker(property, binding);
                }
                else if (property.PropertyType.IsEnum)
                {
                    control = GenerateComboBox(property, binding);
                }
                else if (property.PropertyType == typeof(string))
                {
                    control = GenerateWaterMarkedTextBox(property, binding);
                }
                else if (property.PropertyType == typeof(Int32) || property.PropertyType == typeof(UInt32) || property.PropertyType == typeof(Int16) || property.PropertyType == typeof(UInt16) || property.PropertyType == typeof(Int64) || property.PropertyType == typeof(UInt64) || property.PropertyType == typeof(byte) || property.PropertyType == typeof(sbyte))
                {
                    control = GenerateIntegerUpDow(property, binding);
                }
                else if (property.PropertyType == typeof(Decimal))
                {
                    control = GenerateDecimalUpDown(property, binding);
                }
                else if (property.PropertyType == typeof(Single) || property.PropertyType == typeof(Double))
                {
                    control = GenerateCalculator(property, binding);
                }
                else if (property.PropertyType == typeof(Color))
                {
                    control = GenerateColorPicker(property, binding);
                }
                else
                {
                    control = null;
                }
            }
            else
            {  // we direct the object
                switch (display.FormType)
                {
                    case InputTypeAttribute.FormTypes.box:
                        control = GenerateWaterMarkedTextBox(property, binding);
                        break;
                    case InputTypeAttribute.FormTypes.calculator:
                        control = GenerateCalculator(property, binding);
                        break;
                    case InputTypeAttribute.FormTypes.check:
                        control = GenerateCheckBox(property, binding);
                        break;
                    case InputTypeAttribute.FormTypes.dates:
                        control = GenerateDatePicker(property, binding);
                        break;
                    case InputTypeAttribute.FormTypes.textArea:
                        control = GenerateMultiLineTextBox(property, binding, display.PreferredHeight);
                        break;
                    default:
                        break;
                }

            }
            if (control != null)
                control.HorizontalAlignment = Windows.HorizontalAlignment.Stretch;
            if (display.PreferredWidth != null && control != null)
            {
                control.Width = (int)display.PreferredWidth;
            }
            return control;
        }

        private UIElement GetLabelFromProperty(PropertyInfo prop)
        {
            object[] attrs = prop.GetCustomAttributes(typeof(DisplayAttribute), false);
            DisplayAttribute display = null;
            if (attrs.Length == 1)
                display = (DisplayAttribute)attrs[0];
            else
                display = new DisplayAttribute() { Name = prop.Name, };

            TextBlock lbl = new TextBlock();

            string labelText = prop.Name;

            lbl.Text = String.Format("{0} {1}", prop.Name, this.m_labelSeparator);

            lbl.TextAlignment = TextAlignment.Right;
            lbl.Margin = new Thickness(5, 0, 5, 0);
            lbl.HorizontalAlignment = HorizontalAlignment.Stretch;
            lbl.VerticalAlignment = VerticalAlignment.Center;

            return lbl;
        }

#if !SILVERLIGHT
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

            return result;
        }
#endif

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
