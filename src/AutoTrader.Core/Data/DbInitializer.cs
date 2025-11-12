using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AutoTrader.Core.Data
{
    /// <summary>
    /// 데이터베이스 초기화 헬퍼 클래스
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// 데이터베이스 초기화 (테이블 생성, 인덱스 생성, 트리거 생성)
        /// </summary>
        public static async Task InitializeAsync(AppDbContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // EF Core Migrations 적용
                await context.Database.MigrateAsync();

                // 또는 SQL 스크립트 직접 실행
                // await ExecuteSqlScriptAsync(context, "init_database.sql");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("데이터베이스 초기화 실패", ex);
            }
        }

        /// <summary>
        /// SQL 스크립트 파일 실행
        /// </summary>
        private static async Task ExecuteSqlScriptAsync(AppDbContext context, string scriptFileName)
        {
            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Scripts", scriptFileName);

            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"SQL 스크립트 파일을 찾을 수 없습니다: {scriptPath}");
            }

            var sql = await File.ReadAllTextAsync(scriptPath);

            // SQLite는 여러 명령을 한 번에 실행할 수 없으므로 분리
            var commands = sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var command in commands)
            {
                var trimmedCommand = command.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedCommand))
                {
                    await context.Database.ExecuteSqlRawAsync(trimmedCommand);
                }
            }
        }

        /// <summary>
        /// 데이터베이스 존재 여부 확인
        /// </summary>
        public static async Task<bool> DatabaseExistsAsync(AppDbContext context)
        {
            return await context.Database.CanConnectAsync();
        }

        /// <summary>
        /// 데이터베이스 삭제 (테스트용)
        /// </summary>
        public static async Task DeleteDatabaseAsync(AppDbContext context)
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}
