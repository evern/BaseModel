using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.Misc
{
    public class BulkProcessModel<TProjection, TEntity>
    {
        public TProjection Projection { get; set; }
        public TEntity RepositoryEntity { get; set; }
        public bool IsNewEntity { get; set; }
    }
}
