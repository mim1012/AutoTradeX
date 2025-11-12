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
    /// 계좌 정보에 대한 데이터베이스 CRUD 작업을 담당하는 Repository
    /// </summary>
    public class AccountRepository
    {
        private readonly AppDbContext _context;

        public AccountRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 모든 계좌 조회 (조건식 포함)
        /// </summary>
        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .OrderBy(a => a.AccountId)
                .ToListAsync();
        }

        /// <summary>
        /// 특정 계좌 조회 (ID 기준)
        /// </summary>
        public async Task<Account?> GetAccountByIdAsync(int accountId)
        {
            return await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        /// <summary>
        /// 특정 계좌 조회 (계좌번호 기준)
        /// </summary>
        public async Task<Account?> GetAccountByNumberAsync(string accountNumber)
        {
            return await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
        }

        /// <summary>
        /// 활성 계좌 조회 (최대 1개)
        /// </summary>
        public async Task<Account?> GetActiveAccountAsync()
        {
            return await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .FirstOrDefaultAsync(a => a.IsActive);
        }

        /// <summary>
        /// 계좌 추가
        /// </summary>
        public async Task<Account> AddAccountAsync(Account account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            // 계좌번호 중복 확인
            var existing = await GetAccountByNumberAsync(account.AccountNumber);
            if (existing != null)
                throw new InvalidOperationException($"계좌번호 '{account.AccountNumber}'는 이미 등록되어 있습니다.");

            // 활성 계좌로 설정 시 기존 활성 계좌 비활성화
            if (account.IsActive)
            {
                await DeactivateAllAccountsAsync();
            }

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return account;
        }

        /// <summary>
        /// 계좌 수정
        /// </summary>
        public async Task<Account> UpdateAccountAsync(Account account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            var existing = await _context.Accounts.FindAsync(account.AccountId);
            if (existing == null)
                throw new InvalidOperationException($"계좌 ID '{account.AccountId}'를 찾을 수 없습니다.");

            // 활성 계좌로 설정 시 기존 활성 계좌 비활성화
            if (account.IsActive && !existing.IsActive)
            {
                await DeactivateAllAccountsAsync();
            }

            // 엔티티 업데이트
            _context.Entry(existing).CurrentValues.SetValues(account);
            await _context.SaveChangesAsync();

            return existing;
        }

        /// <summary>
        /// 계좌 삭제 (연결된 조건식도 CASCADE 삭제)
        /// </summary>
        public async Task DeleteAccountAsync(int accountId)
        {
            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null)
                throw new InvalidOperationException($"계좌 ID '{accountId}'를 찾을 수 없습니다.");

            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 특정 계좌를 활성화 (다른 계좌는 자동 비활성화)
        /// </summary>
        public async Task SetActiveAccountAsync(int accountId)
        {
            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null)
                throw new InvalidOperationException($"계좌 ID '{accountId}'를 찾을 수 없습니다.");

            // 모든 계좌 비활성화
            await DeactivateAllAccountsAsync();

            // 해당 계좌만 활성화
            account.IsActive = true;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 모든 계좌 비활성화
        /// </summary>
        public async Task DeactivateAllAccountsAsync()
        {
            var activeAccounts = await _context.Accounts
                .Where(a => a.IsActive)
                .ToListAsync();

            foreach (var account in activeAccounts)
            {
                account.IsActive = false;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 계좌 개수 조회
        /// </summary>
        public async Task<int> GetAccountCountAsync()
        {
            return await _context.Accounts.CountAsync();
        }

        /// <summary>
        /// 계좌번호 중복 확인
        /// </summary>
        public async Task<bool> IsAccountNumberExistsAsync(string accountNumber)
        {
            return await _context.Accounts
                .AnyAsync(a => a.AccountNumber == accountNumber);
        }
    }
}
