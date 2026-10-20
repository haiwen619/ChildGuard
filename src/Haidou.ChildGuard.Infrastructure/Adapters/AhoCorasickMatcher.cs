using System;
using System.Collections.Generic;
using System.Linq;
using Haidou.ChildGuard.Application.Interfaces;
using Haidou.ChildGuard.Domain.Auditing.Entities;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public sealed class AhoCorasickMatcher : ISensitiveKeywordMatcher
{
    private sealed class KeywordEntry
    {
        public string Word { get; }
        public SensitiveCategory Category { get; }
        public AuditRiskLevel RiskLevel { get; }

        public KeywordEntry(string word, SensitiveCategory category, AuditRiskLevel riskLevel)
        {
            Word = word;
            Category = category;
            RiskLevel = riskLevel;
        }
    }

    private sealed class Node
    {
        public Dictionary<char, Node> Children { get; } = new();
        public Node? Fail { get; set; }
        public List<KeywordEntry> Outputs { get; } = new();
    }

    private readonly Node _root = new();

    public AhoCorasickMatcher()
    {
        InitializeDictionary();
        BuildFailurePointers();
    }

    private void AddKeyword(string word, SensitiveCategory category, AuditRiskLevel riskLevel)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        word = word.Trim().ToLowerInvariant();
        var current = _root;
        foreach (char c in word)
        {
            if (!current.Children.TryGetValue(c, out var next))
            {
                next = new Node();
                current.Children[c] = next;
            }
            current = next;
        }
        current.Outputs.Add(new KeywordEntry(word, category, riskLevel));
    }

    private void BuildFailurePointers()
    {
        var queue = new Queue<Node>();
        foreach (var child in _root.Children.Values)
        {
            child.Fail = _root;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var kvp in current.Children)
            {
                char c = kvp.Key;
                var child = kvp.Value;

                var fail = current.Fail;
                while (fail != null && !fail.Children.ContainsKey(c))
                {
                    fail = fail.Fail;
                }

                child.Fail = fail?.Children.GetValueOrDefault(c) ?? _root;
                child.Outputs.AddRange(child.Fail.Outputs);
                queue.Enqueue(child);
            }
        }
    }

    private void InitializeDictionary()
    {
        // 1. 成人色情与低俗黑话
        string[] adultKeywords =
        {
            "成人直播", "裸聊", "免费看片", "成人影视", "同城约炮", "精品国产", "色情诱导", 
            "黄色小视频", "换脸福利", "羞羞视频", "福利姬", "私密视频", "在线无码", "av女优",
            "乱伦家庭", "幼女资源", "偷拍自拍", "色情交友", "招嫖上门", "骚货"
        };
        foreach (var kw in adultKeywords) AddKeyword(kw, SensitiveCategory.AdultContent, AuditRiskLevel.High);

        // 2. 涉赌网彩与博彩黑产
        string[] gamblingKeywords =
        {
            "百家乐", "六合彩", "轮盘赌博", "下注返利", "开奖预测", "导师带单", "真人视讯",
            "澳门博彩", "线上娱乐城", "投注赢钱", "时时彩", "皇冠现金网", "德州扑克真钱",
            "充值返现高赔率", "特码包中", "北京赛车", "高频彩", "炸金花真钱"
        };
        foreach (var kw in gamblingKeywords) AddKeyword(kw, SensitiveCategory.Gambling, AuditRiskLevel.Critical);

        // 3. 游戏外挂、租号与私服充值（未成年人沉迷与盗刷重灾区）
        string[] cheatingKeywords =
        {
            "自瞄透视", "防封解封", "实名认证解绑", "租号玩", "代练接单", "私服发布网",
            "无限元宝", "无限钻石", "透视挂", "无后坐力", "科技锁头", "破解版免费充值",
            "一键过人脸", "防沉迷破解", "成人身份证买卖", "租号上分", "开挂辅助"
        };
        foreach (var kw in cheatingKeywords) AddKeyword(kw, SensitiveCategory.GameCheating, AuditRiskLevel.Medium);

        // 4. 极端网络暴力与有害诱导
        string[] bullyingKeywords =
        {
            "人肉开盒", "户籍查询", "极品人渣", "跳楼自杀", "割腕自残", "全家暴毙", "买凶报复",
            "去死吧赶紧自杀", "相约自杀", "人肉搜索下户", "开盒挂人"
        };
        foreach (var kw in bullyingKeywords) AddKeyword(kw, SensitiveCategory.Cyberbullying, AuditRiskLevel.Critical);

        // 5. 异常转账与诈骗欺诈（针对未成年人弱防备心套话）
        string[] payFraudKeywords =
        {
            "扫码领皮肤", "免费领原石", "加客服qq领取", "兼职刷单", "向父母索要验证码", 
            "不要告诉家长", "偷拿银行卡", "微信扫码解封领红包", "免费送限定皮肤", "扫码中奖888"
        };
        foreach (var kw in payFraudKeywords) AddKeyword(kw, SensitiveCategory.SuspiciousPay, AuditRiskLevel.High);
    }

    public IReadOnlyList<MatchedKeywordResult> MatchText(string text)
    {
        var results = new List<MatchedKeywordResult>();
        if (string.IsNullOrWhiteSpace(text)) return results;

        string cleanText = text.ToLowerInvariant();
        var current = _root;

        for (int i = 0; i < cleanText.Length; i++)
        {
            char c = cleanText[i];
            while (current != null && !current.Children.ContainsKey(c))
            {
                current = current.Fail;
            }
            current = current?.Children.GetValueOrDefault(c) ?? _root;

            foreach (var output in current.Outputs)
            {
                // 截取前后 15 字符的上下文 snippet，脱敏呈现
                int start = Math.Max(0, i - output.Word.Length - 10);
                int len = Math.Min(cleanText.Length - start, output.Word.Length + 25);
                string snippet = text.Substring(start, len).Replace("\r", " ").Replace("\n", " ");

                results.Add(new MatchedKeywordResult(
                    output.Word,
                    output.Category,
                    output.RiskLevel,
                    snippet
                ));
            }
        }

        return results;
    }
}
