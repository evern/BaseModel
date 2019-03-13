using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.Misc
{
    public class TokenProperty<TPrimaryKey>
    {
        readonly Func<TPrimaryKey> getAccessor;
        readonly Action<TPrimaryKey> setAccessor;
        readonly Func<IEnumerable<TPrimaryKey>> getKeyCollection;
        public TokenProperty(Func<TPrimaryKey> getAccessor, Action<TPrimaryKey> setAccessor, Func<IEnumerable<TPrimaryKey>> getKeyCollection)
        {
            this.setAccessor = setAccessor;
            this.getAccessor = getAccessor;
            this.getKeyCollection = getKeyCollection;
        }

        public TPrimaryKey Member
        {
            get => getAccessor();
            set
            {

                if (value == null)
                    setAccessor(default(TPrimaryKey));
                else if (IsSeTPrimaryKeyValid(value))
                    setAccessor(value);
            }
        }

        private bool IsSeTPrimaryKeyValid(TPrimaryKey propertyValue)
        {
            if (propertyValue == null)
                return false;
            
            return getKeyCollection().Any(x => x.Equals(propertyValue));
        }

    }
}
