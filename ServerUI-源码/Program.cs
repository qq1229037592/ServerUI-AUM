// 程序入口点 —— 启动 WinForms 主窗口，是整个应用的起点
using System;
using System.Windows.Forms;
namespace ServerUI;
static class Program
{
    // [STAThread] 表示该线程使用单线程单元模型（Single-Threaded Apartment），WinForms 应用必须加上此特性才能正常工作
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // Application.Run 启动 Windows 消息循环，显示主窗口并持续监听用户操作，直到窗口关闭程序才退出
        Application.Run(new MainForm());
    }
}
