﻿using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BaseModel.Misc
{
    /// <summary>
    /// Represents the type of an entity state change notification that is shown when the IUnitOfWork.SaveChanges method has been called.
    /// </summary>
    public enum EntityMessageType
    {
        /// <summary>
        /// A new entity has been added to the unit of work. 
        /// </summary>
        Added,

        /// <summary>
        /// An entity has been removed from the unit of work.
        /// </summary>
        Deleted,

        /// <summary>
        /// One of the entity properties has been modified. 
        /// </summary>
        Changed
    }

    /// <summary>
    /// Provides the information about an entity state change notification that is shown when an entity has been added, removed or modified, and the IUnitOfWork.SaveChanges method has been called.
    /// </summary>
    /// <typeparam name="TEntity">An entity type.</typeparam>
    /// <typeparam name="TPrimaryKey">A primary key value type.</typeparam>
    public class EntityMessage<TEntity, TPrimaryKey>
    {
        /// <summary>
        /// Initializes a new instance of the EntityMessage class.
        /// </summary>
        /// <param name="primaryKey">A primary key of an entity that has been added, removed or modified.</param>
        /// <param name="messageType">An entity state change notification type.</param>
        /// /// <param name="sender">The message sender.</param>
        public EntityMessage(TPrimaryKey primaryKey, Guid key, EntityMessageType messageType, object sender = null, string hwid = "", bool willPerformBulkRefresh = false)
        {
            PrimaryKey = primaryKey;
            MessageType = messageType;
            Sender = sender;
            HWID = hwid;
            Key = key;
            WillPerformBulkRefresh = willPerformBulkRefresh;
        }

        /// <summary>
        /// The primary key of entity that has been added, deleted or modified.
        /// </summary>
        public TPrimaryKey PrimaryKey { get; private set; }

        /// <summary>
        /// The entity state change notification type.
        /// </summary>
        public EntityMessageType MessageType { get; private set; }

        /// <summary>
        /// The message sender.
        /// </summary>
        public object Sender { get; private set; }

        /// <summary>
        /// The messaging machine
        /// </summary>
        public string HWID { get; private set; }

        /// <summary>
        /// The key of the sender
        /// </summary>
        public Guid Key { get; private set; }

        /// <summary>
        /// Sender will perform bulk refresh 
        /// instead of holding up the save operation with individual single entity refreshes
        /// </summary>
        public bool WillPerformBulkRefresh { get; private set; }
    }

    /// <summary>
    /// A message notifying that all view models should save changes. Usually sent by DocumentsViewModel when the SaveAll command is executed.
    /// </summary>
    public class SaveAllMessage
    {
    }

    /// <summary>
    /// A message notifying that all view models should close itself. Usually sent by DocumentsViewModel when the CloseAll command is executed.
    /// </summary>
    public class CloseAllMessage
    {
        private readonly CancelEventArgs cancelEventArgs;
        private Func<object, bool> viewModelPredicate;

        /// <summary>
        /// Initializes a new instance of the CloseAllMessage class.
        /// </summary>
        /// <param name="cancelEventArgs">An argument of the System.ComponentModel.CancelEventArgs type which can be used to cancel closing.</param>
        public CloseAllMessage(CancelEventArgs cancelEventArgs, Func<object, bool> viewModelPredicate)
        {
            this.cancelEventArgs = cancelEventArgs;
            this.viewModelPredicate = viewModelPredicate;
        }

        public bool ShouldProcess(object viewModel)
        {
            return viewModelPredicate(viewModel);
        }

        /// <summary>
        /// Used to cancel closing and check whether the closing has already been cancelled.
        /// </summary>
        public bool Cancel
        {
            get { return cancelEventArgs.Cancel; }
            set { cancelEventArgs.Cancel = value; }
        }
    }

    public class DestroyOrphanedDocumentsMessage
    {
    }

    /// <summary>
    /// Used by the PeekCollectionViewModel to notify that DocumentsViewModel should navigate to the specified module.
    /// </summary>
    /// <typeparam name="TNavigationToken">The navigation token type.</typeparam>
    public class NavigateMessage<TNavigationToken>
    {
        /// <summary>
        /// Initializes a new instance of the NavigateMessage class.
        /// </summary>
        /// <param name="token">An object that is used to identify the module to which the DocumentsViewModel should navigate.</param>
        public NavigateMessage(TNavigationToken token)
        {
            Token = token;
        }

        /// <summary>
        /// An object that is used to identify the module to which the DocumentsViewModel should navigate.
        /// </summary>
        public TNavigationToken Token { get; private set; }
    }

    public class ErrorMessage
    {
        public ErrorMessage(string name, string error, IEnumerable<KeyValuePair<string, string>> constraintIssues = null)
        {
            NAME = name;
            ERROR = error;

            CONSTRAINT_ISSUES = new List<KeyValuePair<string, string>>();
            if(constraintIssues != null)
            {
                CONSTRAINT_ISSUES.AddRange(constraintIssues);
            }
        }

        public string NAME { get; set; }
        public string ERROR { get; set; }
        public List<KeyValuePair<string, string>> CONSTRAINT_ISSUES { get; set; }
    }
}