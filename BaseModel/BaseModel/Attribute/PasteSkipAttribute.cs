using System;
using System.ComponentModel.DataAnnotations;

namespace BaseModel.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class PasteSkipAttribute : Attribute
    {
        private string skipString;
        public PasteSkipAttribute(string skipString)
        {
            this.skipString = skipString.ToUpper();
        }

        public string SkipString => skipString;
    }
}