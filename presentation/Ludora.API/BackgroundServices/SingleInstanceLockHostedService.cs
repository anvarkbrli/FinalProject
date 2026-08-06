using System.Data;
using System.Data.Common;
using System.Globalization;

using Ludora.API.Configuration;
using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore;

namespace Ludora.API.BackgroundServices
{
    /// <summary>
    /// Keeps a SQL Server session-level application lock for the lifetime of the API process.
    /// This deliberately prevents a second API replica from starting while SignalR presence,
    /// rate limiting and local file storage are configured for a single instance.
    /// </summary>
    public sealed class SingleInstanceLockHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SingleInstanceSettings _settings;
        private readonly ILogger<SingleInstanceLockHostedService> _logger;

        private IServiceScope? _scope;
        private DbConnection? _connection;

        public SingleInstanceLockHostedService(
            IServiceScopeFactory scopeFactory,
            SingleInstanceSettings settings,
            ILogger<SingleInstanceLockHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_settings.Enabled)
            {
                _logger.LogInformation("Single-instance deployment lock is disabled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_settings.LockResource))
            {
                throw new InvalidOperationException(
                    "Deployment:SingleInstance:LockResource boş ola bilməz.");
            }

            if (_settings.AcquireTimeoutSeconds is < 0 or > 30)
            {
                throw new InvalidOperationException(
                    "Deployment:SingleInstance:AcquireTimeoutSeconds 0-30 aralığında olmalıdır.");
            }

            _scope = _scopeFactory.CreateScope();

            try
            {
                var dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (!dbContext.Database.IsSqlServer())
                {
                    throw new InvalidOperationException(
                        "Single-instance deployment lock hazırda yalnız SQL Server üçün dəstəklənir.");
                }

                _connection = dbContext.Database.GetDbConnection();
                await _connection.OpenAsync(cancellationToken);

                await using var command = _connection.CreateCommand();
                command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Session',
                    @LockTimeout = @lockTimeoutMilliseconds;
                SELECT @result;
                """;
                command.CommandTimeout = Math.Max(5, _settings.AcquireTimeoutSeconds + 5);
                AddParameter(command, "@resource", DbType.String, _settings.LockResource.Trim());
                AddParameter(
                    command,
                    "@lockTimeoutMilliseconds",
                    DbType.Int32,
                    checked(_settings.AcquireTimeoutSeconds * 1000));

                var scalarResult = await command.ExecuteScalarAsync(cancellationToken);
                if (scalarResult is null or DBNull)
                    throw new InvalidOperationException("SQL Server single-instance lock nəticə qaytarmadı.");

                var result = Convert.ToInt32(scalarResult, CultureInfo.InvariantCulture);

                if (result < 0)
                {
                    throw new InvalidOperationException(
                        "Başqa WellPlayd API instansı artıq işləyir və single-instance lock alına bilmədi " +
                        $"(SQL result: {result}). Köhnə instansı dayandırmadan yenisini başlatmayın.");
                }

                _logger.LogInformation(
                    "Single-instance deployment lock acquired. Resource: {LockResource}; SQL result: {Result}",
                    _settings.LockResource,
                    result);
            }
            catch
            {
                ReleaseResources();
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_settings.Enabled || _connection is null)
            {
                ReleaseResources();
                return;
            }

            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    await using var command = _connection.CreateCommand();
                    command.CommandText = """
                    DECLARE @result int;
                    EXEC @result = sys.sp_releaseapplock
                        @Resource = @resource,
                        @LockOwner = 'Session';
                    SELECT @result;
                    """;
                    command.CommandTimeout = 5;
                    AddParameter(command, "@resource", DbType.String, _settings.LockResource.Trim());

                    var scalarResult = await command.ExecuteScalarAsync(cancellationToken);
                    if (scalarResult is null or DBNull)
                    {
                        _logger.LogWarning(
                            "Single-instance deployment lock release returned no SQL result. Resource: {LockResource}",
                            _settings.LockResource);
                        return;
                    }

                    var result = Convert.ToInt32(scalarResult, CultureInfo.InvariantCulture);

                    if (result < 0)
                    {
                        _logger.LogWarning(
                            "Single-instance deployment lock could not be released cleanly. Resource: {LockResource}; SQL result: {Result}",
                            _settings.LockResource,
                            result);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Single-instance deployment lock released. Resource: {LockResource}",
                            _settings.LockResource);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException ||
                                               !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    exception,
                    "Single-instance deployment lock release failed. The SQL session close will release it automatically.");
            }
            finally
            {
                ReleaseResources();
            }
        }

        private static void AddParameter(
            DbCommand command,
            string name,
            DbType dbType,
            object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = dbType;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private void ReleaseResources()
        {
            _connection = null;
            _scope?.Dispose();
            _scope = null;
        }
    }
}
