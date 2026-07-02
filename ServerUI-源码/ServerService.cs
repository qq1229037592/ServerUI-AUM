// 服务端进程管理服务 —— 负责启动/停止 DNF 服务端进程、检测 PVF 资源文件
using System;
using System.Diagnostics;
using System.IO;
namespace ServerUI.Services;
public class ServerService
{
    private Process process;
    // 检测服务端进程是否正在运行中（进程对象存在且未退出）
    public bool IsRunning { get { return process != null && !process.HasExited; } }
    
    // 启动服务端：找到 baseDir 下的 start-server.bat，创建后台进程执行
    public void Start(string baseDir)
    {
        if (IsRunning) return;
        var bat = Path.Combine(baseDir, "start-server.bat");
        if (!File.Exists(bat)) return;
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = bat, WorkingDirectory = baseDir,
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
            }
        };
        process.Start();
    }
    
    // 停止服务端：先杀掉自身启动的进程，再遍历杀掉所有 DfoServer.exe 进程
    public void Stop()
    {
        if (process != null && !process.HasExited) { process.Kill(); process = null; }
        try
        {
            foreach (var p in Process.GetProcessesByName("DfoServer")) { p.Kill(); }
        }
        catch { }
    }
    
    // 检查 PVF 资源文件（Script.pvf）是否存在 —— PVF 是 DNF 服务端的核心数据包
    public bool PvfExists(string baseDir)
    {
        return File.Exists(Path.Combine(baseDir, "dist", "win-x64", "Data", "Pvf", "Script.pvf"));
    }
}
