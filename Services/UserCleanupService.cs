using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace COMAVI_SA.Services
{
#nullable disable
#pragma warning disable CS0168

    public interface IUserCleanupService
    {
        Task<int> CleanupNonVerifiedUsersAsync(int diasLimite = 3);
    }

    public class UserCleanupService : IUserCleanupService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public UserCleanupService(
            IConfiguration configuration)
        {
            _configuration = configuration;
#pragma warning disable CS8601 // Possible null reference assignment.
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
#pragma warning restore CS8601 // Possible null reference assignment.
        }

        private IDbConnection CreateConnection()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public async Task<int> CleanupNonVerifiedUsersAsync(int diasLimite = 3)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "sp_LimpiarUsuariosNoVerificados";
                    command.CommandType = CommandType.StoredProcedure;

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@dias_limite";
                    parameter.Value = diasLimite;
                    parameter.DbType = DbType.Int32;
                    command.Parameters.Add(parameter);

                    var resultParameter = command.CreateParameter();
                    resultParameter.ParameterName = "@RETURN_VALUE";
                    resultParameter.Direction = ParameterDirection.ReturnValue;
                    command.Parameters.Add(resultParameter);

                    await ((DbCommand)command).ExecuteNonQueryAsync();

                    var result = Convert.ToInt32(resultParameter.Value);

                    return result;
                }
            }
            catch (Exception ex)
            {
                return -1;
            }
        }
    }
}