// 更新编排服务 —— 调用 update.ps1 脚本执行增量/全量更新，回传实时日志
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ServerUI.Services;

public class UpdateService
{
    // 实时输出事件：每次 PowerShell 输出一行日志时触发
    public event Action<string> OutputReceived;
    // 更新完成事件：参数 true 表示成功，false 表示失败
    public event Action<bool> Completed;

    // 增量更新：只拉取最近 72 小时内变更的文件，适合日常快速更新
    public async Task RunIncremental(string workDir, string scriptDir)
    {
        await RunPowerShell(workDir, Path.Combine(scriptDir, "update.ps1"), "");
    }

    // 全量更新：拉取全部历史变更（带 -FullSync 参数），适合首次部署或修复异常
    public async Task RunFull(string workDir, string scriptDir)
    {
        await RunPowerShell(workDir, Path.Combine(scriptDir, "update.ps1"), "-FullSync");
    }

    // 核心方法：启动 PowerShell 子进程执行脚本，UTF8 编码，逐行回传输出
    private async Task RunPowerShell(string workDir, string scriptPath, string args)
    {
        if (!File.Exists(scriptPath))
        {
            OutputReceived?.Invoke("[ERROR] Script not found: " + scriptPath);
            Completed?.Invoke(false);
            return;
        }

        var fullArgs = "-NoProfile -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding=[Text.Encoding]::UTF8; & '" + scriptPath + "' -NonInteractive" + (string.IsNullOrEmpty(args) ? "" : " " + args) + "\"";

        await Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = fullArgs,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (s, a) => { if (!string.IsNullOrEmpty(a.Data)) OutputReceived?.Invoke(a.Data); };
            p.ErrorDataReceived += (s, a) => { if (!string.IsNullOrEmpty(a.Data)) OutputReceived?.Invoke("[ERR] " + a.Data); };
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
            Completed?.Invoke(p.ExitCode == 0);
        });
    }

    // 读取更新日志末尾 N 行（默认 40 行），用于在界面上快速查看最近的变更记录
    public string ReadLogTail(string baseDir, int lines = 40)
    {
        var log = Path.Combine(baseDir, "更新日志.txt");
        if (!File.Exists(log)) return "";
        var all = File.ReadAllLines(log, Encoding.UTF8);
        var start = Math.Max(0, all.Length - lines);
        return string.Join("\n", all[start..]);
    }

    // 从更新日志中提取最新版本号 —— 搜索文本中最后一个 "版本:" 标记并截取后续内容
    public string GetVersion(string baseDir)
    {
        var log = Path.Combine(baseDir, "更新日志.txt");
        if (!File.Exists(log)) return "--";
        var text = File.ReadAllText(log, Encoding.UTF8);
        var idx = text.LastIndexOf("版本:");
        if (idx >= 0) { var end = text.IndexOf('\n', idx); if (end < 0) end = Math.Min(idx + 20, text.Length); return text.Substring(idx, end - idx).Trim().Replace("版本:", "").Trim(); }
        return "--";
    }
}
