using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace COMAVI_SA.Repository
{
    public interface IDatabaseRepository
    {
        Task<T> ExecuteScalarProcedureAsync<T>(string procedureName, object parameters = null);
        Task<IEnumerable<T>> ExecuteQueryProcedureAsync<T>(string procedureName, object parameters = null);
        Task<int> ExecuteNonQueryProcedureAsync(string procedureName, object parameters = null);
        Task<Tuple<T1, IEnumerable<T2>>> ExecuteMultipleProcedureAsync<T1, T2>(string procedureName, object parameters = null);
    }

    public class DatabaseRepository : IDatabaseRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseRepository> _logger;
        private readonly IConfiguration _configuration;

        public DatabaseRepository(
            IConfiguration configuration,
            ILogger<DatabaseRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _configuration = configuration;
        }

        // En DatabaseRepository, mejorar la creación de conexiones
        private IDbConnection CreateConnection()
        {
            var connection = new SqlConnection(_connectionString);

            // Configurar políticas de reintento para mayor resiliencia
            var retryStrategy = new ExponentialBackoff(
                maxRetryCount: 3,
                initialDelayMilliseconds: 100,
                maxDelayMilliseconds: 1000
            );

            try
            {
                retryStrategy.Execute(() => connection.Open());
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al abrir conexión a la base de datos después de reintentos");
                throw new ApplicationException("No se pudo establecer conexión con la base de datos", ex);
            }
        }

        public class ExponentialBackoff
        {
            private readonly int _maxRetryCount;
            private readonly int _initialDelayMilliseconds;
            private readonly int _maxDelayMilliseconds;

            public ExponentialBackoff(int maxRetryCount, int initialDelayMilliseconds, int maxDelayMilliseconds)
            {
                _maxRetryCount = maxRetryCount;
                _initialDelayMilliseconds = initialDelayMilliseconds;
                _maxDelayMilliseconds = maxDelayMilliseconds;
            }

            public void Execute(Action operation)
            {
                Execute<object>(() => {
                    operation();
                    return null;
                });
            }

            public T Execute<T>(Func<T> operation)
            {
                var exceptions = new List<Exception>();

                for (int i = 0; i < _maxRetryCount; i++)
                {
                    try
                    {
                        return operation();
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);

                        if (i < _maxRetryCount - 1)
                        {
                            int delay = Math.Min(
                                _initialDelayMilliseconds * (int)Math.Pow(2, i),
                                _maxDelayMilliseconds
                            );

                            // Añadir jitter para evitar sincronización de reintentos
                            delay = delay + new Random().Next(0, delay / 2);

                            Thread.Sleep(delay);
                        }
                    }
                }

                throw new AggregateException("Operation failed after multiple retries", exceptions);
            }
        }

        public async Task<T> ExecuteScalarProcedureAsync<T>(string procedureName, object parameters = null)
        {
            try
            {
                using var connection = CreateConnection();
                var commandTimeout = _configuration.GetValue<int?>("DatabaseSettings:CommandTimeout") ?? 30;

                return await connection.QueryFirstOrDefaultAsync<T>(
                    procedureName,
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: commandTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al ejecutar procedimiento almacenado: {procedureName}");
                throw new DatabaseOperationException($"Error al ejecutar {procedureName}", ex);
            }
        }

        public async Task<IEnumerable<T>> ExecuteQueryProcedureAsync<T>(string procedureName, object parameters = null)
        {
            try
            {
                using var connection = CreateConnection();
                var commandTimeout = _configuration.GetValue<int?>("DatabaseSettings:CommandTimeout") ?? 30;

                return await connection.QueryAsync<T>(
                    procedureName,
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: commandTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al ejecutar procedimiento almacenado: {procedureName}");
                throw new DatabaseOperationException($"Error al ejecutar {procedureName}", ex);
            }
        }

        public async Task<int> ExecuteNonQueryProcedureAsync(string procedureName, object parameters = null)
        {
            try
            {
                using var connection = CreateConnection();
                var commandTimeout = _configuration.GetValue<int?>("DatabaseSettings:CommandTimeout") ?? 30;

                return await connection.ExecuteAsync(
                    procedureName,
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: commandTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al ejecutar procedimiento almacenado: {procedureName}");
                throw new DatabaseOperationException($"Error al ejecutar {procedureName}", ex);
            }
        }

        public async Task<Tuple<T1, IEnumerable<T2>>> ExecuteMultipleProcedureAsync<T1, T2>(
            string procedureName,
            object parameters = null)
        {
            try
            {
                using var connection = CreateConnection();
                var commandTimeout = _configuration.GetValue<int?>("DatabaseSettings:CommandTimeout") ?? 30;

                using var multi = await connection.QueryMultipleAsync(
                    procedureName,
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: commandTimeout);

                var result1 = await multi.ReadFirstOrDefaultAsync<T1>();
                var result2 = await multi.ReadAsync<T2>();

                return Tuple.Create(result1, result2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al ejecutar procedimiento almacenado multiple: {procedureName}");
                throw new DatabaseOperationException($"Error al ejecutar {procedureName}", ex);
            }
        }
    }

    // Excepción personalizada para operaciones de base de datos
    public class DatabaseOperationException : Exception
    {
        public DatabaseOperationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
