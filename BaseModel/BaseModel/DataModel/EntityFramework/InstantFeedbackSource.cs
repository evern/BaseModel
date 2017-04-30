using BaseModel.Utils;
using DevExpress.Data.Async.Helpers;
using DevExpress.Data.Linq;
using System;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;

namespace BaseModel.DataModel.EntityFramework
{
    internal class InstantFeedbackSource<TEntity> : IInstantFeedbackSource<TEntity>
        where TEntity : class
    {
        private readonly EntityInstantFeedbackSource source;
        private readonly PropertyDescriptorCollection threadSafeProperties;

        public InstantFeedbackSource(EntityInstantFeedbackSource source,
            PropertyDescriptorCollection threadSafeProperties)
        {
            this.source = source;
            this.threadSafeProperties = threadSafeProperties;
        }

        bool IListSource.ContainsListCollection
        {
            get { return ((IListSource) source).ContainsListCollection; }
        }

        IList IListSource.GetList()
        {
            return ((IListSource) source).GetList();
        }

        TProperty IInstantFeedbackSource<TEntity>.GetPropertyValue<TProperty>(object threadSafeProxy,
            Expression<Func<TEntity, TProperty>> propertyExpression)
        {
            var propertyName = ExpressionHelper.GetPropertyName(propertyExpression);
            var threadSafeProperty = threadSafeProperties[propertyName];
            return (TProperty) threadSafeProperty.GetValue(threadSafeProxy);
        }

        bool IInstantFeedbackSource<TEntity>.IsLoadedProxy(object threadSafeProxy)
        {
            return threadSafeProxy is ReadonlyThreadSafeProxyForObjectFromAnotherThread;
        }

        void IInstantFeedbackSource<TEntity>.Refresh()
        {
            source.Refresh();
        }
    }
}