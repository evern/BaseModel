using System.ComponentModel.DataAnnotations;

namespace BaseModel.Misc
{
    public enum Arithmetic
    {
        None,
        Add,
        Subtract,
        Multiply,
        Divide
    }

    public enum PasteStatus
    {
        Start,
        Stop
    }

    public enum EnumerationType
    {
        None,
        Decrease,
        Increase
    }

    public enum OperationInterceptMode
    {
        [Display(Name = "Continue operation")]
        Continue,
        [Display(Name = "Discontinue current instance and continue with default database operation")]
        SkipOne,
        [Display(Name = "Discontinue current instance and don't continue with default database operation")]
        SkipOneAndAllDbSaves,
        [Display(Name = "Discontinue subsequent operation")]
        SkipAll
    }
}