using System;

namespace BaseModel.Attributes
{
    public class ConstraintAttributes : Attribute
    {
        public ConstraintAttributes(string columns)
        {
            columns = columns.Replace(" ", string.Empty);
            ColumnNames = columns.Split(',');
        }

        public string[] ColumnNames { get; set; }
    }
}