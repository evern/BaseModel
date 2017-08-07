using BaseModel.Attributes;
using BaseModel.Misc;
using DevExpress.Mvvm;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.LookUp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;

namespace BaseModel.Data.Helpers
{   
    public class CopyPasteHelper<TProjection>
        where TProjection : new()
    {
        public delegate bool IsValidProjectionFunc(TProjection projection, ref string errorMessage);
        readonly IsValidProjectionFunc isValidProjectionFunc;
        readonly Func<TProjection, bool> onBeforePasteWithValidationFunc;
        readonly IMessageBoxService messageBoxService;

        public CopyPasteHelper(IsValidProjectionFunc isValidProjectionFunc = null, Func<TProjection, bool> onBeforePasteWithValidationFunc = null, IMessageBoxService messageBoxService = null)
        {
            this.isValidProjectionFunc = isValidProjectionFunc;
            this.onBeforePasteWithValidationFunc = onBeforePasteWithValidationFunc;
            this.messageBoxService = messageBoxService;
        }

        public List<TProjection> PastingFromClipboard<TView>(PastingFromClipboardEventArgs e)
            where TView : DataViewBase
        {
            var PasteString = Clipboard.GetText();
            var RowData = PasteString.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sourceGridControl = (GridControl)e.Source;
            var gridView = sourceGridControl.View;

            List<TProjection> pasteProjections = new List<TProjection>();
            if (gridView.ActiveEditor == null && (gridView.GetType() == typeof(TView)))
            {
                var gridTView = gridView as TView;
                TableView gridTableView = gridTView as TableView;
                TreeListView gridTreeListView = gridTView as TreeListView;

                foreach (var Row in RowData)
                {
                    TProjection projection = new TProjection();

                    var ColumnStrings = Row.Split('\t');
                    for (var i = 0; i < ColumnStrings.Count(); i++)
                        try
                        {
                            ColumnBase copyColumn = gridTableView != null ? gridTableView.VisibleColumns[i] : gridTreeListView.VisibleColumns[i];

                            if (copyColumn.ReadOnly)
                                continue;

                            var columnName = copyColumn.FieldName;
                            var columnPropertyInfo = DataUtils.GetNestedPropertyInfo(columnName, projection);
                            if (columnPropertyInfo != null)
                                if (columnPropertyInfo.PropertyType == typeof(Guid?) ||
                                    columnPropertyInfo.PropertyType == typeof(Guid))
                                {
                                    Type editSettingsType = copyColumn.ActualEditSettings.GetType();
                                    object editSettings = null;
                                    if(editSettingsType == typeof(ComboBoxEditSettings))
                                        editSettings = copyColumn.ActualEditSettings as ComboBoxEditSettings;
                                    else if (editSettingsType == typeof(LookUpEditSettings))
                                        editSettings = copyColumn.ActualEditSettings as LookUpEditSettingsBase;

                                    if (editSettings != null)
                                    {
                                        var copyColumnValueMember = (string)editSettings.GetType().GetProperty("ValueMember").GetValue(editSettings);
                                        var copyColumnDisplayMember = (string)editSettings.GetType().GetProperty("DisplayMember").GetValue(editSettings);
                                        var copyColumnItemsSource = (IEnumerable<object>)editSettings.GetType().GetProperty("ItemsSource").GetValue(editSettings);
                                        Guid? itemValue = null;
                                        foreach (var copyColumnItem in copyColumnItemsSource)
                                        {
                                            var itemDisplayMemberPropertyInfo =
                                                copyColumnItem.GetType().GetProperty(copyColumnDisplayMember);
                                            var itemValueMemberPropertyInfo =
                                                copyColumnItem.GetType().GetProperty(copyColumnValueMember);
                                            if (itemDisplayMemberPropertyInfo.GetValue(copyColumnItem).ToString().ToUpper() ==
                                                ColumnStrings[i].ToUpper())
                                            {
                                                itemValue = (Guid)itemValueMemberPropertyInfo.GetValue(copyColumnItem);
                                                break;
                                            }
                                        }

                                        if (itemValue != null)
                                            DataUtils.SetNestedValue(columnName, projection, itemValue);
                                        else
                                            continue;
                                    }
                                    else if (ColumnStrings[i] != Guid.Empty.ToString())
                                    {
                                        var newGuid = new Guid(ColumnStrings[i]);
                                        DataUtils.SetNestedValue(columnName, projection, newGuid);
                                    }
                                }
                                else if (columnPropertyInfo.PropertyType == typeof(string))
                                    DataUtils.SetNestedValue(columnName, projection, ColumnStrings[i]);
                                else if (columnPropertyInfo.PropertyType.BaseType == typeof(Enum))
                                {
                                    var enumValues = Enum.GetValues(columnPropertyInfo.PropertyType);
                                    foreach (var enumValue in enumValues)
                                    {
                                        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
                                        if (fieldInfo == null)
                                            return pasteProjections;

                                        var descriptionAttributes =
                                            fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false) as
                                                DisplayAttribute[];
                                        if (descriptionAttributes == null || descriptionAttributes.Count() == 0)
                                            return pasteProjections;

                                        var descriptionAttribute = descriptionAttributes.First();
                                        if (ColumnStrings[i] == descriptionAttribute.Name)
                                        {
                                            DataUtils.SetNestedValue(columnName, projection, enumValue);
                                            continue;
                                        }
                                    }
                                }
                                else if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                                         columnPropertyInfo.PropertyType == typeof(decimal?)
                                         || columnPropertyInfo.PropertyType == typeof(int) ||
                                         columnPropertyInfo.PropertyType == typeof(int?)
                                         || columnPropertyInfo.PropertyType == typeof(double) ||
                                         columnPropertyInfo.PropertyType == typeof(double?))
                                {
                                    var rgx = new Regex("[^0-9a-z\\.]");
                                    var cleanColumnString = rgx.Replace(ColumnStrings[i], string.Empty);

                                    if (columnPropertyInfo.PropertyType == typeof(decimal) ||
                                        columnPropertyInfo.PropertyType == typeof(decimal?))
                                    {
                                        decimal getDecimal;
                                        if (decimal.TryParse(cleanColumnString, out getDecimal))
                                        {
                                            if (columnName.Contains('%') || columnName.ToUpper().Contains("PERCENT"))
                                                getDecimal /= 100;

                                            DataUtils.SetNestedValue(columnName, projection, getDecimal);
                                        }
                                        else
                                            return pasteProjections;
                                    }
                                    else if (columnPropertyInfo.PropertyType == typeof(int) ||
                                             columnPropertyInfo.PropertyType == typeof(int?))
                                    {
                                        int getInt;
                                        if (int.TryParse(cleanColumnString, out getInt))
                                            DataUtils.SetNestedValue(columnName, projection, getInt);
                                        else
                                            return pasteProjections;
                                    }
                                    else if (columnPropertyInfo.PropertyType == typeof(double) ||
                                             columnPropertyInfo.PropertyType == typeof(double?))
                                    {
                                        double getDouble;
                                        if (double.TryParse(cleanColumnString, out getDouble))
                                        {
                                            if (columnName.Contains('%') || columnName.ToUpper().Contains("PERCENT"))
                                                getDouble /= 100;

                                            DataUtils.SetNestedValue(columnName, projection, getDouble);
                                        }
                                        else
                                            return pasteProjections;
                                    }
                                    else
                                        return pasteProjections;
                                }
                                else if (columnPropertyInfo.PropertyType == typeof(DateTime?) ||
                                         columnPropertyInfo.PropertyType == typeof(DateTime))
                                {
                                    DateTime getDateTime;
                                    if (DateTime.TryParse(ColumnStrings[i], out getDateTime))
                                        DataUtils.SetNestedValue(columnName, projection, getDateTime);
                                    else
                                        continue;
                                }
                                else
                                    continue;
                            else
                                continue;
                        }
                        catch(Exception ex)
                        {
                            string s = ex.ToString();
                            return pasteProjections;
                        }

                    var errorMessage = "Duplicate exists on constraint field named: ";
                    if (isValidProjectionFunc(projection, ref errorMessage))
                        if (onBeforePasteWithValidationFunc != null)
                        {
                            if (onBeforePasteWithValidationFunc(projection))
                                pasteProjections.Add(projection);
                        }
                        else
                            pasteProjections.Add(projection);
                    else
                    {
                        if(messageBoxService != null)
                        {
                            errorMessage += " , paste operation will be terminated";
                            messageBoxService.ShowMessage(errorMessage, CommonResources.Exception_UpdateErrorCaption, MessageButton.OK);
                        }

                        break;
                    }
                }
            }

            return pasteProjections;
        }
    }

    public static class MorphUtils<TFromEntity, TToEntity>
    {
        public static TToEntity ShallowCopy(TToEntity copyObject, TFromEntity objectToCopy,
            bool copyVirtualProperties = false)
        {
            var objectToCopyProperties =
                objectToCopy.GetType()
                    .GetProperties()
                    .Where(
                        p =>
                            (copyVirtualProperties == true || !p.GetGetMethod().IsVirtual) &&
                            !p.GetCustomAttributes().Any(attr => attr.GetType() == typeof(ProjectionPropertyAttribute)));
            foreach (var objectToCopyProperty in objectToCopyProperties)
            {
                if (!objectToCopyProperty.CanWrite || !objectToCopyProperty.CanRead)
                    continue;

                var objectToCopyValue = objectToCopyProperty.GetValue(objectToCopy);
                var copyObjectProperty = copyObject.GetType().GetProperty(objectToCopyProperty.Name);

                copyObjectProperty.SetValue(copyObject, objectToCopyValue);
            }

            return copyObject;
        }
    }

    public static class DataUtils
    {
        public static bool? IsNewEntity<TEntity>(TEntity entity)
            where TEntity : IHaveCreatedDate
        {
            IHaveCreatedDate iHaveCreatedDateProjectionEntity = entity as IHaveCreatedDate;
            if (iHaveCreatedDateProjectionEntity != null)
            {
                //workaround for created because Save() only sets the projection primary key, this is used for property redo where the interceptor only tampers with UPDATED and CREATED is left as null
                if (iHaveCreatedDateProjectionEntity.EntityCreatedDate.Date.Year == 1)
                    return true;
                else
                    return false;
            }

            return null;
        }

        public static object ShallowCopy(object copyObject, object objectToCopy, bool copyVirtualProperties = false)
        {
            if (copyObject == null || objectToCopy == null)
                return null;

            PropertyInfo keyProperty = objectToCopy.GetType().GetProperties().FirstOrDefault(x => x.GetCustomAttributes().Any(y => y.GetType() == typeof(KeyAttribute)));
            IEnumerable<PropertyInfo> objectToCopyProperties = objectToCopy.GetType().GetProperties().Where(p => !p.GetCustomAttributes().Any(attr => attr.GetType() == typeof(ProjectionPropertyAttribute)));
            if(!copyVirtualProperties)
                objectToCopyProperties = objectToCopyProperties.Where(p => !p.GetGetMethod().IsVirtual);

            
            if(keyProperty != null)
            {
                PropertyInfo copyObjectKeyProperty = copyObject.GetType().GetProperties().FirstOrDefault(x => x.Name == keyProperty.Name);
                if(copyObjectKeyProperty != null)
                {
                    var keyValue = keyProperty.GetValue(objectToCopy);
                    copyObjectKeyProperty.SetValue(copyObject, keyValue);
                }
            }

            foreach (var objectToCopyProperty in objectToCopyProperties)
            {
                if (!objectToCopyProperty.CanWrite || !objectToCopyProperty.CanRead)
                    continue;

                var objectToCopyValue = objectToCopyProperty.GetValue(objectToCopy);
                PropertyInfo copyObjectProperty = copyObject.GetType().GetProperty(objectToCopyProperty.Name);

                copyObjectProperty.SetValue(copyObject, objectToCopyValue);
            }

            return copyObject;
        }

        public static string FormatColumnFieldname(string columnFieldName)
        {
            return columnFieldName.Replace("Entity.", string.Empty);
        }

        public static PropertyInfo GetKeyPropertyInfo(Type type)
        {
            try
            {
                var keyPropertyInfo =
                    type.GetProperties()
                        .Single(
                            property =>
                                property.GetCustomAttributes().Any(attr => attr.GetType() == typeof(KeyAttribute)));
                return keyPropertyInfo;
            }
            catch
            {
                return null;
            }
        }

        public static IEnumerable<PropertyInfo> GetProjectionPropertyInfos(Type type)
        {
            try
            {
                var projectionPropertyInfos =
                    type.GetProperties()
                        .Where(
                            property =>
                                property.GetCustomAttributes()
                                    .Any(attr => attr.GetType() == typeof(ProjectionPropertyAttribute)))
                        .ToList();
                return projectionPropertyInfos;
            }
            catch
            {
                return null;
            }
        }

        public static PropertyInfo GetFilterNamePropertyInfo(Type type)
        {
            try
            {
                var filterPropertyInfo =
                    type.GetProperties()
                        .Single(
                            property =>
                                property.GetCustomAttributes()
                                    .Any(attr => attr.GetType() == typeof(FilterNameAttribute)));
                return filterPropertyInfo;
            }
            catch
            {
                return null;
            }
        }

        public static PropertyInfo GetFilterValuePropertyInfo(Type type)
        {
            try
            {
                var filterPropertyInfo =
                    type.GetProperties()
                        .Single(
                            property =>
                                property.GetCustomAttributes()
                                    .Any(attr => attr.GetType() == typeof(FilterValueAttribute)));
                return filterPropertyInfo;
            }
            catch
            {
                return null;
            }
        }

        public static IEnumerable<string> GetConstraintPropertyStrings(Type type)
        {
            var TypeSpecificConstraintAttribute =
                (ConstraintAttributes) Attribute.GetCustomAttribute(type, typeof(ConstraintAttributes), false);
            if (TypeSpecificConstraintAttribute != null)
                return TypeSpecificConstraintAttribute.ColumnNames;

            return null;
        }

        public static IEnumerable<string> GetBulkEditDisabledPropertyStrings(Type type)
        {
            var TypeSpecificConstraintAttribute =
                (BulkEditDisabledAttributes)
                Attribute.GetCustomAttribute(type, typeof(BulkEditDisabledAttributes), false);
            if (TypeSpecificConstraintAttribute != null)
                return TypeSpecificConstraintAttribute.ColumnNames;

            return null;
        }

        public static IEnumerable<string> GetRequiredPropertyStringsForProjection(Type type)
        {
            var TypeSpecificRequiredAttribute =
                (RequiredAttributes) Attribute.GetCustomAttribute(type, typeof(RequiredAttributes), false);
            if (TypeSpecificRequiredAttribute != null)
                return TypeSpecificRequiredAttribute.ColumnNames;

            return null;
        }

        public static IEnumerable<string> GetRequiredPropertyStrings(Type type)
        {
            var requiredPropertyStrings = new List<string>();
            var props = type.GetProperties();
            foreach (var prop in props)
            {
                var attrs = prop.GetCustomAttributes(true);
                foreach (var attr in attrs)
                {
                    var requiredAttr = attr as RequiredAttribute;
                    if (requiredAttr != null)
                        requiredPropertyStrings.Add(prop.Name);
                }
            }

            return requiredPropertyStrings;
        }

        /// <summary>
        /// Recurse member instance to change its value
        /// </summary>
        /// <param name="propertyString">Property string to change</param>
        /// <param name="parentInstance">Instance to modify</param>
        /// <param name="value">Value to modify</param>
        public static void SetNestedValue(string propertyString, object parentInstance, object value)
        {
            var propertyNames = propertyString.Split('.');
            var firstPropertyName = propertyNames.First();
            var childInstance = parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);

            if (!propertyString.Contains("."))
            {
                parentInstance.GetType().GetProperty(firstPropertyName).SetValue(parentInstance, value);
            }
            else
            {
                string childPropertyString = string.Empty;
                for (int i = 1; i < propertyNames.Count(); i++)
                {
                    childPropertyString += propertyNames[i] + ".";
                }

                if (childPropertyString != string.Empty)
                {
                    childPropertyString = childPropertyString.Substring(0, childPropertyString.Length - 1);
                    SetNestedValue(childPropertyString, childInstance, value);
                }
            }
        }

        /// <summary>
        /// Recurse member instance to get its value
        /// </summary>
        /// <param name="propertyString">Property string to get</param>
        /// <param name="parentInstance">Instance to get</param>
        public static object GetNestedValue(string propertyString, object parentInstance)
        {
            var propertyNames = propertyString.Split('.');
            var firstPropertyName = propertyNames.First();
            var childInstance = parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);

            if (!propertyString.Contains("."))
            {
                return parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);
            }
            else
            {
                string childPropertyString = string.Empty;
                for (int i = 1; i < propertyNames.Count(); i++)
                {
                    childPropertyString += propertyNames[i] + ".";
                }

                childPropertyString = childPropertyString.Substring(0, childPropertyString.Length - 1);
                return GetNestedValue(childPropertyString, childInstance);
            }
        }

        public static PropertyInfo GetNestedPropertyInfo(string propertyString, object parentInstance)
        {
            var propertyNames = propertyString.Split('.');
            var firstPropertyName = propertyNames.First();
            var childInstance = parentInstance.GetType().GetProperty(firstPropertyName).GetValue(parentInstance);

            if (!propertyString.Contains("."))
            {
                return parentInstance.GetType().GetProperty(firstPropertyName);
            }
            else
            {
                string childPropertyString = string.Empty;
                for (int i = 1; i < propertyNames.Count(); i++)
                {
                    childPropertyString += propertyNames[i] + ".";
                }

                childPropertyString = childPropertyString.Substring(0, childPropertyString.Length - 1);
                return GetNestedPropertyInfo(childPropertyString, childInstance);
            }
        }
    }
}