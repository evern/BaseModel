using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;

namespace BaseModel.DataModel.EntityFramework
{
    public class CreatedAndUpdatedDateInterceptor : IDbCommandTreeInterceptor
    {
        private readonly string CreatedColumnName;
        private readonly string CreatedByColumnName;
        private readonly string ModifiedColumnName;
        private readonly string ModifiedByColumnName;
        private readonly List<string> ApplicableContext;
        private readonly Guid? UserGuid;

        public CreatedAndUpdatedDateInterceptor(string createdColumnName, string createdByColumnName, string modifiedColumnName, string modifiedByColumnName, Guid? userGuid = null, List<string> applicableContext = null)
        {
            CreatedColumnName = createdColumnName;
            CreatedByColumnName = createdByColumnName;
            ModifiedColumnName = modifiedColumnName;
            ModifiedByColumnName = modifiedByColumnName;
            UserGuid = userGuid;
            ApplicableContext = applicableContext;
        }

        public void TreeCreated(DbCommandTreeInterceptionContext interceptionContext)
        {
            if(ApplicableContext == null || (interceptionContext.DbContexts.Count() > 0 && ApplicableContext.Any(x => x == interceptionContext.DbContexts.First().GetType().ToString())))
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
        }

        private DbCommandTree HandleInsertCommand(DbInsertCommandTree insertCommand)
        {
            var now = DateTime.Now;

            var setClauses = insertCommand.SetClauses
                .Select(clause => clause.UpdateIfMatch(CreatedColumnName, DbExpression.FromDateTime(now)))
                .Select(
                    clause =>
                        clause.UpdateIfMatch(CreatedByColumnName,
                            DbExpression.FromGuid(UserGuid == null ? Guid.Empty : (Guid)UserGuid)))
                .ToList();

            return new DbInsertCommandTree(
                insertCommand.MetadataWorkspace,
                insertCommand.DataSpace,
                insertCommand.Target,
                setClauses.AsReadOnly(),
                insertCommand.Returning);
        }

        private DbCommandTree HandleUpdateCommand(DbUpdateCommandTree updateCommand)
        {
            DateTime? now = DateTime.Now;
            List<DbModificationClause> setClauses;

            if(updateCommand.SetClauses.Any(x => x.Property().Property.Name == ModifiedColumnName) || updateCommand.SetClauses.Any(x => x.Property().Property.Name == ModifiedByColumnName))
            {
                setClauses = updateCommand.SetClauses.Select(clause => clause
                            .UpdateIfMatch(ModifiedColumnName, DbExpression.FromDateTime(now)))
                            .Select(clause => clause.UpdateIfMatch(ModifiedByColumnName, DbExpression.FromGuid(UserGuid == null ? Guid.Empty : (Guid)UserGuid))).ToList();
            }
            else
            {
                setClauses = updateCommand.SetClauses.ToList();
                setClauses.Add(DbExpressionBuilder.SetClause(
                    updateCommand.Target.VariableType.Variable(updateCommand.Target.VariableName).Property(ModifiedColumnName),
                    DbExpression.FromDateTime(now)));

                setClauses.Add(DbExpressionBuilder.SetClause(
                    updateCommand.Target.VariableType.Variable(updateCommand.Target.VariableName).Property(ModifiedByColumnName),
                    DbExpression.FromGuid(UserGuid == null ? Guid.Empty : (Guid)UserGuid)));
            }

            return new DbUpdateCommandTree(
                updateCommand.MetadataWorkspace,
                updateCommand.DataSpace,
                updateCommand.Target,
                updateCommand.Predicate,
                setClauses.AsReadOnly(), null);
        }
    }
}