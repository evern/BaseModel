using BaseModel.Data.Helpers;
using BaseModel.Misc;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Settings;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.SpellChecker;
using DevExpress.XtraSpellChecker;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BaseModel.ViewModel.Services
{
    public interface IWindowService
    {
        void Show();
        void Hide();
    }

    public class WindowService : ServiceBase, IWindowService
    {
        public DXWindow Window
        {
            get { return (DXWindow)GetValue(WindowProperty); }
            set { SetValue(WindowProperty, value); }
        }
        
        public static readonly DependencyProperty WindowProperty =
            DependencyProperty.Register("Window", typeof(DXWindow), typeof(WindowService), new PropertyMetadata(null));

        public void Hide()
        {
            Window.Hide();
        }

        public void Show()
        {
            Window.Show();
        }
    }
}
