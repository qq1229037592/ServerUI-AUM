// 存档条目数据模型 —— 描述切换库中单个 .db 备份文件的基本信息
using System;
namespace ServerUI.Models;
public class ArchiveEntry
{
    // 存档文件名（含 .db 后缀）
    public string Name { get; set; } = "";
    // 存档文件的完整磁盘路径
    public string FullPath { get; set; } = "";
    // 文件大小（字节数）
    public long Size { get; set; }
    // 文件最后修改时间
    public DateTime Modified { get; set; }
    // 智能显示文件大小：大于 1MB 显示 MB，大于 1KB 显示 KB，否则显示 B
    public string SizeDisplay
    {
        get
        {
            if (Size >= 1048576) return $"{(Size / 1048576.0):F1} MB";
            if (Size >= 1024) return $"{(Size / 1024.0):F1} KB";
            return $"{Size} B";
        }
    }
}
