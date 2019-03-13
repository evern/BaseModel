using BaseModel.Misc;
using BaseModel.ViewModel.Document;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.POCO;
using DevExpress.Xpf.Grid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseModel.ViewModel.Composition
{
    public class AuthorisationToggleViewModel<TPermission, TAttachment, TAttachmentPermission>
        where TPermission : IGuidEntityKey, ISupportSorting
        where TAttachment : IGuidEntityKey
        where TAttachmentPermission : class, ISupportAttachmentPermission, new()
    {
        /// <summary>
        /// Creates a new instance of PROJECTCollectionViewModel as a POCO view model.
        /// </summary>
        /// <param name="getAttachmentFunc">function get retrieve an attachment</param>
        /// <param name="getPermissionCollectionFunc">function to retrieve permission collection</param>
        /// <param name="getAttachmentPermissionViewModel">function to retrieve attachment permission view model for saving and deleting</param>
        /// <returns></returns>
        public static AuthorisationToggleViewModel<TPermission, TAttachment, TAttachmentPermission> Create(Func<TAttachment> getAttachmentFunc, Func<IEnumerable<TPermission>> getPermissionCollectionFunc, Func<ICollectionViewModel<TAttachmentPermission>> getAttachmentPermissionViewModel)
        {
            return ViewModelSource.Create(() => new AuthorisationToggleViewModel<TPermission, TAttachment, TAttachmentPermission>(getAttachmentFunc, getPermissionCollectionFunc, getAttachmentPermissionViewModel));
        }


        readonly Func<TAttachment> _getAttachmentFunc;
        readonly Func<IEnumerable<TPermission>> _getPermissionCollectionFunc;
        readonly Func<ICollectionViewModel<TAttachmentPermission>> _getAttachmentPermissionViewModelFunc;

        /// <summary>
        /// Initializes a new instance of the PermissionToggleViewModel class.
        /// This constructor is declared protected to avoid undesired instantiation of the PermissionToggleViewModel type without the POCO proxy factory.
        /// </summary>
        protected AuthorisationToggleViewModel(Func<TAttachment> getAttachmentFunc, Func<IEnumerable<TPermission>> getPermissionCollectionFunc, Func<ICollectionViewModel<TAttachmentPermission>> getAttachmentPermissionViewModel)
            : base()
        {
            this._getAttachmentFunc = getAttachmentFunc;
            this._getPermissionCollectionFunc = getPermissionCollectionFunc;
            this._getAttachmentPermissionViewModelFunc = getAttachmentPermissionViewModel;
            SelectedAuthorisations = new ObservableCollection<Authorisation<TPermission>>();
        }

        /// <summary>
        /// Refresh user permission based on selected project
        /// </summary>
        public void RefreshPermissions()
        {
            authorisations = null;
            this.RaisePropertyChanged(x => x.Authorisations);
        }

        public void FullRefresh()
        {
            AttachmentPermissionsViewModel.FullRefresh();
            RefreshPermissions();
        }

        /// <summary>
        /// view selected user project permission
        /// </summary>
        public Authorisation<TPermission> SelectedAuthorisation { get; set; }

        /// <summary>
        /// view selected user project permissions
        /// </summary>
        public ObservableCollection<Authorisation<TPermission>> SelectedAuthorisations { get; set; }

        /// <summary>
        /// private member to store selected project user permission
        /// </summary>
        List<Authorisation<TPermission>> authorisations;

        /// <summary>
        /// Property for attachment get function
        /// </summary>
        TAttachment Attachment => _getAttachmentFunc();

        /// <summary>
        /// Property for attachment permissions
        /// </summary>
        IEnumerable<TAttachmentPermission> AttachmentPermissions => _getAttachmentPermissionViewModelFunc().Entities;

        /// <summary>
        /// Property for attachment permission's view model get function
        /// </summary>
        public ICollectionViewModel<TAttachmentPermission> AttachmentPermissionsViewModel => _getAttachmentPermissionViewModelFunc();

        /// <summary>
        /// Property for permission collection get function
        /// </summary>
        IEnumerable<TPermission> Permissions => _getPermissionCollectionFunc();

        /// <summary>
        /// permission as per selected project
        /// </summary>
        public IEnumerable<Authorisation<TPermission>> Authorisations
        {
            get
            {
                if (Attachment == null)
                    return null;

                if (authorisations == null)
                {
                    authorisations = new List<Authorisation<TPermission>>();
                    foreach (TPermission permission in Permissions)
                    {
                        if (AttachmentPermissions.Any(x => x.PermissionKey == permission.GUID && x.AttachmentKey == Attachment.GUID))
                            authorisations.Add(new Authorisation<TPermission>(permission, true));
                        else
                            authorisations.Add(new Authorisation<TPermission>(permission, false));
                    }
                }

                return authorisations.OrderBy(x => x.Entity.SortMember);
            }
        }

        /// <summary>
        /// view event when user click the checkbox to enable/disable access for selected user
        /// </summary>
        public void AuthorisationCellValueChanging(CellValueChangedEventArgs e)
        {
            //don't need to validate fieldname since only this field is changeable in role permission grid control
            if (Attachment != null)
            {
                bool newValue = (bool)e.Value;
                if (newValue)
                {
                    TAttachmentPermission authorisation = CreateNewAuthorisation(Attachment, SelectedAuthorisation);
                    if (authorisation != null)
                        AttachmentPermissionsViewModel.Save(authorisation);
                }
                else
                {
                    TAttachmentPermission authorisation = getAuthorisation(Attachment, SelectedAuthorisation);
                    if(authorisation != null)
                        AttachmentPermissionsViewModel.Delete(authorisation);
                }
            }

            e.Handled = true;
        }

        /// <summary>
        /// get database permissions on permission and attachment
        /// </summary>
        /// <param name="attachment">attachment to search for</param>
        /// <param name="permission">permissoin to search for</param>
        /// <returns>Attachment permission in database</returns>
        private TAttachmentPermission getAuthorisation(TAttachment attachment, Authorisation<TPermission> permission)
        {
            return AttachmentPermissionsViewModel.Entities.FirstOrDefault(x => x.PermissionKey == permission.Entity.GUID && x.AttachmentKey == attachment.GUID);
        }

        /// <summary>
        /// Add new authorisation on permission and attachment
        /// </summary>
        private TAttachmentPermission CreateNewAuthorisation(TAttachment attachment, Authorisation<TPermission> permission)
        {
            if(getAuthorisation(attachment, permission) == null)
            {
                TAttachmentPermission newAttachmentPermission = new TAttachmentPermission();
                newAttachmentPermission.AttachmentKey = attachment.GUID;
                newAttachmentPermission.PermissionKey = permission.Entity.GUID;
                return newAttachmentPermission;
            }

            return null;
        }

        /// <summary>
        /// Remove authorisation on permission and attachment
        /// </summary>
        private void DeleteAuthorisation(TAttachment attachment, Authorisation<TPermission> permission)
        {
            TAttachmentPermission attachmentPermission = getAuthorisation(attachment, permission);
            if (attachmentPermission != null)
                AttachmentPermissionsViewModel.Delete(attachmentPermission);
        }

        /// <summary>
        /// Bulk add new authorisations on selected permissions and selected attachment
        /// </summary>
        public void AuthoriseSelectedPermissions()
        {
            List<TAttachmentPermission> bulkSaveAttachmentPermissions = new List<TAttachmentPermission>();
            foreach(Authorisation<TPermission> selectedPermission in SelectedAuthorisations)
            {
                TAttachmentPermission attachmentPermission = CreateNewAuthorisation(Attachment, selectedPermission);
                if (attachmentPermission != null)
                    bulkSaveAttachmentPermissions.Add(attachmentPermission);
            }

            AttachmentPermissionsViewModel.BulkSave(bulkSaveAttachmentPermissions);
            RefreshPermissions();
        }

        /// <summary>
        /// Bulk delete authorisations on selected permissions and selected attachment
        /// </summary>
        public void DeauthoriseSelectedPermissions()
        {
            List<TAttachmentPermission> bulkDeleteAttachmentPermissions = new List<TAttachmentPermission>();
            foreach (Authorisation<TPermission> selectedPermission in SelectedAuthorisations)
            {
                TAttachmentPermission attachmentPermission = getAuthorisation(Attachment, selectedPermission);
                if (attachmentPermission != null)
                    bulkDeleteAttachmentPermissions.Add(attachmentPermission);
            }

            AttachmentPermissionsViewModel.BaseBulkDelete(bulkDeleteAttachmentPermissions);
            RefreshPermissions();
        }
    }

    public interface ISupportAttachmentPermission
    {
        Guid AttachmentKey { get; set; }
        Guid PermissionKey { get; set; }
    }

    public interface ISupportSorting
    {
        object SortMember { get; }
    }

    public class Authorisation<T>
        where T : ISupportSorting
    {
        public Authorisation(T entity, bool canAccess)
        {
            Entity = entity;
            CanAccess = canAccess;
        }

        public T Entity { get; set; }
        public bool CanAccess { get; set; }
    }
}
