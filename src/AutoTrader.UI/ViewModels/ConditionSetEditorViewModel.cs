using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Market;
using AutoTrader.UI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AutoTrader.UI.ViewModels
{
    /// <summary>
    /// 조건식 설정 ViewModel
    /// </summary>
    public class ConditionSetEditorViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;
        private readonly ConditionSetRepository _conditionSetRepository;
        private readonly KisMarketDataService _marketDataService;
        private int? _accountId;
        private int? _conditionSetId;

        // DI 생성자 사용 권장 (현재는 직접 생성)
        public ConditionSetEditorViewModel()
        {
            // Configuration 로드 (실행 파일 위치 기준)
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // DbContext 초기화 (appsettings.json에서 데이터베이스 타입 읽기)
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var databaseType = configuration.GetValue<string>("ConnectionStrings:DatabaseType") ?? "SQLite";

            switch (databaseType.ToUpper())
            {
                case "SQLITE":
                    optionsBuilder.UseSqlite("Data Source=autotrader.db");
                    break;
                case "MYSQL":
                    var mysqlConnectionString = configuration.GetConnectionString("DefaultConnection");
                    var serverVersion = ServerVersion.AutoDetect(mysqlConnectionString);
                    optionsBuilder.UseMySql(mysqlConnectionString, serverVersion);
                    break;
                case "POSTGRESQL":
                case "POSTGRES":
                    var postgresConnectionString = configuration.GetConnectionString("DefaultConnection");
                    optionsBuilder.UseNpgsql(postgresConnectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database type: {databaseType}");
            }

            _dbContext = new AppDbContext(optionsBuilder.Options);

            // Repository 초기화
            _conditionSetRepository = new ConditionSetRepository(_dbContext);

            // Note: MarketDataService는 App.xaml.cs DI Container에서 주입받습니다.
            // 이 기본 생성자는 디자인 타임 및 독립 실행용입니다.
        }

        // DI 생성자 추가
        public ConditionSetEditorViewModel(
            KisMarketDataService marketDataService,
            ConditionSetRepository conditionSetRepository)
        {
            _marketDataService = marketDataService;
            _conditionSetRepository = conditionSetRepository;

            Conditions = new ObservableCollection<ConditionItemViewModel>();
            FormulaTokens = new ObservableCollection<FormulaToken>();
            MatchedStocks = new ObservableCollection<StockPreviewItem>();

            LoadPreviewData();
        }

        #region Properties

        /// <summary>
        /// 조건 목록
        /// </summary>
        public ObservableCollection<ConditionItemViewModel> Conditions { get; } = new ObservableCollection<ConditionItemViewModel>();

        /// <summary>
        /// 조건식 토큰 목록
        /// </summary>
        public ObservableCollection<FormulaToken> FormulaTokens { get; } = new ObservableCollection<FormulaToken>();

        /// <summary>
        /// 조건 충족 종목 미리보기 목록
        /// </summary>
        public ObservableCollection<StockPreviewItem> MatchedStocks { get; } = new ObservableCollection<StockPreviewItem>();

        #endregion

        #region Methods

        /// <summary>
        /// 특정 계좌의 조건식 로드
        /// </summary>
        public async void LoadConditionSet(int accountId)
        {
            _accountId = accountId;

            try
            {
                var conditionSet = await _conditionSetRepository.GetConditionSetByAccountIdAsync(accountId);
                if (conditionSet != null)
                {
                    _conditionSetId = conditionSet.ConditionSetId;

                    // 조건 목록 로드
                    Conditions.Clear();
                    var sortedConditions = conditionSet.Conditions.OrderBy(c => c.ConditionOrder).ToList();

                    foreach (var condition in sortedConditions)
                    {
                        var conditionItem = new ConditionItemViewModel
                        {
                            ConditionId = GetConditionIdLetter(condition.ConditionOrder),
                            ConditionType = condition.ConditionType,
                            Description = GetConditionDescription(condition),
                            IsEnabled = true
                        };
                        Conditions.Add(conditionItem);
                    }

                    // 조건식 동적 생성 및 파싱
                    var formula = BuildFormulaFromConditions(sortedConditions);
                    if (!string.IsNullOrEmpty(formula))
                    {
                        ParseFormula(formula);
                    }
                }
            }
            catch (Exception ex)
            {
                // 로그 출력
                Console.WriteLine($"조건식 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 조건 추가
        /// </summary>
        public void AddCondition(ConditionItemViewModel condition)
        {
            condition.ConditionId = GetConditionIdLetter(Conditions.Count);
            Conditions.Add(condition);
        }

        /// <summary>
        /// 조건 제거
        /// </summary>
        public void RemoveCondition(ConditionItemViewModel condition)
        {
            Conditions.Remove(condition);

            // 조건 ID 재할당
            for (int i = 0; i < Conditions.Count; i++)
            {
                Conditions[i].ConditionId = GetConditionIdLetter(i);
            }

            // 조건식에서도 제거
            RemoveConditionFromFormula(condition.ConditionId);
        }

        /// <summary>
        /// 조건 위로 이동
        /// </summary>
        public void MoveConditionUp(ConditionItemViewModel condition)
        {
            int index = Conditions.IndexOf(condition);
            if (index > 0)
            {
                Conditions.Move(index, index - 1);

                // 조건 ID 재할당
                for (int i = 0; i < Conditions.Count; i++)
                {
                    Conditions[i].ConditionId = GetConditionIdLetter(i);
                }
            }
        }

        /// <summary>
        /// 조건 아래로 이동
        /// </summary>
        public void MoveConditionDown(ConditionItemViewModel condition)
        {
            int index = Conditions.IndexOf(condition);
            if (index < Conditions.Count - 1)
            {
                Conditions.Move(index, index + 1);

                // 조건 ID 재할당
                for (int i = 0; i < Conditions.Count; i++)
                {
                    Conditions[i].ConditionId = GetConditionIdLetter(i);
                }
            }
        }

        /// <summary>
        /// 조건식 토큰 제거
        /// </summary>
        public void RemoveFormulaToken(FormulaToken token)
        {
            FormulaTokens.Remove(token);
        }

        /// <summary>
        /// 연산자 업데이트
        /// </summary>
        public void UpdateOperator(FormulaToken token, string newOperator)
        {
            token.Value = newOperator;
        }

        /// <summary>
        /// 여는 괄호 추가
        /// </summary>
        public void AddOpenParen()
        {
            FormulaTokens.Add(new FormulaToken(FormulaTokenType.OpenParen, "("));
        }

        /// <summary>
        /// 닫는 괄호 추가
        /// </summary>
        public void AddCloseParen()
        {
            FormulaTokens.Add(new FormulaToken(FormulaTokenType.CloseParen, ")"));
        }

        /// <summary>
        /// 마지막 토큰 제거
        /// </summary>
        public void RemoveLastToken()
        {
            if (FormulaTokens.Count > 0)
            {
                FormulaTokens.RemoveAt(FormulaTokens.Count - 1);
            }
        }

        /// <summary>
        /// 조건식 미리보기 생성
        /// </summary>
        public string GetFormulaPreview()
        {
            if (FormulaTokens.Count == 0)
                return "조건식이 비어있습니다";

            var sb = new StringBuilder();
            foreach (var token in FormulaTokens)
            {
                sb.Append(token.Value);
                if (token.Type != FormulaTokenType.OpenParen && token.Type != FormulaTokenType.CloseParen)
                {
                    sb.Append(" ");
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// 조건식 유효성 검사
        /// </summary>
        public bool ValidateFormula()
        {
            // 조건이 없으면 무효
            if (Conditions.Count == 0)
                return false;

            // 토큰이 없으면 무효
            if (FormulaTokens.Count == 0)
                return false;

            // 괄호 균형 검사
            int openCount = FormulaTokens.Count(t => t.Type == FormulaTokenType.OpenParen);
            int closeCount = FormulaTokens.Count(t => t.Type == FormulaTokenType.CloseParen);

            if (openCount != closeCount)
                return false;

            // 연산자 연속 검사
            for (int i = 0; i < FormulaTokens.Count - 1; i++)
            {
                var current = FormulaTokens[i];
                var next = FormulaTokens[i + 1];

                // 연산자가 연속으로 나오면 무효 (예: "A and and B")
                if (current.Type == FormulaTokenType.Operator && next.Type == FormulaTokenType.Operator)
                    return false;

                // 닫는 괄호 다음에 조건 ID가 오면 무효 (예: ")A")
                if (current.Type == FormulaTokenType.CloseParen && next.Type == FormulaTokenType.ConditionId)
                    return false;

                // 조건 ID 다음에 여는 괄호가 오면 무효 (예: "A(")
                if (current.Type == FormulaTokenType.ConditionId && next.Type == FormulaTokenType.OpenParen)
                    return false;
            }

            // 시작과 끝 검사
            var first = FormulaTokens[0];
            var last = FormulaTokens[FormulaTokens.Count - 1];

            // 연산자로 시작하거나 끝나면 무효
            if (first.Type == FormulaTokenType.Operator || last.Type == FormulaTokenType.Operator)
                return false;

            // FormulaParser를 사용한 완전한 구문 검사
            try
            {
                var formula = GetFormulaPreview();
                var availableIds = Conditions.Select(c => c.Id).ToList();
                var parser = new Core.Services.FormulaParser();
                var result = parser.Parse(formula, availableIds);

                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 조건식 저장
        /// </summary>
        public async void SaveConditionSet()
        {
            if (!_accountId.HasValue)
                throw new InvalidOperationException("계좌 ID가 설정되지 않았습니다.");

            // ConditionSet 생성
            var conditionSet = new ConditionSet
            {
                ConditionSetId = _conditionSetId ?? 0,
                AccountId = _accountId.Value,
                Name = "조건식1",
                Conditions = new List<Core.Models.Database.Condition>()
            };

            // Condition 목록 생성
            for (int i = 0; i < Conditions.Count; i++)
            {
                var condition = Conditions[i];
                var dbCondition = new Core.Models.Database.Condition
                {
                    ConditionOrder = i + 1,
                    ConditionType = condition.ConditionType,
                    Parameters = System.Text.Json.JsonSerializer.Serialize(condition.Parameters ?? new Dictionary<string, object>()),
                    LogicOperator = i < Conditions.Count - 1 ? "AND" : null
                };
                conditionSet.Conditions.Add(dbCondition);
            }

            // DB에 저장
            await _conditionSetRepository.UpsertConditionSetAsync(_accountId.Value, conditionSet.Name, conditionSet.Conditions.ToList());
        }

        /// <summary>
        /// 조건 충족 종목 미리보기 데이터 로드
        /// </summary>
        public async void LoadPreviewData()
        {
            if (_marketDataService == null) return;

            try
            {
                // 실제 API 호출하여 거래대금 상위 300 종목 가져오기
                var stocks = await _marketDataService.GetTop300StocksAsync();

                MatchedStocks.Clear();

                if (stocks != null && stocks.Any())
                {
                    // 조건식 평가하여 충족 종목 필터링 (임시로 전체 표시)
                    foreach (var stock in stocks.Take(100)) // 상위 100개만 표시
                    {
                        MatchedStocks.Add(new StockPreviewItem
                        {
                            Rank = stock.Rank,
                            Symbol = stock.Symbol,
                            Name = stock.Name,
                            CurrentPrice = stock.CurrentPrice,
                            ChangePercent = (double)stock.ChangePercent,
                            TradeVolume = stock.TradeVolume
                        });
                    }
                }
                else
                {
                    // API 연결 실패 시 임시 데이터 표시
                    MatchedStocks.Add(new StockPreviewItem
                    {
                        Rank = 0,
                        Symbol = "INFO",
                        Name = "데이터가 없습니다.",
                        CurrentPrice = 0m,
                        ChangePercent = 0,
                        TradeVolume = 0m
                    });
                }
            }
            catch (Exception ex)
            {
                // 오류 발생 시 오류 메시지 표시
                MatchedStocks.Clear();
                MatchedStocks.Add(new StockPreviewItem
                {
                    Rank = 0,
                    Symbol = "ERROR",
                    Name = $"데이터 로드 실패: {ex.Message}",
                    CurrentPrice = 0m,
                    ChangePercent = 0,
                    TradeVolume = 0m
                });
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 조건 순서를 알파벳 ID로 변환
        /// </summary>
        private string GetConditionIdLetter(int order)
        {
            if (order < 0 || order >= 26)
                return "?";

            return ((char)('A' + order)).ToString();
        }

        /// <summary>
        /// 조건 설명 생성
        /// </summary>
        private string GetConditionDescription(Core.Models.Database.Condition condition)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(condition.Parameters))
                {
                    return $"{condition.ConditionType} 조건";
                }

                var parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(condition.Parameters);
                if (parameters == null)
                {
                    return $"{condition.ConditionType} 조건";
                }

                return condition.ConditionType switch
                {
                    "PriceChange" => GeneratePriceChangeDescription(parameters),
                    "MovingAverage" => GenerateMovingAverageDescription(parameters),
                    "TradeVolume" => GenerateTradeVolumeDescription(parameters),
                    "PriceComparison" => GeneratePriceComparisonDescription(parameters),
                    _ => $"{condition.ConditionType} 조건"
                };
            }
            catch (Exception)
            {
                return $"{condition.ConditionType} 조건";
            }
        }

        private string GeneratePriceChangeDescription(Dictionary<string, JsonElement> parameters)
        {
            var candleType = parameters.TryGetValue("CandleType", out var ct) ? ct.GetString() : "Daily";
            var minPercent = parameters.TryGetValue("MinPercent", out var min) ? min.GetDecimal() : 0;
            var maxPercent = parameters.TryGetValue("MaxPercent", out var max) ? max.GetDecimal() : 0;

            var candleTypeKor = candleType switch
            {
                "Daily" => "일봉",
                "Weekly" => "주봉",
                "Monthly" => "월봉",
                _ => "일봉"
            };

            return $"등락률: [{candleTypeKor}] 0봉 전 종가 대비 {minPercent:F1}% ~ {maxPercent:F1}%";
        }

        private string GenerateMovingAverageDescription(Dictionary<string, JsonElement> parameters)
        {
            var maType = parameters.TryGetValue("MaType", out var mt) ? mt.GetInt32() : 10;
            var maRangeMin = parameters.TryGetValue("MaRangeMin", out var min) ? min.GetDecimal() : 0;
            var maRangeMax = parameters.TryGetValue("MaRangeMax", out var max) ? max.GetDecimal() : 0;

            return $"이동평균: {maType}일 이평선 대비 {maRangeMin:F1}% ~ {maRangeMax:F1}%";
        }

        private string GenerateTradeVolumeDescription(Dictionary<string, JsonElement> parameters)
        {
            var minTradeVolume = parameters.TryGetValue("MinTradeVolume", out var min) ? min.GetDecimal() : 0;

            return $"거래량: 최소 {minTradeVolume:N0}주 이상";
        }

        private string GeneratePriceComparisonDescription(Dictionary<string, JsonElement> parameters)
        {
            var offsetA = parameters.TryGetValue("CandleOffsetA", out var oa) ? oa.GetInt32() : 0;
            var elementA = parameters.TryGetValue("PriceElementA", out var ea) ? ea.GetString() : "Close";
            var op = parameters.TryGetValue("Operator", out var o) ? o.GetString() : ">";
            var offsetB = parameters.TryGetValue("CandleOffsetB", out var ob) ? ob.GetInt32() : 0;
            var elementB = parameters.TryGetValue("PriceElementB", out var eb) ? eb.GetString() : "Close";

            var elementAKor = elementA switch
            {
                "Open" => "시가",
                "High" => "고가",
                "Low" => "저가",
                "Close" => "종가",
                _ => "종가"
            };

            var elementBKor = elementB switch
            {
                "Open" => "시가",
                "High" => "고가",
                "Low" => "저가",
                "Close" => "종가",
                _ => "종가"
            };

            var opKor = op switch
            {
                ">" => "초과",
                ">=" => "이상",
                "<" => "미만",
                "<=" => "이하",
                "==" => "같음",
                _ => "초과"
            };

            return $"가격 비교: {offsetA}봉 전 {elementAKor} {opKor} {offsetB}봉 전 {elementBKor}";
        }

        /// <summary>
        /// DB 조건 목록에서 조건식 문자열 생성
        /// </summary>
        private string BuildFormulaFromConditions(List<Core.Models.Database.Condition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return string.Empty;

            var formulaParts = new List<string>();

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                var conditionId = GetConditionIdLetter(condition.ConditionOrder);

                // 조건 ID 추가
                formulaParts.Add(conditionId);

                // 마지막 조건이 아니면 LogicOperator 추가
                if (i < conditions.Count - 1 && !string.IsNullOrEmpty(condition.LogicOperator))
                {
                    formulaParts.Add(condition.LogicOperator.ToLower());
                }
            }

            return string.Join(" ", formulaParts);
        }

        /// <summary>
        /// 조건식 파싱
        /// </summary>
        private void ParseFormula(string formula)
        {
            FormulaTokens.Clear();

            // 간단한 파싱 로직 (공백 기준)
            var tokens = formula.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (token == "(")
                {
                    FormulaTokens.Add(new FormulaToken(FormulaTokenType.OpenParen, "("));
                }
                else if (token == ")")
                {
                    FormulaTokens.Add(new FormulaToken(FormulaTokenType.CloseParen, ")"));
                }
                else if (token.ToLower() == "and" || token.ToLower() == "or")
                {
                    FormulaTokens.Add(new FormulaToken(FormulaTokenType.Operator, token.ToLower()));
                }
                else if (token.Length == 1 && char.IsLetter(token[0]))
                {
                    FormulaTokens.Add(new FormulaToken(FormulaTokenType.ConditionId, token.ToUpper()));
                }
            }
        }

        /// <summary>
        /// 조건식에서 특정 조건 제거
        /// </summary>
        private void RemoveConditionFromFormula(string conditionId)
        {
            var tokensToRemove = FormulaTokens.Where(t => t.Type == FormulaTokenType.ConditionId && t.Value == conditionId).ToList();
            foreach (var token in tokensToRemove)
            {
                FormulaTokens.Remove(token);
            }
        }

        #endregion
    }
}
