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

    public enum DeleteInterceptMode
    {
        [Display(Name = "Continue deletion")]
        Continue,
        [Display(Name = "Discontinue only this instance")]
        Skip,
        [Display(Name = "Discontinue subsequent deletion is bulk delete")]
        DiscontinueAll
    }
}