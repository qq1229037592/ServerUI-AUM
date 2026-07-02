// 存档管理服务 —— 提供切换/备份/导入/导出/撤销/删除等全部存档操作的核心逻辑
using System.Text;
using System;
using System.Collections.Generic;
using System.IO;
using ServerUI.Models;
namespace ServerUI.Services;
public class ArchiveService
{
    // 切换库目录 —— 存放用户手动保存/导入的存档备份
    private string SwitchDir(string baseDir) => Path.Combine(baseDir, "存档管理", "切换库");
    // 自动备份目录 —— 换挡时自动备份旧档到此目录
    private string BackupDir(string baseDir) => Path.Combine(baseDir, "存档管理", "备份存档");
    // 主存档路径 —— 服务端实际读取的 inventory.db 位置
    private string DbPath(string baseDir) => Path.Combine(baseDir, "ServerS4A12-AUM", "dist", "win-x64", "Data", "inventory.db");
    
    // 列出切换库中所有 .db 存档文件，按修改时间从新到旧排序
    public List<ArchiveEntry> List(string baseDir)
    {
        var dir = SwitchDir(baseDir);
        var list = new List<ArchiveEntry>();
        if (!Directory.Exists(dir)) return list;
        foreach (var f in new DirectoryInfo(dir).GetFiles("*.db"))
        {
            list.Add(new ArchiveEntry { Name = f.Name, FullPath = f.FullName, Size = f.Length, Modified = f.LastWriteTime });
        }
        list.Sort((a, b) => b.Modified.CompareTo(a.Modified));
        return list;
    }
    
    // 统计备份目录中 .db 文件的数量，用于界面显示 "备份数: X"
    public int BackupCount(string baseDir)
    {
        var dir = BackupDir(baseDir);
        if (!Directory.Exists(dir)) return 0;
        return new DirectoryInfo(dir).GetFiles("*.db").Length;
    }
    
    // 获取当前 inventory.db 的信息（文件名 + 智能大小），不存在则返回 "N/A"
    public string CurrentInfo(string baseDir)
    {
        var db = DbPath(baseDir);
        if (!File.Exists(db)) return "N/A";
        var fi = new FileInfo(db);
        return $"{fi.Name} ({(fi.Length >= 1048576 ? (fi.Length / 1048576.0).ToString("F1") + " MB" : (fi.Length / 1024.0).ToString("F1") + " KB")})";
    }
    
    // 换挡操作：先把当前 inventory.db 备份到备份目录，再把来源存档复制覆盖主存档
    public void Swap(string baseDir, string srcPath)
    {
        var bak = BackupDir(baseDir); Directory.CreateDirectory(bak);
        var db = DbPath(baseDir);
        if (File.Exists(db))
        {
            var dst = Path.Combine(bak, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            File.Copy(db, dst, true);
        }
        File.Copy(srcPath, db, true);
    }
    
    // 撤销换挡：把备份目录中最新的那个 backup_*.db 恢复到主存档位置，还原上一步操作
    public bool UndoSwap(string baseDir)
    {
        var bak = BackupDir(baseDir);
        if (!Directory.Exists(bak)) return false;
        var files = new DirectoryInfo(bak).GetFiles("backup_*.db");
        if (files.Length == 0) return false;
        File.Copy(files[files.Length - 1].FullName, DbPath(baseDir), true);
        return true;
    }
    
    // 导出当前存档：把 inventory.db 以指定名称复制到切换库，方便后续换挡
    public void Export(string baseDir, string name)
    {
        var db = DbPath(baseDir);
        if (!File.Exists(db)) return;
        var dest = Path.Combine(SwitchDir(baseDir), name + ".db");
        Directory.CreateDirectory(SwitchDir(baseDir));
        File.Copy(db, dest, true);
    }
    
    // 导入外部 .db 文件到切换库，用于从其他地方获取的存档加入管理
    public void Import(string baseDir, string srcPath)
    {
        var dest = Path.Combine(SwitchDir(baseDir), Path.GetFileName(srcPath));
        Directory.CreateDirectory(SwitchDir(baseDir));
        File.Copy(srcPath, dest, true);
    }
    
    // 删除切换库中指定名称的存档文件
    public void Delete(string baseDir, string name)
    {
        var path = Path.Combine(SwitchDir(baseDir), name);
        if (File.Exists(path)) File.Delete(path);
    }
}
