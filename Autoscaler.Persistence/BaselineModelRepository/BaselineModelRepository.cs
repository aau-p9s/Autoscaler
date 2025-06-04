using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autoscaler.Persistence.Connection;
using Dapper;

namespace Autoscaler.Persistence.BaselineModelRepository
{
    public class BaselineModelRepository : IBaselineModelRepository
    {
        private static readonly string TableName = "BaselineModels";
        private readonly IDbConnectionFactory _connectionFactory;
        private IDbConnection Connection => _connectionFactory.Connection;

        public BaselineModelRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task InsertAllBaselineModels(string modelsRootPath)
        {
            // check if there already are models in the db and if so, skip inserting them
            var existingModels = await Connection.QueryAsync<Guid>($"SELECT Id FROM {TableName}");
            if (existingModels.AsList().Count > 0)
                return;

            string sql =
                $"INSERT INTO {TableName} (Id, Name, Bin, Ckpt, TrainedAt) " +
                "VALUES (@Id, @Name, @Bin, @Ckpt, @TrainedAt)";

            var conn = Connection;
            conn.Open();
            using var tx = conn.BeginTransaction();
            foreach (var folder in Directory.GetDirectories(modelsRootPath))
            {
                var modelName = Path.GetFileName(folder);

                string pthPath = Directory.EnumerateFiles(folder, "*.pth", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => !f.EndsWith(".pth.ckpt", StringComparison.OrdinalIgnoreCase));
                if (pthPath == null)
                    continue;

                var binBytes = await File.ReadAllBytesAsync(pthPath);

                string ckptPath = Directory.EnumerateFiles(folder, "*.pth.ckpt", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
                byte[] ckptBytes = null;
                if (ckptPath != null)
                    ckptBytes = await File.ReadAllBytesAsync(ckptPath);

                var param = new
                {
                    Id = Guid.NewGuid(),
                    Name = modelName,
                    Bin = binBytes,
                    Ckpt = (object)ckptBytes ?? DBNull.Value,
                    TrainedAt = DateTime.UtcNow
                };

                await conn.ExecuteAsync(sql, param, tx);
            }

            tx.Commit();
        }
    }
}