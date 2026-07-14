/*
 * ==================================================================
 * 更新编排服务 (UpdateService)
 * ==================================================================
 * 
 * 【功能说明】
 *   调用外部 update.ps1 PowerShell 脚本，执行服务端的增量或全量更新。
 *   负责：启动 PowerShell 进程 → 实时回传输出日志 → 通知完成状态。
 * 
 * 【工作流程】
 *   1. MainForm 调用 RunIncremental() 或 RunFull()
 *   2. 本服务启动 powershell.exe 子进程，执行 AUM管理组件\update.ps1
 *   3. 逐行读取 PowerShell 输出，通过 OutputReceived 事件回传给界面
 *   4. 脚本执行完毕，通过 Completed 事件通知成功/失败
 * 
 * 【新手修改指南】
 *   - 想修改更新脚本路径? 改 RunPowerShell 中的 scriptPath 参数
 *   - 想修改日志读取行数? 改 ReadLogTail 的默认参数
 *   - 想禁用更新功能? 在 MainForm 中隐藏 btIn/btFu 按钮即可
 *   - 想更换更新仓库? 去改 update.ps1 中的仓库 URL
 * 
 * 【事件说明】
 *   OutputReceived — 每收到一行 PowerShell 输出时触发（用于日志区域实时显示）
 *   Completed       — 脚本执行完毕后触发，参数 true=成功, false=失败
 * ==================================================================
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ServerUI.Services;

public class UpdateService
{
    // 实时输出事件：每次 PowerShell 输出一行日志时触发
    // MainForm 订阅此事件，将输出显示到界面上的 RichTextBox 日志区域
    public event Action<string> OutputReceived;

    // 更新完成事件：脚本执行完毕后触发
    // MainForm 订阅此事件，用于停止进度条动画并刷新界面
    public event Action<bool> Completed;

    /*
     * 增量更新
     * 作用: 只下载最近 72 小时内变更的文件，速度快，适合日常更新
     * 原理: update.ps1 通过 Codeberg API 获取最近 3 天的 commit，只同步变更的文件
     * 调用时机: 用户点击 [增量更新] 按钮
     * 
     * 参数:
     *   workDir   — PowerShell 的工作目录（ServerS4A12-AUM 目录）
     *   scriptDir — update.ps1 所在目录（AUM管理组件\）
     */
    public async Task RunIncremental(string workDir, string scriptDir)
    {
        // 不带 -FullSync 参数 = 增量模式
        await RunPowerShell(workDir, Path.Combine(scriptDir, "update.ps1"), "");
    }

    /*
     * 全量更新
     * 作用: 下载所有历史变更文件，确保与仓库完全一致，适合首次部署
     * 原理: update.ps1 带上 -FullSync 参数，对比整个仓库历史
     * 调用时机: 用户点击 [全量更新] 按钮
     */
    public async Task RunFull(string workDir, string scriptDir)
    {
        // 带 -FullSync 参数 = 全量模式
        await RunPowerShell(workDir, Path.Combine(scriptDir, "update.ps1"), "-FullSync");
    }

    /*
     * 核心方法：启动 PowerShell 子进程执行 update.ps1
     * 
     * 执行过程:
     *   1. 检查脚本文件是否存在
     *   2. 构造 PowerShell 命令行（UTF8编码 + Bypass执行策略 + 非交互模式）
     *   3. 启动进程，注册 Output/Error 数据接收事件
     *   4. 异步读取输出，逐行触发 OutputReceived
     *   5. 等待进程退出，触发 Completed
     * 
     * 修改建议:
     *   - 想修改超时时间? 在 ProcessStartInfo 中设置 Timeout
     *   - 想隐藏控制台? 已经通过 CreateNoWindow=true 隐藏了
     *   - 想改用 cmd.exe 而不是 PowerShell? 改 FileName 和 Arguments
     */
    private async Task RunPowerShell(string workDir, string scriptPath, string args)
    {
        // 脚本不存在时的处理：通知界面并标记失败
        if (!File.Exists(scriptPath))
        {
            OutputReceived?.Invoke("[ERROR] Script not found: " + scriptPath);
            Completed?.Invoke(false);
            return;
        }

        // 构造 PowerShell 完整命令行
        // -NoProfile: 不加载用户配置（加快启动）
        // -ExecutionPolicy Bypass: 绕过脚本执行限制
        // -Command: 设置 UTF8 输出编码 + 执行脚本
        var fullArgs = "-NoProfile -ExecutionPolicy Bypass -Command \"[Console]::OutputEncoding=[Text.Encoding]::UTF8; & '"
                       + scriptPath + "' -NonInteractive"
                       + (string.IsNullOrEmpty(args) ? "" : " " + args) + "\"";

        // 在后台线程中运行，不阻塞 UI
        await Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = fullArgs,
                WorkingDirectory = workDir,   // PowerShell 的起始目录
                UseShellExecute = false,       // 必须为 false 才能重定向输出
                RedirectStandardOutput = true, // 捕获标准输出
                RedirectStandardError = true,  // 捕获错误输出
                CreateNoWindow = true,         // 不显示 PowerShell 黑窗口
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // 注册数据接收事件：每收到一行输出就触发 OutputReceived
            p.OutputDataReceived += (s, a) =>
            {
                if (!string.IsNullOrEmpty(a.Data))
                    OutputReceived?.Invoke(a.Data);
            };
            p.ErrorDataReceived += (s, a) =>
            {
                if (!string.IsNullOrEmpty(a.Data))
                    OutputReceived?.Invoke("[ERR] " + a.Data);
            };

            p.Start();
            p.BeginOutputReadLine();   // 开始异步读取标准输出
            p.BeginErrorReadLine();    // 开始异步读取错误输出
            p.WaitForExit();           // 等待 PowerShell 进程结束

            // 通过 ExitCode 判断成功或失败
            // 0 = 成功, 非0 = 失败（脚本中 exit 1 时）
            Completed?.Invoke(p.ExitCode == 0);
        });
    }

    /*
     * 读取更新日志末尾 N 行
     * 用于界面上快速查看最近的变更记录（暂未直接使用，保留备用）
     * 
     * 参数:
     *   baseDir — AUM管理组件目录
     *   lines   — 读取行数（默认 40 行）
     */
    public string ReadLogTail(string baseDir, int lines = 40)
    {
        var log = Path.Combine(baseDir, "更新日志.txt");
        if (!File.Exists(log)) return "";
        var all = File.ReadAllLines(log, Encoding.UTF8);
        // 从后往前取 lines 行（如果文件不够 lines 行，从头开始取）
        var start = Math.Max(0, all.Length - lines);
        return string.Join("\n", all[start..]);
    }

    /*
     * 从更新日志中提取最新版本号
     * 搜索逻辑: 在 更新日志.txt 中找到最后一个 "版本:" 标记，截取后面的日期字符串
     * 示例: "版本: 2026-07-15" → 返回 "2026-07-15"
     * 返回 "--" 表示日志文件不存在或未找到版本标记
     */
    public string GetVersion(string baseDir)
    {
        var log = Path.Combine(baseDir, "更新日志.txt");
        if (!File.Exists(log)) return "--";

        var text = File.ReadAllText(log, Encoding.UTF8);

        // 查找最后一个 "版本:" 位置
        var idx = text.LastIndexOf("版本:");
        if (idx >= 0)
        {
            // 截取从 "版本:" 到行尾的内容
            var end = text.IndexOf('\n', idx);
            if (end < 0) end = Math.Min(idx + 20, text.Length);
            return text.Substring(idx, end - idx).Trim().Replace("版本:", "").Trim();
        }

        return "--";
    }
}
