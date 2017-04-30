using System;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;

namespace BaseModel.DataModel.EntityFramework
{
    public class CreatedAndUpdatedDateInterceptor : IDbCommandTreeInterceptor
    {
        public const string CreatedColumnName = "CREATED";
        public const string CreatedByColumnName = "CREATEDBY";
        public const string ModifiedColumnName = "UPDATED";
        public const string ModifiedByColumnName = "UPDATEDBY";

        public void TreeCreated(DbCommandTreeInterceptionContext interceptionContext)
        {
            if (interceptionContext.OriginalResult.DataSpace != DataSpace.SSpace)
                return;

            var insertCommand = interceptionContext.Result as DbInsertCommandTree;
            if (insertCommand != null)
                interceptionContext.Result = HandleInsertCommand(insertCommand);

            var updateCommand = interceptionContext.OriginalResult as DbUpdateCommandTree;
            if (updateCommand != null)
                interceptionContext.Result = HandleUpdateCommand(updateCommand);
        }

        private static DbCommandTree HandleInsertCommand(DbInsertCommandTree insertCommand)
        {
            var now = DateTime.Now;

            var setClauses = insertCommand.SetClauses
                .Select(clause => clause.UpdateIfMatch(CreatedColumnName, DbExpression.FromDateTime(now)))
                //.Select(
                //    clause =>
                //        clause.UpdateIfMatch(CreatedByColumnName,
                //            DbExpression.FromGuid(LoginCredentials.CurrentUserGuid())))
                .ToList();

            return new DbInsertCommandTree(
                insertCommand.MetadataWorkspace,
                insertCommand.DataSpace,
                insertCommand.Target,
                setClauses.AsReadOnly(),
                insertCommand.Returning);
        }

        private static DbCommandTree HandleUpdateCommand(DbUpdateCommandTree updateCommand)
        {
            DateTime? now = DateTime.Now;

            var setClauses = updateCommand.SetClauses
                .Select(clause => clause.UpdateIfMatch(ModifiedColumnName, DbExpression.FromDateTime(now)))
                //.Select(
                //    clause =>
                //        clause.UpdateIfMatch(ModifiedByColumnName,
                //            DbExpression.FromGuid(LoginCredentials.CurrentUserGuid())))
                .ToList();

            //var setClauses = new List<DbModificationClause>();
            //setClauses.Add(DbExpressionBuilder.SetClause(
            //    updateCommand.Target.VariableType.Variable(updateCommand.Target.VariableName).Property(ModifiedColumnName),
            //    DbExpression.FromDateTime(now)));

            //setClauses.Add(DbExpressionBuilder.SetClause(
            //    updateCommand.Target.VariableType.Variable(updateCommand.Target.VariableName).Property(ModifiedByColumnName),
            //    DbExpression.FromGuid(LoginCredentials.CurrentUserGuid())));

            return new DbUpdateCommandTree(
                updateCommand.MetadataWorkspace,
                updateCommand.DataSpace,
                updateCommand.Target,
                updateCommand.Predicate,
                setClauses.AsReadOnly(), null);
        }
    }
}