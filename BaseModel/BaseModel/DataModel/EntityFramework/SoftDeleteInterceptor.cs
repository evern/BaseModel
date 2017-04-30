using BaseModel;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;

namespace BaseModel.DataModel.EntityFramework
{
    public class SoftDeleteInterceptor : IDbCommandTreeInterceptor
    {
        private readonly string DeletedColumnName;
        private readonly string DeletedByColumnName;

        public SoftDeleteInterceptor(string deletedColumnName, string deletedByColumnName)
        {
            DeletedColumnName = deletedColumnName;
            DeletedByColumnName = deletedByColumnName;
        }

        public void TreeCreated(DbCommandTreeInterceptionContext interceptionContext)
        {
            if (interceptionContext.OriginalResult.DataSpace != DataSpace.SSpace)
                return;

            var queryCommand = interceptionContext.Result as DbQueryCommandTree;
            if (queryCommand != null)
                interceptionContext.Result = HandleQueryCommand(queryCommand);

            var deleteCommand = interceptionContext.OriginalResult as DbDeleteCommandTree;
            if (deleteCommand != null)
                interceptionContext.Result = HandleDeleteCommand(deleteCommand);
        }

        private DbCommandTree HandleQueryCommand(DbQueryCommandTree queryCommand)
        {
            var newQuery = queryCommand.Query.Accept(new SoftDeleteQueryVisitor(DeletedColumnName));
            return new DbQueryCommandTree(
                queryCommand.MetadataWorkspace,
                queryCommand.DataSpace,
                newQuery);
        }

        private DbCommandTree HandleDeleteCommand(DbDeleteCommandTree deleteCommand)
        {
            var setClauses = new List<DbModificationClause>();
            var table = (EntityType) deleteCommand.Target.VariableType.EdmType;

            if (table.Properties.All(p => p.Name != DeletedColumnName))
                return deleteCommand;

            DateTime? now = DateTime.Now;
            setClauses.Add(DbExpressionBuilder.SetClause(
                deleteCommand.Target.VariableType.Variable(deleteCommand.Target.VariableName)
                    .Property(DeletedColumnName),
                DbExpression.FromDateTime(now)));

            //setClauses.Add(DbExpressionBuilder.SetClause(
            //    deleteCommand.Target.VariableType.Variable(deleteCommand.Target.VariableName)
            //        .Property(DeletedByColumnName),
            //    DbExpression.FromGuid(LoginCredentials.CurrentUserGuid())));

            return new DbUpdateCommandTree(
                deleteCommand.MetadataWorkspace,
                deleteCommand.DataSpace,
                deleteCommand.Target,
                deleteCommand.Predicate,
                setClauses.AsReadOnly(), null);
        }

        public class SoftDeleteQueryVisitor : DefaultExpressionVisitor
        {
            private readonly string DeletedColumnName;
            public SoftDeleteQueryVisitor(string deletedColumnName)
            {
                DeletedColumnName = deletedColumnName;
            }

            public override DbExpression Visit(DbScanExpression expression)
            {
                var table = (EntityType) expression.Target.ElementType;
                if (table.Properties.All(p => p.Name != DeletedColumnName))
                    return base.Visit(expression);

                var binding = expression.Bind();
                return binding.Filter(
                    binding.VariableType
                        .Variable(binding.VariableName)
                        .Property(DeletedColumnName).IsNull());
            }
        }
    }
}