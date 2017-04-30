using DevExpress.Mvvm;
using System.Collections.Generic;

namespace BaseModel.Misc
{
    public class PersistentLayoutHelper
    {
        public static string PersistentLogicalLayout
        {
            get { return LayoutSettings.Default.LogicalLayout; }
            set { LayoutSettings.Default.LogicalLayout = value; }
        }

        private static Dictionary<string, string> persistentViewsLayout;

        public static Dictionary<string, string> PersistentViewsLayout
        {
            get
            {
                if (persistentViewsLayout == null)
                    persistentViewsLayout =
                        LogicalLayoutSerializationHelper.Deserialize(LayoutSettings.Default.ViewsLayout);
                return persistentViewsLayout;
            }
        }

        public static void TryDeserializeLayout(ILayoutSerializationService service, string viewName)
        {
            string state = null;
            if (service != null && PersistentViewsLayout.TryGetValue(viewName, out state))
                try
                {
                    service.Deserialize(state);
                }
                catch
                {
                }
        }

        public static void TrySerializeLayout(ILayoutSerializationService service, string viewName)
        {
            if (service != null)
                PersistentViewsLayout[viewName] = service.Serialize();
        }

        public static void SaveLayout()
        {
            LayoutSettings.Default.ViewsLayout = LogicalLayoutSerializationHelper.Serialize(PersistentViewsLayout);
            LayoutSettings.Default.Save();
        }

        public static void ResetLayout(string viewName)
        {
            PersistentViewsLayout[viewName] = string.Empty;
            SaveLayout();
        }
    }
}