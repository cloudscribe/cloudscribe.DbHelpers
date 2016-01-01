// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Author:					Joe Audette
// Created:					2016-01-01
// Last Modified:			2016-01-01
// 

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlServerCe;
using System.Threading.Tasks;

namespace cloudscribe.DbHelpers
{
    public class SqlCeHelper : AdoHelper
    {
        public SqlCeHelper(DbProviderFactory factory) : base(factory)
        {
            // not sure we should throw this, 
            // what if the DbProviderFactory is a wrapper around SqlCeProviderFactory 
            // with decorator pattern, ie for glimpse tracking queries
            //if(!(factory is SqlCeProviderFactory))
            //{ throw new ArgumentException("Expected SqlCeProviderFactory"); }
        }

        public object DoInsertGetIdentitiy(
            string connectionString,
            CommandType commandType,
            string commandText,
            params DbParameter[] commandParameters)
        {
            if (connectionString == null || connectionString.Length == 0) { throw new ArgumentNullException("connectionString"); }
            
            using (DbConnection connection = GetConnection(connectionString))
            {
                connection.Open();
                int rowsAffected = 0;
                using (DbCommand command = factory.CreateCommand())
                {
                    PrepareCommand(command, connection, null, commandType, commandText, commandParameters);
                    rowsAffected = command.ExecuteNonQuery();
                }
                if (rowsAffected == 0) { return -1; }
                using (DbCommand command = factory.CreateCommand())
                {
                    PrepareCommand(command, connection, (DbTransaction)null, CommandType.Text, "SELECT @@IDENTITY", null);
                    return command.ExecuteScalar();
                }
            }
        }

    }
}
