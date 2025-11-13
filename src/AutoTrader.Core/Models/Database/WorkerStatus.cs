using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoTrader.Core.Models.Database
{
    /// <summary>
    /// Worker Service 상태 정보를 저장하는 엔티티 모델
    /// </summary>
    [Table("WorkerStatus")]
    public class WorkerStatus
    {
        /// <summary>
        /// 상태 ID (Primary Key)
        /// </summary>
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>
        /// Worker 실행 여부
        /// </summary>
        [Column("IsRunning")]
        public bool IsRunning { get; set; }

        /// <summary>
        /// 마지막 하트비트 시간 (UTC)
        /// </summary>
        [Column("LastHeartbeat")]
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Top 300 종목 수
        /// </summary>
        [Column("Top300Count")]
        public int Top300Count { get; set; }

        /// <summary>
        /// 조건 충족 후보 종목 수
        /// </summary>
        [Column("CandidateCount")]
        public int CandidateCount { get; set; }

        /// <summary>
        /// 주문 실행 종목 수
        /// </summary>
        [Column("OrderCount")]
        public int OrderCount { get; set; }

        /// <summary>
        /// 마지막 로그 메시지
        /// </summary>
        [Column("LastLog")]
        [MaxLength(500)]
        public string? LastLog { get; set; }

        /// <summary>
        /// 마지막 업데이트 시간 (UTC)
        /// </summary>
        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 상태 정보를 문자열로 반환 (디버깅용)
        /// </summary>
        public override string ToString()
        {
            return $"Worker Status: {(IsRunning ? "Running" : "Stopped")}, Top300={Top300Count}, Candidates={CandidateCount}, Orders={OrderCount}";
        }
    }
}
