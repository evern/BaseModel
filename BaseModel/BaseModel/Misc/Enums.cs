using System.ComponentModel.DataAnnotations;

namespace BaseModel.Misc
{
    public enum DesignManager
    {
        [Display(Name = "None")] None = 0,
        [Display(Name = "Peter Grigsby")] PeterGrigsby = 1,
        [Display(Name = "Ben Davies")] BenDavies = 2,
        [Display(Name = "Ned Hambling")] NedHambling = 3
    }

    public enum ContractType
    {
        [Display(Name = "Lump Sum")] LumpSum = 0,
        [Display(Name = "Rates")] Rates = 1,
    }

    public enum ProjectType
    {
        [Display(Name = "Design")] Design = 0,
        [Display(Name = "Design & Construct")] DesignConstruct = 1
    }

    public enum ProjectStatus
    {
        [Display(Name = "Active")] Active = 0,
        [Display(Name = "On Hold")] OnHold = 1,
        [Display(Name = "Closed")] Closed = 2,
        [Display(Name = "Tender")] Tender = 3
    }

    public enum BaselineStatus
    {
        [Display(Name = "Superseded")] Superseded = 0,
        [Display(Name = "Working")] Working = 1,
        [Display(Name = "Live")] Live = 2
    }

    public enum EstimationStatus
    {
        [Display(Name = "Superseded")] Superseded = 0,
        [Display(Name = "Working")] Working = 1,
        [Display(Name = "Live")] Live = 2
    }

    public enum DeliverableType
    {
        [Display(Name = "Deliverable")] Deliverable = 0,
        [Display(Name = "Deliverable NCR")] DeliverableNCR = 1,
        [Display(Name = "Task")] Task = 2
    }

    public enum ProgressIntervalType
    {
        [Display(Name = "Daily")] Daily = 1,
        [Display(Name = "Weekly")] Weekly = 7,
        [Display(Name = "Monthly")] Monthly = 30
    }

    public enum ProgressStatus
    {
        [Display(Name = "Superseded")] Superseded = 0,
        [Display(Name = "Working")] Working = 1,
        [Display(Name = "Live")] Live = 2
    }

    public enum VariationAction
    {
        [Display(Name = "No Action")] NoAction = 0,
        [Display(Name = "Append")] Append = 1,
        [Display(Name = "Add")] Add = 2,
        [Display(Name = "Cancel")] Cancel = 3
    }

    public enum BaselineMappingType
    {
        [Display(Name = "Original")] Original = 0,
        [Display(Name = "Modified")] Modified = 1
    }

    public enum DashboardViewType
    {
        Costs,
        Units
    }

    public enum BaselineMappingSelectionType
    {
        None,
        Original,
        Modified
    }

    public enum AppointmentActivityType
    {
        WBS = 0,
        Activity = 1,
        Milestone = 2
    }

    public enum ReportType
    {
        Progress_Report,
        Baseline_Report
    }

    public enum P6TASKSTATUS
    {
        TK_NotStart = 0,
        TK_Active = 1,
        TK_Complete = 2
    }

    public enum Arithmetic
    {
        None,
        Add,
        Subtract,
        Multiply,
        Divide
    }

    public enum DateNavigationType
    {
        Forward,
        Backward,
        Current
    }

    public enum WorkpackType
    {
        Design,
        Supply,
        Install,
        Indirect
    }

    public enum VariationType
    {
        Internal,
        External
    }

    public enum CommodityCodeType
    {
        Direct = 0,
        Design = 1,
        Indirect = 2,
        Overhead = 3
    }

    public enum CostGroup
    {
        [Display(Name = "Offsite")]
        Offsite = 0,
        [Display(Name = "Site")]
        Site = 1,
    }

    public enum PasteStatus
    {
        Start,
        Stop
    }

    public enum COMMODITY_CODEProjectionType
    {
        [Display(Name = "Grouped Project Specific")] ProjectSpecificGrouped,
        [Display(Name = "Not Grouped Project Specific")] ProjectSpecificNotGrouped,
        [Display(Name = "Grouped Not Project Specific")] GeneralGrouped,
        [Display(Name = "Not Grouped Not Project Specific")] GeneralNotGrouped
    }

    public class ReportingEnum
    {
        /// <summary>
        /// Specifies P6 DataPoints Processing Type
        /// </summary>
        public enum DataPointsType
        {
            Planned = 0,
            Earned = 1,
            Remaining = 2
        }

        /// <summary>
        /// Specifies P6 Task Assignment Load Type
        /// </summary>
        public enum AssignmentLoadType
        {
            Original,
            Modified,
            Both
        }
    }


    /// <summary>
    /// Used for optional parameter purpose
    /// </summary>
    public class CommodityCodeTypeClass
    {
        public CommodityCodeType commodityCodeType { get; set; }

        public CommodityCodeTypeClass(CommodityCodeType commodityCodeType)
        {
            this.commodityCodeType = commodityCodeType;
        }
    }

}