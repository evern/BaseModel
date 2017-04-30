using System;

namespace BaseModel.Attributes
{
    public class BulkEditDisabledAttributes : Attribute
    {
        public BulkEditDisabledAttributes(string columns)
        {
            columns = columns.Replace(" ", string.Empty);
            ColumnNames = columns.Split(',');
        }

        public string[] ColumnNames { get; set; }
    }
}