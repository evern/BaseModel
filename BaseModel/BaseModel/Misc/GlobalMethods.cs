using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.Misc
{
    public static class GlobalMethods
    {
        /// <summary>
        /// bool indicate whether to expand
        /// </summary>
        public static Action<bool> SetAccordionExpandedState { get; set; }
    }
}
