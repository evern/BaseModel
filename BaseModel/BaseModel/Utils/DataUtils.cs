using BaseModel.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace BaseModel.Data.Helpers
{
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