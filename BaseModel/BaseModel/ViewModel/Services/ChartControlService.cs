using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Charts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BaseModel.ViewModel.Services
{
    public interface IChartControlService
    {
        void Animate();
    }

    public class ChartControlService : ServiceBase, IChartControlService
    {
        public ChartControl ChartControl
        {
            get { return (ChartControl)GetValue(ChartControlProperty); }
            set { SetValue(ChartControlProperty, value); }
        }

        public static readonly DependencyProperty ChartControlProperty = DependencyProperty.Register("ChartControl", typeof(ChartControl), typeof(ChartControlService), new PropertyMetadata(null));

        public void Animate()
        {
            if(ChartControl != null)
                ChartControl.Animate();
        }
    }
}
