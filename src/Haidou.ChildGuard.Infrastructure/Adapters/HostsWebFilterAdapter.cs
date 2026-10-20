using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public record BuiltinDomainRule(string Domain, string Category);

public record BlockedRuleModel(
    string Domain,
    string Category,
    bool IsCustom,
    DateTime AddedAtUtc
);

public record ShieldStatsModel(
    int TotalActiveRules,
    int AdultCount,
    int GamblingCount,
    int GameGrayCount,
    int PhishingCount,
    int CustomCount
);

/// <summary>
/// 全系统级 Hosts 网页屏蔽适配器（工业级大规模防护库 + 支持家长自定义黑名单）
/// 涵盖：成人低俗、非法赌博彩票、绕过防沉迷租号外挂黑产、钓鱼盗号皮肤陷阱四大维度
/// 0 浏览器插件依赖，Chrome/Edge/Firefox 全局生效，无痕隐身模式无法穿透
/// </summary>
public class HostsWebFilterAdapter
{
    private const string BlockMarkerBegin = "# --- BEGIN HaidouChildGuard Blocklist ---";
    private const string BlockMarkerEnd = "# --- END HaidouChildGuard Blocklist ---";

    private readonly string _hostsPath;
    private readonly string _customRulesPath;

    public HostsWebFilterAdapter(string? hostsPath = null, string? customRulesPath = null)
    {
        _hostsPath = hostsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        
        if (string.IsNullOrEmpty(customRulesPath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string baseFolder = Path.Combine(appData, "HaidouChildGuard");
            Directory.CreateDirectory(baseFolder);
            _customRulesPath = Path.Combine(baseFolder, "custom_blocked_domains.json");
        }
        else
        {
            _customRulesPath = customRulesPath;
        }
    }

    /// <summary>
    /// 预置工业级全维不良网址核心防护库 (四大维度共 150+ 顶级核心域名，展开后 300+ 拦截项)
    /// </summary>
    public static readonly BuiltinDomainRule[] BuiltinRules = new BuiltinDomainRule[]
    {
        new BuiltinDomainRule("pornhub.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("xvideos.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("xnxx.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("xhamster.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("youporn.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("redtube.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("chaturbate.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("onlyfans.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("stripchat.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("livejasmin.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("cam4.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("bongacams.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("spankbang.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("tube8.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("eporner.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("beeg.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("txxx.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("heavy-r.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("daftsex.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("motherless.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("hqporner.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("tnaflix.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("nuvid.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("porn555.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("pornmd.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("thumbzilla.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("vjav.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("javlibrary.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("javbus.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("javdb.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("dmm.co.jp", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("missav.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("jable.tv", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("netflav.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("hanime1.me", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("wnacg.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("e-hentai.org", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("nhentai.net", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("exhentai.org", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("hitomi.la", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("luscious.net", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("rule34.xxx", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("gelbooru.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("danbooru.donmai.us", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("t66y.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("caoliu.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("sis001.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("91porn.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("91prny.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("sexinsex.net", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("hjdang.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("rouse.me", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("biquge.com.co", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("biquge.tv", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("piaotian.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("xszj.org", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("shuedu.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("xsw.tw", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("23wx.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("biqugela.com", "🔞 成人与低俗内容防护"),
        new BuiltinDomainRule("bet365.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("1xbet.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("bovada.lv", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("pokerstars.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("888casino.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("williamhill.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("betway.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("unibet.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("betfair.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("bwin.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("ladbrokes.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("draftkings.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("fanduel.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("stake.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("roobet.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("bc.game", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("rollbit.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("csgoroll.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("csgolive.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("hellcase.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("skinport.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("csgo500.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("gamdom.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("datdrop.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("farmskins.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("key-drop.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("skinbet.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("dota2lounge.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("csgolounge.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("vpgame.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("macau-casino.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("wynnmacau.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("sandsmacao.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("venetianmacao.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("galaxyresorts.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("mgmchina.com.mo", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("lisboamacau.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("168am.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("kj168.net", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("118kj.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("6hch.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("tk188.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("kj998.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("999kj.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("liuhecai.cc", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("cp99.com", "🎲 非法赌博与涉赌网彩防护"),
        new BuiltinDomainRule("zuhaowan.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("zuhaomao.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("dianwan.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("99zuhao.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("yizuhao.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("haomao.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("kuaizuhao.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("u9zuhao.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("haozhuli.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("haobangbang.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("taoshouyou.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("jiaoyimao.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("uu898.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("5173.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("c5game.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("igxe.cn", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("buff.163.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("fz520.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("waigua99.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("fuzhu88.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("wg998.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("wg999.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("cfwaigua.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("lolwaigua.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("pubgcheat.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("unknowncheats.me", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("guidedhacking.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("aimbot.net", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("x22cheats.com", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("engineowning.to", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("battlelog.co", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("hvh.gg", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("wallhack.net", "🎮 绕过防沉迷租号与外挂黑产"),
        new BuiltinDomainRule("free-robux.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("robuxtrick.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("skin-generator.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("genshin-primogems-free.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("freevbucks.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("fortnite-vbucks-generator.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("skin-claimer.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("steam-community-login.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("steamcommunltiy.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("steampowered-gift.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("discord-nitro-free.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("discordapp.gift", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("discord-drop.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("crypto-loot.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("webminerpool.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("free-minecraft-accounts.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("valorant-points-free.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("bilibili-vip-free.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
        new BuiltinDomainRule("iqiyi-vip-crack.com", "🎣 钓鱼欺诈与皮肤免密陷阱"),
    };

    public bool IsFilterActive()
    {
        if (!File.Exists(_hostsPath)) return false;
        try
        {
            string content = File.ReadAllText(_hostsPath);
            return content.Contains(BlockMarkerBegin);
        }
        catch
        {
            return false;
        }
    }

    public List<string> GetCustomDomains()
    {
        if (!File.Exists(_customRulesPath)) return new List<string>();

        try
        {
            string json = File.ReadAllText(_customRulesPath, Encoding.UTF8);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public void SaveCustomDomains(IEnumerable<string> domains)
    {
        try
        {
            var clean = domains
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            string json = JsonSerializer.Serialize(clean, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_customRulesPath, json, Encoding.UTF8);
        }
        catch {}
    }

    public List<BlockedRuleModel> GetAllRules()
    {
        var result = new List<BlockedRuleModel>();

        // 1. 家长自定义规则
        var custom = GetCustomDomains();
        foreach (var c in custom)
        {
            result.Add(new BlockedRuleModel(c, "👤 家长自定义拦截", true, DateTime.UtcNow));
        }

        // 2. 内置核心分类规则
        foreach (var r in BuiltinRules)
        {
            if (!custom.Contains(r.Domain, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(new BlockedRuleModel(r.Domain, r.Category, false, DateTime.UtcNow));
            }
        }

        return result;
    }

    public ShieldStatsModel GetStats()
    {
        var rules = GetAllRules();
        int customCount = rules.Count(r => r.IsCustom);
        int adult = rules.Count(r => !r.IsCustom && r.Category.Contains("成人"));
        int gambling = rules.Count(r => !r.IsCustom && r.Category.Contains("赌博"));
        int gameGray = rules.Count(r => !r.IsCustom && r.Category.Contains("租号"));
        int phishing = rules.Count(r => !r.IsCustom && r.Category.Contains("钓鱼"));
        int total = rules.Count;

        return new ShieldStatsModel(total, adult, gambling, gameGray, phishing, customCount);
    }

    public async Task<bool> AddCustomDomainAsync(string rawDomain)
    {
        string normalized = NormalizeDomain(rawDomain);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        var custom = GetCustomDomains();
        if (!custom.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            custom.Add(normalized);
            SaveCustomDomains(custom);

            if (IsFilterActive())
            {
                await ApplyFilterAsync();
            }
            return true;
        }
        return false;
    }

    public async Task<bool> RemoveCustomDomainAsync(string rawDomain)
    {
        string normalized = NormalizeDomain(rawDomain);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        var custom = GetCustomDomains();
        bool removed = custom.RemoveAll(d => string.Equals(d, normalized, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            SaveCustomDomains(custom);
            if (IsFilterActive())
            {
                await ApplyFilterAsync();
            }
            return true;
        }
        return false;
    }

    public static string NormalizeDomain(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string clean = input.Trim().ToLowerInvariant();

        if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
        if (clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(8);

        int slashIdx = clean.IndexOf('/');
        if (slashIdx >= 0) clean = clean.Substring(0, slashIdx);

        int colonIdx = clean.IndexOf(':');
        if (colonIdx >= 0) clean = clean.Substring(0, colonIdx);

        return clean.Trim();
    }

    public async Task<bool> ApplyFilterAsync()
    {
        try
        {
            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 载入内置规则及对应 www 域
            foreach (var r in BuiltinRules)
            {
                domains.Add(r.Domain);
                if (!r.Domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    domains.Add("www." + r.Domain);
                }
            }

            // 载入家长自定义规则及对应 www 域
            var custom = GetCustomDomains();
            foreach (var d in custom)
            {
                if (!string.IsNullOrWhiteSpace(d))
                {
                    domains.Add(d);
                    if (!d.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        domains.Add("www." + d);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(BlockMarkerBegin);
            sb.AppendLine("# 本规则由 海豆·童伴守护 (Haidou ChildGuard) 自动维护，保护未成年人身心健康");
            sb.AppendLine($"# 更新时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 系统底层屏蔽条目总数: {domains.Count}");
            foreach (var domain in domains)
            {
                sb.AppendLine($"127.0.0.1 {domain}");
                sb.AppendLine($"::1 {domain}");
            }
            sb.AppendLine(BlockMarkerEnd);

            string originalContent = File.Exists(_hostsPath) ? await File.ReadAllTextAsync(_hostsPath) : "";
            string cleanedContent = RemoveFilterBlock(originalContent);

            string finalContent = cleanedContent.TrimEnd() + Environment.NewLine + Environment.NewLine + sb.ToString();
            await File.WriteAllTextAsync(_hostsPath, finalContent, Encoding.UTF8);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoveFilterAsync()
    {
        try
        {
            if (!File.Exists(_hostsPath)) return true;

            string content = await File.ReadAllTextAsync(_hostsPath);
            string cleaned = RemoveFilterBlock(content);
            await File.WriteAllTextAsync(_hostsPath, cleaned, Encoding.UTF8);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string RemoveFilterBlock(string content)
    {
        int startIndex = content.IndexOf(BlockMarkerBegin, StringComparison.Ordinal);
        if (startIndex < 0) return content;

        int endIndex = content.IndexOf(BlockMarkerEnd, StringComparison.Ordinal);
        if (endIndex < 0) return content.Substring(0, startIndex);

        string before = content.Substring(0, startIndex);
        string after = content.Substring(endIndex + BlockMarkerEnd.Length);
        return before.TrimEnd() + Environment.NewLine + after.TrimStart();
    }
}
