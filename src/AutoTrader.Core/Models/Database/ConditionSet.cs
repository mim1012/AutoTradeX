using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrader.Core.Models.Database
{
    /// <summary>
    /// 조건식 메타데이터를 저장하는 엔티티 모델
    /// </summary>
    [Table("ConditionSets")]
    public class ConditionSet
    {
        /// <summary>
        /// 조건식 고유 ID (Primary Key)
        /// </summary>
        [Key]
        [Column("ConditionSetId")]
        public int ConditionSetId { get; set; }

        /// <summary>
        /// 외래키: Accounts.AccountId
        /// </summary>
        [Required]
        [Column("AccountId")]
        public int AccountId { get; set; }

        /// <summary>
        /// 조건식 이름
        /// </summary>
        [Column("Name")]
        [MaxLength(100)]
        public string Name { get; set; } = "조건식1";

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
        /// 연결된 계좌 (Navigation Property)
        /// </summary>
        [ForeignKey("AccountId")]
        public Account? Account { get; set; }

        /// <summary>
        /// 조건 목록 (Navigation Property, 최대 10개)
        /// </summary>
        public ICollection<Condition> Conditions { get; set; } = new List<Condition>();

        /// <summary>
        /// 조건식 정보를 문자열로 반환 (디버깅용)
        /// </summary>
        public override string ToString()
        {
            return $"{Name} (조건 {Conditions.Count}개)";
        }
    }
}
