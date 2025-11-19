using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrader.Core.Models.Database
{
    /// <summary>
    /// 한국투자증권 계좌 정보를 저장하는 엔티티 모델
    /// </summary>
    [Table("Accounts")]
    public class Account
    {
        /// <summary>
        /// 계좌 고유 ID (Primary Key)
        /// </summary>
        [Key]
        [Column("AccountId")]
        public int AccountId { get; set; }

        /// <summary>
        /// 한국투자증권 계좌번호 (예: 12345678-01)
        /// </summary>
        [Required]
        [Column("AccountNumber")]
        [MaxLength(20)]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// 사용자 지정 계좌 별칭 (예: "메인 계좌")
        /// </summary>
        [Column("AccountName")]
        [MaxLength(100)]
        public string? AccountName { get; set; }

        /// <summary>
        /// KIS API AppKey
        /// </summary>
        [Required]
        [Column("AppKey")]
        [MaxLength(100)]
        public string AppKey { get; set; } = string.Empty;

        /// <summary>
        /// KIS API AppSecret (암호화 저장 권장)
        /// </summary>
        [Required]
        [Column("AppSecret")]
        [MaxLength(500)]
        public string AppSecret { get; set; } = string.Empty;

        /// <summary>
        /// 활성 계좌 여부 (0: 비활성, 1: 활성, 최대 1개만 1)
        /// </summary>
        [Column("IsActive")]
        public bool IsActive { get; set; } = false;

        /// <summary>
        /// 관심종목 제외 옵션 (true: 보유 종목 재진입 방지)
        /// </summary>
        [Column("ExcludeOwnedStocks")]
        public bool ExcludeOwnedStocks { get; set; } = false;

        /// <summary>
        /// 생성 시간 (ISO 8601 형식)
        /// </summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 수정 시간 (ISO 8601 형식)
        /// </summary>
        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 연결된 조건식 (Navigation Property)
        /// </summary>
        public ConditionSet? ConditionSet { get; set; }

        /// <summary>
        /// 계좌 정보를 문자열로 반환 (디버깅용)
        /// </summary>
        public override string ToString()
        {
            return $"{AccountNumber} ({AccountName})";
        }
    }
}
