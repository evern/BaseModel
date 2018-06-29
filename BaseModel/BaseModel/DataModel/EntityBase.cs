using BaseModel.Misc;
using DevExpress.Mvvm;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseModel.DataModel
{
    /// <summary>
    /// Provides mapping back to key naming convention for projection
    /// Projection key property name must be the same with entity key name for repository results to map back to projections
    /// </summary>
    /// <typeparam name="TEntity">Entity with Guid typed key</typeparam>
    public abstract class EntityBase : BindableBase, ICanUpdate
    {
        [NotMapped]
        public bool NewEntityFromView { get; set; }

        public virtual void Update()
        {
            RaisePropertiesChanged();
        }
    }
}
