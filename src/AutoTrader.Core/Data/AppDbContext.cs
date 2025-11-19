using Microsoft.EntityFrameworkCore;
using AutoTrader.Core.Models.Database;
using System;

namespace AutoTrader.Core.Data
{
    /// <summary>
    /// Entity Framework Core DbContext for AutoTradeX
    /// SQLite 데이터베이스 연결 및 엔티티 관리
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// 계좌 테이블
        /// </summary>
        public DbSet<Account> Accounts { get; set; } = null!;

        /// <summary>
        /// 조건식 테이블
        /// </summary>
        public DbSet<ConditionSet> ConditionSets { get; set; } = null!;

        /// <summary>
        /// 조건 테이블
        /// </summary>
        public DbSet<Condition> Conditions { get; set; } = null!;

        /// <summary>
        /// Worker 상태 테이블
        /// </summary>
        public DbSet<WorkerStatus> WorkerStatus { get; set; } = null!;

        /// <summary>
        /// 생성자 (Dependency Injection용)
        /// </summary>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// 모델 구성 (Fluent API)
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Accounts 테이블 설정
            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasKey(e => e.AccountId);
                entity.HasIndex(e => e.AccountNumber).IsUnique();
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.AccountName).HasMaxLength(100);
                entity.Property(e => e.AppKey).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AppSecret).IsRequired().HasMaxLength(500);
                entity.Property(e => e.IsActive).HasDefaultValue(false);
                entity.Property(e => e.ExcludeOwnedStocks).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // 1:1 관계: Account -> ConditionSet
                entity.HasOne(e => e.ConditionSet)
                      .WithOne(cs => cs.Account)
                      .HasForeignKey<ConditionSet>(cs => cs.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ConditionSets 테이블 설정
            modelBuilder.Entity<ConditionSet>(entity =>
            {
                entity.HasKey(e => e.ConditionSetId);
                entity.HasIndex(e => e.AccountId);

                entity.Property(e => e.Name).HasDefaultValue("조건식1").HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // 1:N 관계: ConditionSet -> Conditions
                entity.HasMany(e => e.Conditions)
                      .WithOne(c => c.ConditionSet)
                      .HasForeignKey(c => c.ConditionSetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Conditions 테이블 설정
            modelBuilder.Entity<Condition>(entity =>
            {
                entity.HasKey(e => e.ConditionId);
                entity.HasIndex(e => e.ConditionSetId);
                entity.HasIndex(e => new { e.ConditionSetId, e.ConditionOrder });

                entity.Property(e => e.ConditionType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Parameters).IsRequired();
                entity.Property(e => e.LogicOperator).HasMaxLength(10);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }

        /// <summary>
        /// SaveChanges 오버라이드: UpdatedAt 자동 갱신
        /// </summary>
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        /// <summary>
        /// SaveChangesAsync 오버라이드: UpdatedAt 자동 갱신
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// 엔티티의 UpdatedAt 타임스탬프 자동 갱신
        /// </summary>
        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Entity is Account account)
                {
                    account.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is ConditionSet conditionSet)
                {
                    conditionSet.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is Condition condition)
                {
                    condition.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
