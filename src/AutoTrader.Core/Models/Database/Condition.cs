using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrader.Core.Models.Database
{
    /// <summary>
    /// 개별 조건을 저장하는 엔티티 모델
    /// </summary>
    [Table("Conditions")]
    public class Condition
    {
        /// <summary>
        /// 조건 고유 ID (Primary Key)
        /// </summary>
        [Key]
        [Column("ConditionId")]
        public int ConditionId { get; set; }

        /// <summary>
        /// 외래키: ConditionSets.ConditionSetId
        /// </summary>
        [Required]
        [Column("ConditionSetId")]
        public int ConditionSetId { get; set; }

        /// <summary>
        /// 조건 순서 (1, 2, 3, ..., 최대 10)
        /// </summary>
        [Required]
        [Column("ConditionOrder")]
        public int ConditionOrder { get; set; }

        /// <summary>
        /// 조건 타입 (PriceChange, MovingAverage, TradeVolume, PriceComparison)
        /// </summary>
        [Required]
        [Column("ConditionType")]
        [MaxLength(50)]
        public string ConditionType { get; set; } = string.Empty;

        /// <summary>
        /// JSON 형태의 파라미터 (조건 타입별 상이)
        /// </summary>
        [Required]
        [Column("Parameters")]
        public string Parameters { get; set; } = string.Empty;

        /// <summary>
        /// 다음 조건과의 논리 연산자 (AND, OR, 마지막 조건은 NULL)
        /// </summary>
        [Column("LogicOperator")]
        [MaxLength(10)]
        public string? LogicOperator { get; set; }

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
        [ForeignKey("ConditionSetId")]
        public ConditionSet? ConditionSet { get; set; }

        /// <summary>
        /// 조건 정보를 문자열로 반환 (디버깅용)
        /// </summary>
        public override string ToString()
        {
            return $"조건{ConditionOrder} [{ConditionType}]";
        }
    }
}
