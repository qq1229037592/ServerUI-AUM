/*
 * ==================================================================
 * 存档管理服务 (ArchiveService)
 * ==================================================================
 * 
 * 【功能说明】
 *   提供全部存档操作的核心逻辑：列出、备份、切换、导入、导出、撤销、删除。
 *   所有操作围绕 inventory.db 这个文件，它是服务端的玩家数据库。
 * 
 * 【目录结构】
 *   AUM管理组件\
 *     ├── 存档管理\
 *     │   ├── 切换库\        ← SwitchDir — 用户手动保存/导入的 .db 存档
 *     │   └── 备份存档\       ← BackupDir — 换挡时自动备份旧档
 *     └── ServerS4A12-AUM\
 *         └── dist\win-x64\
 *             └── Data\
 *                 └── inventory.db  ← DbPath — 服务端实际读取的主存档
 * 
 * 【新手修改指南】
 *   - 想改目录名称? 修改 SwitchDir / BackupDir / DbPath 中的路径字符串
 *   - 想改成 MySQL/SQL Server 存储? 需要大幅重构，不建议新手尝试
 *   - 想加新的存档操作? 参考 Swap() 的模式：备份 → 操作 → 确认
 * 
 * 【重要警告】
 *   操作 inventory.db 前必须先停止服务端，否则可能损坏数据库！
 *   MainForm 中的 DoSafeSwap() 已经处理了自动启停逻辑。
 * ==================================================================
 */
using System;
using System.Collections.Generic;
using System.IO;
using ServerUI.Models;

namespace ServerUI.Services;

public class ArchiveService
{
    // ===== 路径计算辅助方法 =====
    // 这些方法根据基准目录(baseDir)计算出各子目录的完整路径。
    // baseDir 通常是 AUM管理组件\ 目录。

    // 切换库目录 — 存放用户保存/导入的 .db 存档备份
    // 示例: "E:\Game\DXF\AUM管理组件\存档管理\切换库"
    private string SwitchDir(string baseDir) =>
        Path.Combine(baseDir, "存档管理", "切换库");

    // 自动备份目录 — 换挡时自动将旧 inventory.db 备份到此
    // 文件命名格式: backup_20260715_143052.db (年月日_时分秒)
    private string BackupDir(string baseDir) =>
        Path.Combine(baseDir, "存档管理", "备份存档");

    // 主存档路径 — 服务端运行时实际使用的 inventory.db 位置
    // 这是唯一的"真存档"，切换操作就是替换这个文件
    // 绝对不要手动删除这个文件，除非你知道自己在做什么
    private string DbPath(string baseDir) =>
        Path.Combine(baseDir, "ServerS4A12-AUM", "dist", "win-x64", "Data", "inventory.db");

    /*
     * 列出切换库中的所有 .db 存档文件
     * 
     * 返回: ArchiveEntry 列表，按修改时间从新到旧排序
     * 用于填充主界面的 ListView 存档列表
     * 
     * 修改建议:
     *   - 想按名称排序? 把 Sort 那一行改成 (a, b) => a.Name.CompareTo(b.Name)
     *   - 想只显示最近 N 个? 在 Sort 后加 .Take(N).ToList()
     *   - 想过滤特定存档? 在 foreach 里加 if 条件
     */
    public List<ArchiveEntry> List(string baseDir)
    {
        var dir = SwitchDir(baseDir);
        var list = new List<ArchiveEntry>();
        if (!Directory.Exists(dir)) return list;

        // 扫描目录中所有 .db 文件
        foreach (var f in new DirectoryInfo(dir).GetFiles("*.db"))
        {
            list.Add(new ArchiveEntry
            {
                Name = f.Name,
                FullPath = f.FullName,
                Size = f.Length,
                Modified = f.LastWriteTime
            });
        }

        // 按修改时间降序（最新的排前面）
        list.Sort((a, b) => b.Modified.CompareTo(a.Modified));
        return list;
    }

    /*
     * 统计备份存档数量
     * 用于界面显示 "备份数: X"
     * 
     * 修改建议:
     *   - 想限制备份数量? 不要在统计里做，去 MainForm.TB() 里改
     */
    public int BackupCount(string baseDir)
    {
        var dir = BackupDir(baseDir);
        if (!Directory.Exists(dir)) return 0;
        return new DirectoryInfo(dir).GetFiles("*.db").Length;
    }

    /*
     * 获取当前 inventory.db 的信息
     * 
     * 返回格式: "inventory.db (15.2 MB)" 或 "inventory.db (512.3 KB)" 或 "N/A"
     * 用于界面显示 "当前: inventory.db (15.2 MB)"
     * 
     * 修改建议:
     *   - 想显示完整路径? 把 fi.Name 改成 db
     *   - 想显示修改时间? 加 fi.LastWriteTime
     *   - 想调整大小格式? 改 F1（一位小数）→ F2（两位小数）
     */
    public string CurrentInfo(string baseDir)
    {
        var db = DbPath(baseDir);
        if (!File.Exists(db)) return "N/A";

        var fi = new FileInfo(db);
        if (fi.Length >= 1048576)
            return $"{fi.Name} ({(fi.Length / 1048576.0):F1} MB)";
        else
            return $"{fi.Name} ({(fi.Length / 1024.0):F1} KB)";
    }

    /*
     * 换挡操作 — 核心功能
     * 
     * 执行步骤:
     *   1. 把当前 inventory.db 备份到 备份存档\ 目录（防止换错）
     *   2. 把来源存档（srcPath）复制并覆盖 inventory.db
     * 
     * 参数:
     *   baseDir — AUM管理组件 目录
     *   srcPath — 来源 .db 文件的完整路径
     * 
     * 【重要】执行此操作前必须先停止服务端！否则数据库可能损坏。
     * MainForm.DoSafeSwap() 已经处理了自动启停。
     * 
     * 修改建议:
     *   - 想保留更多备份? 不要改这里，去 MainForm.TB() 里改 MB 常量
     *   - 想用硬链接代替复制? 把 File.Copy 改成 File.CreateSymbolicLink（NTFS 专用）
     */
    public void Swap(string baseDir, string srcPath)
    {
        var bak = BackupDir(baseDir);
        Directory.CreateDirectory(bak);

        var db = DbPath(baseDir);
        // 如果当前有 inventory.db，先备份
        if (File.Exists(db))
        {
            var dst = Path.Combine(bak, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            File.Copy(db, dst, true);  // true = 允许覆盖已有文件
        }

        // 把来源存档复制为主存档（覆盖）
        File.Copy(srcPath, db, true);
    }

    /*
     * 撤销换挡
     * 
     * 作用: 把备份目录中最新的一次备份恢复到主存档
     * 适用场景: "刚才切错存档了，想换回去"
     * 
     * 返回: true=撤销成功, false=没有可用的备份
     * 
     * 修改建议:
     *   - 想支持多步撤销? 需要改成维护一个撤销栈，复杂度较高
     */
    public bool UndoSwap(string baseDir)
    {
        var bak = BackupDir(baseDir);
        if (!Directory.Exists(bak)) return false;

        // 找到所有备份文件
        var files = new DirectoryInfo(bak).GetFiles("backup_*.db");
        if (files.Length == 0) return false;

        // 最后一个文件是最近的一次备份（按文件名排序，backup_20260715_143052 大于 backup_20260715_143051）
        File.Copy(files[files.Length - 1].FullName, DbPath(baseDir), true);
        return true;
    }

    /*
     * 导出当前存档到切换库
     * 
     * 作用: 把正在使用的 inventory.db 以指定名称保存到切换库
     * 适用场景: "这个存档玩了很久，想存起来以后换回来玩"
     * 
     * 参数:
     *   baseDir — AUM管理组件 目录
     *   name    — 存档名称（不含 .db 后缀，方法会自动加上）
     * 
     * 注意: 如果切换库已有同名文件，会被覆盖（File.Copy 的 true 参数）
     */
    public void Export(string baseDir, string name)
    {
        var db = DbPath(baseDir);
        if (!File.Exists(db)) return;

        // 目标路径: 切换库\名称.db
        var dest = Path.Combine(SwitchDir(baseDir), name + ".db");
        Directory.CreateDirectory(SwitchDir(baseDir));
        File.Copy(db, dest, true);
    }

    /*
     * 导入外部 .db 文件到切换库
     * 
     * 作用: 把其他地方获取的存档加入管理
     * 适用场景: "从群友那里拿到了一个全职业存档，想放进切换库"
     * 
     * 注意: 只是复制到切换库，不会自动切换到该存档。
     *       如果导入后想使用，需要双击列表或拖拽到拖拽区。
     */
    public void Import(string baseDir, string srcPath)
    {
        var dest = Path.Combine(SwitchDir(baseDir), Path.GetFileName(srcPath));
        Directory.CreateDirectory(SwitchDir(baseDir));
        File.Copy(srcPath, dest, true);
    }

    /*
     * 从切换库中删除指定存档
     * 
     * 作用: 删除不需要的存档备份（释放磁盘空间）
     * 注意: 删除操作不可恢复！请确认后再操作。
     *       当前在 MainForm 中未直接暴露此功能（可通过右键操作）
     */
    public void Delete(string baseDir, string name)
    {
        var path = Path.Combine(SwitchDir(baseDir), name);
        if (File.Exists(path)) File.Delete(path);
    }
}
