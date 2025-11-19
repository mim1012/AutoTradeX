using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Services.Security;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AutoTrader.Core.Repositories
{
    /// <summary>
    /// 계좌 정보에 대한 데이터베이스 CRUD 작업을 담당하는 Repository
    /// - API 키 자동 암호화/복호화 처리
    /// </summary>
    public class AccountRepository
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public AccountRepository(AppDbContext context, IEncryptionService encryptionService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        /// <summary>
        /// 모든 계좌 조회 (조건식 포함)
        /// - API 키 자동 복호화
        /// </summary>
        public async Task<List<Account>> GetAllAccountsAsync()
        {
            var accounts = await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .OrderBy(a => a.AccountId)
                .ToListAsync();

            // API 키 복호화
            foreach (var account in accounts)
            {
                DecryptAccount(account);
            }

            return accounts;
        }

        /// <summary>
        /// 특정 계좌 조회 (ID 기준)
        /// - API 키 자동 복호화
        /// </summary>
        public async Task<Account?> GetAccountByIdAsync(int accountId)
        {
            var account = await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (account != null)
            {
                DecryptAccount(account);
            }

            return account;
        }

        /// <summary>
        /// 특정 계좌 조회 (계좌번호 기준)
        /// - API 키 자동 복호화
        /// </summary>
        public async Task<Account?> GetAccountByNumberAsync(string accountNumber)
        {
            var account = await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);

            if (account != null)
            {
                DecryptAccount(account);
            }

            return account;
        }

        /// <summary>
        /// 활성 계좌 조회 (최대 1개)
        /// - API 키 자동 복호화
        /// </summary>
        public async Task<Account?> GetActiveAccountAsync()
        {
            var account = await _context.Accounts
                .Include(a => a.ConditionSet)
                    .ThenInclude(cs => cs.Conditions.OrderBy(c => c.ConditionOrder))
                .FirstOrDefaultAsync(a => a.IsActive);

            if (account != null)
            {
                DecryptAccount(account);
            }

            return account;
        }

        /// <summary>
        /// 계좌 추가
        /// </summary>
        public async Task<Account> AddAccountAsync(Account account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            // 트랜잭션 시작
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 계좌번호 중복 확인
                var existing = await GetAccountByNumberAsync(account.AccountNumber);
                if (existing != null)
                    throw new InvalidOperationException($"계좌번호 '{account.AccountNumber}'는 이미 등록되어 있습니다.");

                // 활성 계좌로 설정 시 기존 활성 계좌 비활성화
                if (account.IsActive)
                {
                    await DeactivateAllAccountsAsync();
                }

                // API 키 암호화
                EncryptAccount(account);

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                // 트랜잭션 커밋
                await transaction.CommitAsync();

                // 반환 전 복호화
                DecryptAccount(account);

                // 엔티티 추적 해제 (복호화된 값이 DB에 저장되지 않도록)
                _context.Entry(account).State = EntityState.Detached;

                return account;
            }
            catch
            {
                // 롤백
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 계좌 수정
        /// </summary>
        public async Task<Account> UpdateAccountAsync(Account account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            // 트랜잭션 시작
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existing = await _context.Accounts.FindAsync(account.AccountId);
                if (existing == null)
                    throw new InvalidOperationException($"계좌 ID '{account.AccountId}'를 찾을 수 없습니다.");

                // 활성 계좌로 설정 시 기존 활성 계좌 비활성화
                if (account.IsActive && !existing.IsActive)
                {
                    await DeactivateAllAccountsAsync();
                }

                // API 키 암호화
                EncryptAccount(account);

                // 엔티티 업데이트
                _context.Entry(existing).CurrentValues.SetValues(account);
                await _context.SaveChangesAsync();

                // 트랜잭션 커밋
                await transaction.CommitAsync();

                // 반환 전 복호화
                DecryptAccount(existing);

                // 엔티티 추적 해제 (복호화된 값이 DB에 저장되지 않도록)
                _context.Entry(existing).State = EntityState.Detached;

                return existing;
            }
            catch
            {
                // 롤백
                await transaction.RollbackAsync();
                throw;
            }
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

        /// <summary>
        /// 계좌 API 키 암호화 (저장 전 호출)
        /// </summary>
        private void EncryptAccount(Account account)
        {
            if (!string.IsNullOrWhiteSpace(account.AppKey))
            {
                account.AppKey = _encryptionService.Encrypt(account.AppKey);
            }

            if (!string.IsNullOrWhiteSpace(account.AppSecret))
            {
                account.AppSecret = _encryptionService.Encrypt(account.AppSecret);
            }
        }

        /// <summary>
        /// 계좌 API 키 복호화 (조회 후 호출)
        /// </summary>
        private void DecryptAccount(Account account)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(account.AppKey))
                {
                    account.AppKey = _encryptionService.Decrypt(account.AppKey);
                }

                if (!string.IsNullOrWhiteSpace(account.AppSecret))
                {
                    account.AppSecret = _encryptionService.Decrypt(account.AppSecret);
                }
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // 암호화 해독 실패 시 원본 값 유지 (이미 평문이거나 손상된 데이터)
                // 로그만 남기고 예외를 삼킴
            }
        }
    }
}
