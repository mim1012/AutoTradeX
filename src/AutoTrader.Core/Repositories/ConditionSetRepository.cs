using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AutoTrader.Core.Repositories
{
    /// <summary>
    /// 조건식에 대한 데이터베이스 CRUD 작업을 담당하는 Repository
    /// </summary>
    public class ConditionSetRepository
    {
        private readonly AppDbContext _context;

        public ConditionSetRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 특정 계좌의 조건식 조회
        /// </summary>
        public async Task<ConditionSet?> GetConditionSetByAccountIdAsync(int accountId)
        {
            return await _context.ConditionSets
                .Include(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .FirstOrDefaultAsync(cs => cs.AccountId == accountId);
        }

        /// <summary>
        /// 조건식 추가 또는 업데이트 (Upsert)
        /// </summary>
        public async Task<ConditionSet> UpsertConditionSetAsync(int accountId, string name, List<Condition> conditions)
        {
            if (conditions == null)
                throw new ArgumentNullException(nameof(conditions));

            if (conditions.Count > 10)
                throw new InvalidOperationException("조건은 최대 10개까지만 추가할 수 있습니다.");

            // 기존 조건식 조회
            var existingConditionSet = await GetConditionSetByAccountIdAsync(accountId);

            if (existingConditionSet != null)
            {
                // 업데이트: 기존 조건 삭제 후 새로 추가
                _context.Conditions.RemoveRange(existingConditionSet.Conditions);
                existingConditionSet.Name = name;
                existingConditionSet.UpdatedAt = DateTime.UtcNow;

                // 새 조건 추가
                foreach (var condition in conditions)
                {
                    condition.ConditionSetId = existingConditionSet.ConditionSetId;
                    _context.Conditions.Add(condition);
                }

                await _context.SaveChangesAsync();
                return existingConditionSet;
            }
            else
            {
                // 추가: 새 조건식 생성
                var newConditionSet = new ConditionSet
                {
                    AccountId = accountId,
                    Name = name,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ConditionSets.Add(newConditionSet);
                await _context.SaveChangesAsync();

                // 새 조건 추가
                foreach (var condition in conditions)
                {
                    condition.ConditionSetId = newConditionSet.ConditionSetId;
                    _context.Conditions.Add(condition);
                }

                await _context.SaveChangesAsync();
                return newConditionSet;
            }
        }

        /// <summary>
        /// 조건식 삭제
        /// </summary>
        public async Task DeleteConditionSetAsync(int conditionSetId)
        {
            var conditionSet = await _context.ConditionSets
                .Include(cs => cs.Conditions)
                .FirstOrDefaultAsync(cs => cs.ConditionSetId == conditionSetId);

            if (conditionSet == null)
                throw new InvalidOperationException($"조건식 ID '{conditionSetId}'를 찾을 수 없습니다.");

            _context.ConditionSets.Remove(conditionSet);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 특정 계좌의 조건식 삭제
        /// </summary>
        public async Task DeleteConditionSetByAccountIdAsync(int accountId)
        {
            var conditionSet = await GetConditionSetByAccountIdAsync(accountId);
            if (conditionSet != null)
            {
                _context.ConditionSets.Remove(conditionSet);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// 활성 계좌의 조건식 조회
        /// </summary>
        public async Task<ConditionSet?> GetActiveAccountConditionSetAsync()
        {
            var activeAccount = await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .FirstOrDefaultAsync(a => a.IsActive);

            return activeAccount?.ConditionSet;
        }

        /// <summary>
        /// 조건식 존재 여부 확인
        /// </summary>
        public async Task<bool> HasConditionSetAsync(int accountId)
        {
            return await _context.ConditionSets
                .AnyAsync(cs => cs.AccountId == accountId);
        }

        /// <summary>
        /// 다른 계좌의 조건식을 현재 계좌로 복사
        /// </summary>
        /// <param name="sourceAccountId">복사할 원본 계좌 ID</param>
        /// <param name="targetAccountId">복사 대상 계좌 ID</param>
        /// <returns>복사된 조건식</returns>
        public async Task<ConditionSet> CopyConditionSetAsync(int sourceAccountId, int targetAccountId)
        {
            // 원본 조건식 조회
            var sourceConditionSet = await GetConditionSetByAccountIdAsync(sourceAccountId);
            if (sourceConditionSet == null)
                throw new InvalidOperationException($"원본 계좌(ID: {sourceAccountId})에 조건식이 없습니다.");

            // 대상 계좌가 존재하는지 확인
            var targetAccount = await _context.Accounts.FindAsync(targetAccountId);
            if (targetAccount == null)
                throw new InvalidOperationException($"대상 계좌(ID: {targetAccountId})를 찾을 수 없습니다.");

            // 대상 계좌의 기존 조건식 삭제
            await DeleteConditionSetByAccountIdAsync(targetAccountId);

            // 새 조건식 생성 (복사)
            var copiedConditionSet = new ConditionSet
            {
                AccountId = targetAccountId,
                Name = sourceConditionSet.Name + " (복사본)",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ConditionSets.Add(copiedConditionSet);
            await _context.SaveChangesAsync();

            // 조건 복사
            foreach (var sourceCondition in sourceConditionSet.Conditions.OrderBy(c => c.ConditionOrder))
            {
                var copiedCondition = new Condition
                {
                    ConditionSetId = copiedConditionSet.ConditionSetId,
                    ConditionOrder = sourceCondition.ConditionOrder,
                    ConditionType = sourceCondition.ConditionType,
                    Parameters = sourceCondition.Parameters,
                    LogicOperator = sourceCondition.LogicOperator
                };

                _context.Conditions.Add(copiedCondition);
            }

            await _context.SaveChangesAsync();

            // 복사된 조건식 반환 (조건 포함)
            return await GetConditionSetByAccountIdAsync(targetAccountId)
                ?? throw new InvalidOperationException("조건식 복사 중 오류가 발생했습니다.");
        }

        /// <summary>
        /// 모든 조건식 목록 조회 (조건식 복사 시 사용)
        /// </summary>
        public async Task<List<ConditionSet>> GetAllConditionSetsAsync()
        {
            return await _context.ConditionSets
                .Include(cs => cs.Account)
                .Include(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .OrderBy(cs => cs.ConditionSetId)
                .ToListAsync();
        }
    }
}
