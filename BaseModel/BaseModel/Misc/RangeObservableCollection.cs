using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.Misc
{
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }

        public void AddRange(IEnumerable<T> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            _suppressNotification = true;

            foreach (var item in list)
                Add(item);
            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void InsertRangeBackground(IEnumerable<T> list)
        {
            var backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += backgroundWorker_DoInsert;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
            backgroundWorker.RunWorkerAsync(list);
        }

        public void RemoveRangeBackground(T[] list)
        {
            var backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += backgroundWorker_DoRemove;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
            backgroundWorker.RunWorkerAsync(list);
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void backgroundWorker_DoInsert(object sender, DoWorkEventArgs e)
        {
            var list = (IEnumerable<T>)e.Argument;
            InsertRange(list);
        }

        private void backgroundWorker_DoRemove(object sender, DoWorkEventArgs e)
        {
            var list = (T[])e.Argument;
            RemoveRange(list);
        }

        private void InsertRange(IEnumerable<T> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            _suppressNotification = true;

            foreach (var item in list)
                Insert(0, item);
            _suppressNotification = false;
        }

        public void InvokeCollectionChanged()
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void RemoveRange(T[] list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            _suppressNotification = true;

            for (var i = 0; i < list.Count(); i++)
                Remove(list[i]);

            _suppressNotification = false;
        }
    }
}
