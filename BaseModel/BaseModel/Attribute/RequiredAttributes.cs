using System;

namespace BaseModel.Attributes
{
    public class RequiredAttributes : Attribute
    {
        public RequiredAttributes(string columns)
        {
            columns = columns.Replace(" ", string.Empty);
            ColumnNames = columns.Split(',');
        }

        public string[] ColumnNames { get; set; }
    }
}