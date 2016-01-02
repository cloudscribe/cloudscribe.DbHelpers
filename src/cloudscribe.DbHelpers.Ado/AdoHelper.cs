// 2015-12-31 Joe Audette refactored this as an instance class based on the very similar but platform specific
// static helpers that we had in mojoportal and currently have in cloudscribe

//Last Modified:    2016-01-02

// next step will be to make the cloudscribe repository implementations use the new instance class and migrate away from
// the old platform specific static helpers
// some platform versions have extra methods or may have need of overriding some of these methods
// maybe could implement FirebirdHelper to inherit from AdoHelper but add methods and/or override them if needed

// back in mvc 5 this DbProviderFactories was configured in web.config
// and glimpse was able to wrap around it for tracking ado queries
//var factory = DbProviderFactories.GetFactory("System.Data.SqlClient");
// need to figure out how to make this ado code glimpse friendly
// so that glimpse can track the queries.
// I see clues how glimpse hooks into EF here: 
//https://github.com/Glimpse/Glimpse.Prototype/blob/dev/src/Glimpse.Agent.Dnx/Internal/Inspectors/EF/EFDiagnosticsInspector.cs
// and I've asked a question here:
// https://github.com/Glimpse/Glimpse.Prototype/issues/99

// my best guess of what ado.net glimpse integration will look like whenever it gets implemented is
// some wrapper around DbProviderFactory in a decorator patern to track the queries.
// so this new instance class with DbProviderFactory passed in should be ready for Glimpse if I am correct

// not sure at the moment if DbProviderFactory should be added to services in startup and injected
// or if the platform specific repositories should create and pass in the one the need (will do this for now)

// if it were injected would there be a problem to use multiple factories so that different features can use different db platforms
// in that case how could we make sure each repository got the specific DbProviderFactory it needs and not the wrong one that was meant for
// a different feature.


using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace cloudscribe.DbHelpers
{
    public class AdoHelper
    {
        public AdoHelper(DbProviderFactory factory)
        {
            this.factory = factory;
        }

        protected DbProviderFactory factory;

        #region Protected Methods

        protected DbConnection GetConnection(string connectionString)
        { 
            var connection = factory.CreateConnection();
            connection.ConnectionString = connectionString;
            return connection;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        protected void PrepareCommand(
           DbCommand command,
           DbConnection connection,
           DbTransaction transaction,
           CommandType commandType,
           string commandText,
           DbParameter[] commandParameters)
        {
            if (command == null) { throw new ArgumentNullException("command"); }
            if (string.IsNullOrEmpty(commandText)) { throw new ArgumentNullException("commandText"); }

            command.CommandType = commandType;
            command.CommandText = commandText;
            command.Connection = connection;

            if (transaction != null)
            {
                if (transaction.Connection == null) { throw new ArgumentException("The transaction was rollbacked or commited, please provide an open transaction.", "transaction"); }
                command.Transaction = transaction;
            }

            if (commandParameters != null) { AttachParameters(command, commandParameters); }
        }

        protected void AttachParameters(DbCommand command, DbParameter[] commandParameters)
        {
            if (command == null) { throw new ArgumentNullException("command"); }
            if (commandParameters != null)
            {
                foreach (DbParameter p in commandParameters)
                {
                    if (p != null)
                    {
                        if ((p.Direction == ParameterDirection.InputOutput ||
                            p.Direction == ParameterDirection.Input) &&
                            (p.Value == null))
                        {
                            p.Value = DBNull.Value;
                        }
                        command.Parameters.Add(p);
                    }
                }
            }
        }


        #endregion

        #region ExecuteNonQuery 

        public int ExecuteNonQuery(
            string connectionString,
            string commandText,
            DbParameter[] commandParameters)
        {
            return ExecuteNonQuery(
                connectionString, 
                CommandType.Text, 
                commandText, 
                commandParameters);
        }

        public int ExecuteNonQuery(
            string connectionString,
            CommandType commandType,
            string commandText,
            params DbParameter[] commandParameters)
        {
            int commandTimeout = 30; //30 seconds default http://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqlcommand.commandtimeout.aspx
            bool useTransaction = false;
            return ExecuteNonQuery(
                connectionString, 
                commandType, 
                commandText, 
                commandTimeout, 
                useTransaction,
                commandParameters
                );

        }

        public int ExecuteNonQuery(
            string connectionString,
            CommandType commandType,
            string commandText,
            bool useTransaction,
            params DbParameter[] commandParameters)
        {
            int commandTimeout = 30; //30 seconds default http://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqlcommand.commandtimeout.aspx

            return ExecuteNonQuery(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters
                );

        }

        // firebird version of this needs a transaction

        public int ExecuteNonQuery(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            bool useTransaction,
            params DbParameter[] commandParameters)
        {
            if (connectionString == null || connectionString.Length == 0) { throw new ArgumentNullException("connectionString"); }
            
            using (DbConnection connection = GetConnection(connectionString))
            {
                connection.Open();

                DbTransaction transaction = null;
                if (useTransaction) { transaction = connection.BeginTransaction(); }

                using (DbCommand command = factory.CreateCommand())
                {
                    PrepareCommand(
                        command, 
                        connection,
                       transaction, 
                        commandType, 
                        commandText, 
                        commandParameters);

                    command.CommandTimeout = commandTimeout;
                    int result = command.ExecuteNonQuery();

                    if (transaction != null)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = null;

                    }

                    return result;
                }
            }
        }

        public int ExecuteNonQuery(
            DbTransaction transaction,
            CommandType commandType,
            string commandText,
            params DbParameter[] commandParameters)
        {
            int commandTimeout = 30; //30 seconds default

            return ExecuteNonQuery(
                transaction, 
                commandType, 
                commandText, 
                commandTimeout, 
                commandParameters);


        }

        public int ExecuteNonQuery(
            DbTransaction transaction,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            params DbParameter[] commandParameters)
        {
            if (transaction == null) throw new ArgumentNullException("transaction");
            if (transaction != null && transaction.Connection == null) { throw new ArgumentException("The transaction was rollbacked or commited, please provide an open transaction.", "transaction"); }
            
            using (DbCommand command = factory.CreateCommand())
            {
                PrepareCommand(
                    command,
                    transaction.Connection,
                    transaction,
                    commandType,
                    commandText,
                    commandParameters);

                command.CommandTimeout = commandTimeout;

                return command.ExecuteNonQuery();
            }
        }

        public async Task<int> ExecuteNonQueryAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            int commandTimeout = 30; //30 seconds default http://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqlcommand.commandtimeout.aspx
            bool useTransaction = false;

            return await ExecuteNonQueryAsync(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters,
                cancellationToken
                );

        }

        public async Task<int> ExecuteNonQueryAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            bool useTransaction,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            int commandTimeout = 30; //30 seconds default http://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqlcommand.commandtimeout.aspx
            
            return await ExecuteNonQueryAsync(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters,
                cancellationToken);

        }

        public async Task<int> ExecuteNonQueryAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            bool useTransaction = false;

            return await ExecuteNonQueryAsync(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters,
                cancellationToken
                );
        }

       

        // firebird needs a transaction here so added this overload
        // I think other dbs have implicit transactions by default but firebird needs explicit

        public async Task<int> ExecuteNonQueryAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            bool useTransaction,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (connectionString == null || connectionString.Length == 0) { throw new ArgumentNullException("connectionString"); }
            
            using (DbConnection connection = GetConnection(connectionString))
            {
                connection.Open();

                DbTransaction transaction = null;
                if (useTransaction) { transaction = connection.BeginTransaction(); }

                using (DbCommand command = factory.CreateCommand())
                {
                    PrepareCommand(
                        command, 
                        connection,
                        transaction, 
                        commandType, 
                        commandText, 
                        commandParameters
                        );
                    command.CommandTimeout = commandTimeout;
                    int result = await command.ExecuteNonQueryAsync(cancellationToken);

                    if (transaction != null)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = null;

                    }

                    return result;
                }
            }
        }

        public async Task<int> ExecuteNonQueryAsync(
            DbTransaction transaction,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (transaction == null) throw new ArgumentNullException("transaction");
            if (transaction != null && transaction.Connection == null) { throw new ArgumentException("The transaction was rollbacked or commited, please provide an open transaction.", "transaction"); }

            using (DbCommand command = factory.CreateCommand())
            {
                PrepareCommand(
                    command,
                    transaction.Connection,
                    transaction,
                    commandType,
                    commandText,
                    commandParameters);

                command.CommandTimeout = commandTimeout;

                return await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        #endregion

        #region ExecuteReader

        public DbDataReader ExecuteReader(
            string connectionString,
            string commandText,
            params DbParameter[] commandParameters)
        {

            return ExecuteReader(
                connectionString, 
                CommandType.Text, 
                commandText, 
                commandParameters);


        }

        public DbDataReader ExecuteReader(
            string connectionString,
            CommandType commandType,
            string commandText,
            params DbParameter[] commandParameters)
        {
            int commandTimeout = 30; //30 seconds default
            return ExecuteReader(
                connectionString, 
                commandType, 
                commandText, 
                commandTimeout, 
                commandParameters);
        }

        public DbDataReader ExecuteReader(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            params DbParameter[] commandParameters)
        {
            if (connectionString == null || connectionString.Length == 0) { throw new ArgumentNullException("connectionString"); }
            
            // we cannot wrap this connection in a using
            // we need to let the reader close it at using(IDataReader reader = ...
            // otherwise it gets closed before the reader can use it
            DbConnection connection = null;
            try
            {
                //connection = new SqlConnection(connectionString);
                connection = GetConnection(connectionString);

                connection.Open();

                DbCommand command = factory.CreateCommand();
                // this using was causing the connection to be closed before reader could read with firebird
                //using (DbCommand command = factory.CreateCommand())
                //{
                    PrepareCommand(
                        command,
                        connection,
                        null,
                        commandType,
                        commandText,
                        commandParameters);

                if(commandTimeout != 30)
                {
                    command.CommandTimeout = commandTimeout;
                }
                    

                    // 2015-11-10 CommandBehavior.CloseConnection throws exception
                    // should be fixed either in rc1 rc2 or 1.0.0
                    //https://github.com/aspnet/Microsoft.Data.Sqlite/pull/169

                    try
                    {
                        return command.ExecuteReader(CommandBehavior.CloseConnection);
                    }
                    catch(ArgumentException)
                    {
                        return command.ExecuteReader(CommandBehavior.Default);
                    }
                    

                    
                //}

            }
            catch
            {
                if ((connection != null) && (connection.State == ConnectionState.Open)) { connection.Close(); }
                throw;
            }
        }



        public async Task<DbDataReader> ExecuteReaderAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            int commandTimeout = 30; //30 seconds default
            return await ExecuteReaderAsync(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                commandParameters,
                cancellationToken);
        }

        public async Task<DbDataReader> ExecuteReaderAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (connectionString == null || connectionString.Length == 0) { throw new ArgumentNullException("connectionString"); }

            // we cannot wrap this connection in a using
            // we need to let the reader close it at using(IDataReader reader = ...
            // otherwise it gets closed before the reader can use it
            DbConnection connection = null;
            try
            {
                //connection = new SqlConnection(connectionString);
                connection = GetConnection(connectionString);

                connection.Open();
                DbCommand command = factory.CreateCommand();
                //with firebird this using was causing the conneciton to be closed before the reader could read
                //using (DbCommand command = factory.CreateCommand())
                //{
                    PrepareCommand(
                        command,
                        connection,
                        null,
                        commandType,
                        commandText,
                        commandParameters);

                if(commandTimeout != 30)
                {
                    command.CommandTimeout = commandTimeout;
                }
                    

                    DbDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection, cancellationToken);

                    return reader;
                //}
            }
            catch
            {
                if ((connection != null) && (connection.State == ConnectionState.Open)) { connection.Close(); }
                throw;
            }
        }

        #endregion

        public object ExecuteScalar(
            string connectionString,
            string commandText,
            params DbParameter[] commandParameters)
        {
            return ExecuteScalar(
                connectionString,
                CommandType.Text,
                commandText,
                commandParameters);

        }

        public object ExecuteScalar(
            string connectionString,
            CommandType commandType,
            string commandText,
            params DbParameter[] commandParameters)
        {
            int commandTimeout = 30; //30 seconds default
            bool useTransaction = false;
            return ExecuteScalar(
                connectionString, 
                commandType, 
                commandText, 
                commandTimeout, 
                useTransaction,
                commandParameters);

        }

        public object ExecuteScalar(
            string connectionString,
            CommandType commandType,
            string commandText,
            bool useTransaction,
            params DbParameter[] commandParameters)
        {
            int commandTimeout = 30; //30 seconds default
            return ExecuteScalar(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters);

        }

        public object ExecuteScalar(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            params DbParameter[] commandParameters)
        {
            bool useTransaction = false;
            return ExecuteScalar(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters
                );
        }

        // firebird version needs transaction so added this overload

        public object ExecuteScalar(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            bool useTransaction,
            params DbParameter[] commandParameters)
        {
            if (connectionString == null || connectionString.Length == 0) { throw new ArgumentNullException("connectionString"); }
            
            using (DbConnection connection = GetConnection(connectionString))
            {
                connection.Open();

                DbTransaction transaction = null;
                if (useTransaction) { transaction = connection.BeginTransaction(); }

                using (DbCommand command = factory.CreateCommand())
                {
                    PrepareCommand(
                        command, 
                        connection,
                        transaction, 
                        commandType, 
                        commandText, 
                        commandParameters);

                    command.CommandTimeout = commandTimeout;

                    object result = command.ExecuteScalar();

                    if (transaction != null)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = null;

                    }

                    return result;
                }
            }
        }

        public async Task<object> ExecuteScalarAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            int commandTimeout = 30; //30 seconds default
            bool useTransaction = false;
            return await ExecuteScalarAsync(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters,
                cancellationToken);

        }

        public async Task<object> ExecuteScalarAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            bool useTransaction,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            int commandTimeout = 30; //30 seconds default
            return await ExecuteScalarAsync(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters,
                cancellationToken);

        }

        public async Task<object> ExecuteScalarAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            bool useTransaction = false;
            return await ExecuteScalarAsync(
                connectionString,
                commandType,
                commandText,
                commandTimeout,
                useTransaction,
                commandParameters,
                cancellationToken
                );

        }

        // firebird version needs transaction so added this overload

        public async Task<object> ExecuteScalarAsync(
            string connectionString,
            CommandType commandType,
            string commandText,
            int commandTimeout,
            bool useTransaction,
            DbParameter[] commandParameters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (connectionString == null || connectionString.Length == 0) { throw new ArgumentNullException("connectionString"); }
            
            using (DbConnection connection = GetConnection(connectionString))
            {
                await connection.OpenAsync();
                DbTransaction transaction = null;
                if (useTransaction) { transaction = connection.BeginTransaction(); }

                using (DbCommand command = factory.CreateCommand())
                {
                    PrepareCommand(
                        command, 
                        connection,
                        transaction, 
                        commandType, 
                        commandText, 
                        commandParameters);
                    command.CommandTimeout = commandTimeout;

                    object result = await command.ExecuteScalarAsync(cancellationToken);

                    if (transaction != null)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = null;

                    }

                    return result;
                }
            }
        }

    }

}
