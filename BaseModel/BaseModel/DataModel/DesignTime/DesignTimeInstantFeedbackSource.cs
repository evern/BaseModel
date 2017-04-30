using DevExpress.Mvvm;
using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

namespace BaseModel.DataModel.DesignTime
{
    internal class DesignTimeInstantFeedbackSource<TProjection, TPrimaryKey> : IInstantFeedbackSource<TProjection>
        where TProjection : class
    {
        void IInstantFeedbackSource<TProjection>.Refresh()
        {
        }

        bool IInstantFeedbackSource<TProjection>.IsLoadedProxy(object threadSafeProxy)
        {
            return true;
        }

        bool IListSource.ContainsListCollection
        {
            get { return true; }
        }

        IList IListSource.GetList()
        {
            return DesignTimeHelper.CreateDesignTimeObjects<TProjection>(2).ToList();
        }

        TProperty IInstantFeedbackSource<TProjection>.GetPropertyValue<TProperty>(object threadSafeProxy,
            Expression<Func<TProjection, TProperty>> propertyExpression)
        {
            return default(TProperty);
        }
    }
}