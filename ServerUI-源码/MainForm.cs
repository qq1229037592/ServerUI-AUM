// 主窗口类 —— 包含全部 UI 布局、按钮事件、存档管理、服务端更新等核心逻辑
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using ServerUI.Services;

namespace ServerUI;

public partial class MainForm : Form
{
    // ===== UI 配色方案 =====
    // Bg：深色背景  Card：卡片/区块背景  Txt/Txt2：主/次文字色
    // Ac：强调蓝  Gn：成功绿  Rd：错误红  Or：警告橙
    static readonly Color Bg = Color.FromArgb(30, 30, 30), Card = Color.FromArgb(40, 40, 40);
    static readonly Color Txt = Color.FromArgb(220, 220, 220), Txt2 = Color.FromArgb(160, 160, 160);
    static readonly Color Ac = Color.FromArgb(0, 120, 215), Gn = Color.FromArgb(0, 150, 0), Rd = Color.FromArgb(180, 0, 0), Or = Color.FromArgb(200, 120, 0);
    static readonly Color Cy = Color.FromArgb(0, 190, 190);
    // 备份数量上限：超出后自动删除最旧备份
    const int MB = 10;
    const string VER = "1.55";

    // _bd：程序所在基础目录  _ad：AUM管理组件目录（优先使用子目录，否则退回到 _bd）
    readonly string _bd = AppDomain.CurrentDomain.BaseDirectory;
    readonly string _ad;
    readonly string _gr;
    // _sv：服务端进程管理  _ar：存档管理  _up：更新管理 —— 三大服务组件
    readonly ServerService _sv = new();
    readonly ArchiveService _ar = new();
    readonly UpdateService _up = new();

    // ===== UI 控件声明 =====
    Label lbSt, lbVe, lbPv, lbLu, lbCu, lbBk, lbDr, lbSd;
    Button btPlay, btStop, btRe, btIn, btFu, btVL, btPv;
    Button btOD, btOB, btMD, btSC, btIm, btEx, btUd, btCp, btCl, btSdk;
    CheckBox cbDx, cbDt, cbDw;
    ListView lv;
    RichTextBox rt;
    ProgressBar pb;
    Label lbPg;
    Timer _st, _pt, _ct;
    int _pv; bool _sa = true, _cdBusy;

    // 构造函数：初始化窗口属性、检测 AUM 管理组件目录、构建 UI、绑定事件
    public MainForm()
    {
        _ad = Directory.Exists(Path.Combine(_bd, "AUM管理组件")) ? Path.Combine(_bd, "AUM管理组件") : _bd;
        _gr = Directory.GetParent(_ad)?.FullName ?? _ad;
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96F, 96F);
        MinimumSize = new Size(1024, 700); Size = new Size(1200, 820);
        StartPosition = FormStartPosition.CenterScreen; Text = "ServerS4A12 v" + VER;
        BackColor = Bg; Font = new Font("Microsoft YaHei", 10f);
        AllowDrop = true; DragEnter += De; DragDrop += Dd; FormClosing += Fc;
        Build(); Ti(); Load += (s, e) => { Ck(); Rf(); };
    }

    // B() 快捷创建按钮：统一设置扁平化样式、颜色、字号、光标，避免每次都写一堆重复代码
    Button B(string t, Color bg, int fs = 10, bool bd = false) { var b = new Button { Text = t, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = new Font("Microsoft YaHei", fs, bd ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(4), Cursor = Cursors.Hand, UseVisualStyleBackColor = false, FlatAppearance = { BorderSize = 0 } }; b.MinimumSize = new Size(60, 28); return b; }
    // L() 快捷创建标签：透明背景、自动尺寸、指定前景色
    Label L(string t, Color c) => new Label { Text = t, ForeColor = c, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };

    // Build() 构建整个 UI 布局 —— 采用 TableLayoutPanel 4 行结构（顶栏/主区域/日志/底栏）
    void Build()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Bg, Padding = new Padding(6) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 52F)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 48F)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        Controls.Add(root);

        // ===== r0 顶栏：显示服务端运行状态 / 版本号 / .NET SDK 状态 =====
        var r0 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(10, 0, 10, 0) };
        r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F)); r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F)); r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
        lbSt = L("[O] 未运行", Rd); lbSt.Font = new Font(lbSt.Font, FontStyle.Bold); lbVe = L("| 版本: --", Txt2); lbSd = L(".NET SDK: ...", Txt2); lbSd.TextAlign = ContentAlignment.MiddleRight;
        btSdk = B("安装SDK", Or, 7); btSdk.Dock = DockStyle.Fill; btSdk.Click += async (s, e) => { Lg(">>> 开始安装 .NET 10 SDK...", Color.CornflowerBlue); await IS(); };
        r0.Controls.Add(lbSt, 0, 0); r0.Controls.Add(lbVe, 1, 0); r0.Controls.Add(lbSd, 2, 0); r0.Controls.Add(btSdk, 3, 0); root.Controls.Add(r0, 0, 0);

        // ===== r1 主区域容器：左 40%（操作区） + 右 60%（存档管理区） =====
        var r1 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Bg };
        r1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F)); r1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F)); root.Controls.Add(r1, 0, 1);

        // ===== left 左侧操作区：开始游戏 / 快速操作 / 更新管理（三行纵向排列） =====
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Bg };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 34F)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 33F)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));

        // ===== 开始游戏区域：Play（启动服务端+游戏）/ Stop（停止） / Restart（重启） =====
        var gp = new GroupBox { Text = "开始游戏", Dock = DockStyle.Fill, ForeColor = Txt, BackColor = Bg, Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold), Padding = new Padding(6) };
        var pg = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, BackColor = Bg };
        pg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); pg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); pg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        pg.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); pg.RowStyles.Add(new RowStyle(SizeType.Percent, 20F)); pg.RowStyles.Add(new RowStyle(SizeType.Percent, 20F)); pg.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        btPlay = B("开始游戏", Gn, 16, true); btPlay.Dock = DockStyle.Fill; btPlay.Click += async (s, e) => { Lg(">>> 点击了开始游戏", Color.CornflowerBlue); await Play(); };
        btStop = B("停止服务端", Rd, 10, true); btStop.Dock = DockStyle.Fill; btStop.Click += (s, e) => { Lg(">>> 点击了停止服务端", Color.Gold); _sv.Stop(); Lg(">>> 确认服务端已停止", Color.Gold); };
        btRe = B("重启服务端", Or, 10, true); btRe.Dock = DockStyle.Fill; btRe.Click += (s, e) => { Lg(">>> 点击了重启服务端", Color.CornflowerBlue); _sv.Stop(); System.Threading.Thread.Sleep(1000); Go(); };
        pg.Controls.Add(btPlay, 0, 0); pg.Controls.Add(btStop, 1, 0); pg.Controls.Add(btRe, 2, 0);
        cbDx = new CheckBox { Text = "使用 DX11 运行游戏", ForeColor = Txt2, BackColor = Color.Transparent, Font = new Font("Microsoft YaHei", 9f), Cursor = Cursors.Hand, Dock = DockStyle.Fill, CheckAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), MinimumSize = new Size(0, 22) };
        cbDx.CheckedChanged += Cd; pg.Controls.Add(cbDx, 0, 1); pg.SetColumnSpan(cbDx, 2);
        cbDt = new CheckBox { Text = "使用 DX12 运行游戏", ForeColor = Txt2, BackColor = Color.Transparent, Font = new Font("Microsoft YaHei", 9f), Cursor = Cursors.Hand, Dock = DockStyle.Fill, CheckAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), MinimumSize = new Size(0, 22) };
        cbDt.CheckedChanged += Cd; pg.Controls.Add(cbDt, 0, 2); pg.SetColumnSpan(cbDt, 2);
        cbDw = new CheckBox { Text = "去除dgVoodooCpl运行水印", ForeColor = Txt2, BackColor = Color.Transparent, Font = new Font("Microsoft YaHei", 9f), Cursor = Cursors.Hand, Dock = DockStyle.Fill, CheckAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), MinimumSize = new Size(0, 22) };
        cbDw.CheckedChanged += Cd; pg.Controls.Add(cbDw, 0, 3); pg.SetColumnSpan(cbDw, 2);
        gp.Controls.Add(pg); left.Controls.Add(gp, 0, 0);

        // ===== 快速操作区域：PVF 状态显示 / 上次更新时间 / 打开 PVF 目录 =====
        var gq = new GroupBox { Text = "快速操作", Dock = DockStyle.Fill, ForeColor = Txt, BackColor = Bg, Padding = new Padding(6) };
        var qg = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Bg };
        qg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F)); qg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        lbPv = L("PVF: 检测中...", Txt2); lbLu = L("上次更新: 尚未有log日志无法识别版本，请进行更新", Or);
        qg.Controls.Add(lbPv, 0, 0); qg.Controls.Add(lbLu, 0, 1); qg.SetColumnSpan(lbLu, 2);
        btPv = B("打开PVF目录", Bg, 9); btPv.Dock = DockStyle.Fill;
        btPv.Click += (s, e) => { Lg(">>> 打开PVF目录", Color.CornflowerBlue); var d = Path.Combine(_ad, "ServerS4A12-AUM", "dist", "win-x64", "Data", "Pvf"); if (Directory.Exists(d)) Process.Start("explorer.exe", d); else Lg("PVF目录不存在", Color.Gold); };
        qg.Controls.Add(btPv, 1, 0); gq.Controls.Add(qg); left.Controls.Add(gq, 0, 1);

        // ===== 更新管理区域：增量更新 / 全量更新 / 查看更新日志 =====
        var gu = new GroupBox { Text = "更新管理", Dock = DockStyle.Fill, ForeColor = Txt, BackColor = Bg, Padding = new Padding(6) };
        var ug = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Bg };
        ug.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); ug.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); ug.RowStyles.Add(new RowStyle(SizeType.Percent, 55F)); ug.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        btIn = B("增量更新", Ac, 10, true); btFu = B("全量更新", Or, 10, true); btIn.Dock = DockStyle.Fill; btFu.Dock = DockStyle.Fill;
        btIn.Click += async (s, e) => { Lg(">>> 点击了增量更新", Color.CornflowerBlue); await RI(); };
        btFu.Click += async (s, e) => { Lg(">>> 点击了全量更新", Color.CornflowerBlue); await RF(); };
        ug.Controls.Add(btIn, 0, 0); ug.Controls.Add(btFu, 1, 0);
        btVL = B("查看更新日志", Bg, 9); btVL.Dock = DockStyle.Fill; btVL.Click += (s, e) => { Lg(">>> 查看更新日志", Color.CornflowerBlue); SL(); };
        ug.Controls.Add(btVL, 0, 1); ug.SetColumnSpan(btVL, 2); gu.Controls.Add(ug); left.Controls.Add(gu, 0, 2);
        r1.Controls.Add(left, 0, 0);

        // ===== 存档管理区域：信息栏 + 工具栏 + ListView + 拖拽换挡区 =====
        var ga = new GroupBox { Text = "存档管理", Dock = DockStyle.Fill, ForeColor = Txt, BackColor = Bg, Padding = new Padding(6) };
        var ag = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Bg };
        ag.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F)); ag.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F)); ag.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); ag.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        var ib = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Bg };
        ib.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F)); ib.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); ib.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        lbCu = L("当前: --", Txt); lbBk = L("备份数: 0", Txt2); lbBk.TextAlign = ContentAlignment.MiddleRight;
        var btRf = new Button { Text = "刷新", FlatStyle = FlatStyle.Flat, BackColor = Bg, ForeColor = Txt, Font = new Font("Microsoft YaHei", 8f), Dock = DockStyle.Fill, Margin = new Padding(2), Cursor = Cursors.Hand, UseVisualStyleBackColor = false, FlatAppearance = { BorderSize = 0 }, TextAlign = ContentAlignment.MiddleCenter, MinimumSize = new Size(40, 20) };
        btRf.Click += (s, e) => { Lg(">>> 刷新存档列表", Color.CornflowerBlue); RA(); };
        ib.Controls.Add(lbCu, 0, 0); ib.Controls.Add(lbBk, 1, 0); ib.Controls.Add(btRf, 2, 0);
        ag.Controls.Add(ib, 0, 0);

        var ab = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, BackColor = Bg };
        for (int i = 0; i < 7; i++) ab.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 7F));
        btOD = B("切换存档目录", Bg, 8); btOB = B("备份存档目录", Bg, 8); btMD = B("主存档目录", Bg, 8);
        btSC = B("储存当前存档", Ac, 8, true); btIm = B("导入存档", Bg, 8); btEx = B("导出当前", Bg, 8); btUd = B("撤销换挡", Bg, 8);
        foreach (var b in new[] { btOD, btOB, btMD, btSC, btIm, btEx, btUd }) b.Dock = DockStyle.Fill;
        btOD.Click += (s, e) => { var d = Path.Combine(_ad, "存档管理", "切换库"); Directory.CreateDirectory(d); Process.Start("explorer.exe", d); Lg(">>> 打开了切换存档目录", Color.CornflowerBlue); };
        btOB.Click += (s, e) => { var d = Path.Combine(_ad, "存档管理", "备份存档"); Directory.CreateDirectory(d); Process.Start("explorer.exe", d); Lg(">>> 打开了备份存档目录", Color.CornflowerBlue); };
        btMD.Click += (s, e) => { var d = Path.Combine(_ad, "ServerS4A12-AUM", "dist", "win-x64", "Data"); if (Directory.Exists(d)) { Process.Start("explorer.exe", d); Lg(">>> 打开了主存档目录", Color.CornflowerBlue); } else Lg("主存档目录不存在", Color.Gold); };
        btSC.Click += (s, e) => { Lg(">>> 点击了储存当前存档", Color.CornflowerBlue); SC(); };
        btIm.Click += (s, e) => { Lg(">>> 点击了导入存档", Color.CornflowerBlue); IA(); };
        btEx.Click += (s, e) => { Lg(">>> 点击了导出当前", Color.CornflowerBlue); EC(); };
        btUd.Click += (s, e) => { Lg(">>> 点击了撤销换挡", Color.CornflowerBlue); if (_ar.UndoSwap(_ad)) LS("已撤销"); else Lg("无备份", Color.Gold); RA(); };
        ab.Controls.Add(btOD, 0, 0); ab.Controls.Add(btOB, 1, 0); ab.Controls.Add(btMD, 2, 0);
        ab.Controls.Add(btSC, 3, 0); ab.Controls.Add(btIm, 4, 0); ab.Controls.Add(btEx, 5, 0); ab.Controls.Add(btUd, 6, 0);
        ag.Controls.Add(ab, 0, 1);

        // ListView：以表格形式展示所有存档条目（编号/名称/大小/修改时间）
        lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BackColor = Color.FromArgb(25, 25, 25), ForeColor = Txt, Font = new Font("Microsoft YaHei", 9f), Scrollable = true, HeaderStyle = ColumnHeaderStyle.Clickable };
        lv.Columns.Add("#", 40); lv.Columns.Add("存档名称(右键双击改名)", -2); lv.Columns.Add("大小", 75); lv.Columns.Add("修改时间(点此排序)", 145);
        lv.MouseDown += Am; lv.ColumnClick += Ao;
        ag.Controls.Add(lv, 0, 2);

        // 拖拽区：从外部拖入 .db 文件即可快速换挡，双击则储存当前存档
        var dz = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 20), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
        dz.DoubleClick += (s, e) => { Lg(">>> 双击拖拽区，储存当前存档", Color.CornflowerBlue); SC(); };
        dz.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { var fs = (string[])e.Data.GetData(DataFormats.FileDrop); if (fs.Length == 1 && fs[0].EndsWith(".db", StringComparison.OrdinalIgnoreCase)) e.Effect = DragDropEffects.Copy; } };
        dz.DragDrop += (s, e) => { var fs = (string[])e.Data.GetData(DataFormats.FileDrop); Lg(">>> 拖拽换挡: " + Path.GetFileName(fs[0]), Color.CornflowerBlue); _ar.Swap(_ad, fs[0]); LS("拖拽换挡完成"); RA(); TB(); };
        lbDr = new Label { Text = "[ 拖拽DB到此处可以快速替换存档 ]", ForeColor = Txt2, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        dz.Controls.Add(lbDr); ag.Controls.Add(dz, 0, 3);
        ga.Controls.Add(ag); r1.Controls.Add(ga, 1, 0);

        // ===== 日志区域：RichTextBox + 进度条 + 清空/复制按钮 =====
        var lp = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 12, 12) };
        rt = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 12, 12), ForeColor = Txt, ReadOnly = true, WordWrap = true, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.None };
        pb = new ProgressBar { Dock = DockStyle.Bottom, Height = 8, Style = ProgressBarStyle.Continuous, Maximum = 100, Visible = false, BackColor = Bg };
        lbPg = new Label { Text = "", ForeColor = Txt2, AutoSize = true, BackColor = Color.Transparent, Visible = false };
        var lbar = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Bg };
        btCl = new Button { Text = "清空", FlatStyle = FlatStyle.Flat, BackColor = Bg, ForeColor = Txt, MinimumSize = new Size(65, 28), Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Cursor = Cursors.Hand, UseVisualStyleBackColor = false, FlatAppearance = { BorderSize = 0 } };
        btCp = new Button { Text = "复制", FlatStyle = FlatStyle.Flat, BackColor = Bg, ForeColor = Txt, MinimumSize = new Size(65, 28), Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Cursor = Cursors.Hand, UseVisualStyleBackColor = false, FlatAppearance = { BorderSize = 0 } };
        btCl.Click += (s, e) => { Lg(">>> 清空日志", Color.CornflowerBlue); rt.Clear(); };
        btCp.Click += (s, e) => { Lg(">>> 复制日志", Color.CornflowerBlue); if (rt.Text.Length > 0) Clipboard.SetText(rt.Text); };
        lbar.Controls.Add(btCp); lbar.Controls.Add(btCl);
        btCp.Location = new Point(lbar.Width - 140, 4); btCl.Location = new Point(lbar.Width - 70, 4);
        lbar.Resize += (s, e) => { btCp.Location = new Point(lbar.Width - 140, 4); btCl.Location = new Point(lbar.Width - 70, 4); };
        lp.Controls.Add(rt); lp.Controls.Add(pb); lp.Controls.Add(lbPg); lp.Controls.Add(lbar); root.Controls.Add(lp, 0, 2);

        // ===== 底部链接栏：显示 GitHub 仓库链接 =====
        var r3 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = Color.FromArgb(45, 45, 45), Padding = new Padding(8, 0, 8, 0) };
        var lr = new LinkLabel { Text = "仓库: codeberg.org/rewio/ServerS4A12", ForeColor = Color.LightBlue, LinkColor = Color.LightBlue, AutoSize = true, Anchor = AnchorStyles.Right };
        lr.LinkClicked += (s, e) => { Lg(">>> 打开仓库链接", Color.CornflowerBlue); Process.Start("explorer.exe", "https://codeberg.org/rewio/ServerS4A12"); };
        r3.Controls.Add(lr, 0, 0); root.Controls.Add(r3, 0, 3);
    }

    // Ti() 初始化三个 Timer：
    //   _pt：进度条定时器（200ms 间隔，_pv 递增模拟更新进度）
    //   _ct：确认运行定时器（3秒后检查服务端是否成功启动）
    //   _st：状态刷新定时器（2秒间隔，刷新 UI 上的服务端状态、PVF、版本信息）
    void Ti() { _pt = new Timer { Interval = 200 }; _pt.Tick += (s, e) => { _pv++; if (_pv >= 95) _pt.Stop(); pb.Value = _pv; lbPg.Text = "更新进度: " + _pv + "%"; }; _ct = new Timer { Interval = 3000 }; _ct.Tick += (s, e) => { if (_sv.IsRunning) { Lg(">>> 确认服务端运行开始", Gn); _ct.Stop(); } }; _st = new Timer { Interval = 2000 }; _st.Tick += (s, e) => Rs(); _st.Start(); }
    // Fc() 窗口关闭事件：弹出确认对话框，用户确认后才停止服务端并关闭程序
    void Fc(object s, FormClosingEventArgs e) { var r = MessageBox.Show("退出本程序之后会自动关闭正在运行的服务端，是否确认？", "确认退出", MessageBoxButtons.YesNo, MessageBoxIcon.Warning); if (r == DialogResult.Yes) { _sv.Stop(); _st.Stop(); _pt.Stop(); _ct.Stop(); } else e.Cancel = true; }
    // Go() 启动服务端：调用 ServerService.Start() 并启动确认定时器等待进程就绪
    void Go() { _sv.Start(Path.Combine(_ad, "ServerS4A12-AUM")); _ct.Start(); }
    // Play() 异步启动游戏流程：启动服务端 → 等待 5 秒 → 打开游戏客户端 bat 文件
    async System.Threading.Tasks.Task Play() { Lg(">>> 正在启动服务端...", Color.CornflowerBlue); Go(); await System.Threading.Tasks.Task.Delay(5000); var p = Directory.GetParent(_bd); var bat = p != null ? Path.Combine(p.FullName, "本地游戏S4.bat") : ""; if (File.Exists(bat)) { Process.Start(new ProcessStartInfo { FileName = bat, WorkingDirectory = p.FullName, UseShellExecute = true }); Lg(">>> 已打开本地游戏S4.bat", Gn); } else { var fb = Path.Combine(_ad, "启动本地游戏.bat"); if (File.Exists(fb)) { Process.Start(new ProcessStartInfo { FileName = fb, WorkingDirectory = _ad, UseShellExecute = true }); Lg(">>> 已打开启动本地游戏.bat", Gn); } else Lg(">>> 本地游戏S4.bat 未找到!", Rd); } }
    // Lg() 追加日志到 RichTextBox：支持跨线程调用，自动加时间戳前缀，滚动到末尾
    void Lg(string m) => Lg(m, Txt);
    void Lg(string m, Color c) { if (rt.InvokeRequired) { rt.Invoke(new Action(() => Lg(m, c))); return; } rt.SelectionStart = rt.TextLength; rt.SelectionLength = 0; rt.SelectionColor = Txt; rt.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] "); rt.SelectionColor = c; rt.AppendText(m + "\n"); rt.ScrollToCaret(); }
    // LS() 以绿色文字记录成功/提示类日志
    void LS(string m) => Lg(m, Gn);

    // IA() 导入存档：弹出文件选择框 → 复制 .db 到切换库 → 刷新列表
    void IA() { using var d = new OpenFileDialog { Filter = "DB|*.db" }; if (d.ShowDialog() == DialogResult.OK) { var dest = Path.Combine(_ad, "存档管理", "切换库", Path.GetFileName(d.FileName)); Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(d.FileName, dest, true); LS("已导入: " + Path.GetFileName(d.FileName)); RA(); } }
    // EC() 导出存档：弹出输入框让用户命名 → 调用 ArchiveService.Export 复制到切换库
    void EC() { var n = Interaction.InputBox("名称:", "导出存档", "存档"); if (!string.IsNullOrWhiteSpace(n)) { _ar.Export(_ad, n); LS("已导出: " + n + ".db"); RA(); } }
    // SC() 储存当前存档：以当前时间作为默认名 → 调用 Export 保存
    void SC() { var n = Interaction.InputBox("名称:", "储存当前存档", DateTime.Now.ToString("MMdd_HHmm")); if (!string.IsNullOrWhiteSpace(n)) { _ar.Export(_ad, n); LS("已储存到切换库: " + n + ".db"); RA(); } }

    // Am() ListView 双击事件：左键双击 → 换挡到该存档；右键双击 → 重命名存档文件
    void Am(object s, MouseEventArgs e)
    {
        var h = lv.HitTest(e.X, e.Y); if (h?.Item == null) return;
        var nm = h.Item.SubItems[1].Text;
        if (e.Button == MouseButtons.Right && e.Clicks == 2)
        {
            var nn = Interaction.InputBox("修改存档名称:", "重命名", nm.Replace(".db", ""));
            if (!string.IsNullOrWhiteSpace(nn) && nn != nm.Replace(".db", ""))
            {
                var op = Path.Combine(_ad, "存档管理", "切换库", nm);
                var nf = nn.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ? nn : nn + ".db";
                var np = Path.Combine(_ad, "存档管理", "切换库", nf);
                if (File.Exists(op) && !File.Exists(np)) { File.Move(op, np); LS("已重命名: " + nm + " -> " + nf); RA(); }
                else if (File.Exists(np)) Lg("名称已存在", Color.Gold);
                else Lg("重命名失败", Color.Gold);
            }
            return;
        }
        if (e.Button == MouseButtons.Left && e.Clicks == 2)
        {
            _ar.Swap(_ad, Path.Combine(_ad, "存档管理", "切换库", nm)); LS("已切换到: " + nm); RA(); TB();
        }
    }

    // Ao() 点击列头排序：点击"修改时间"列头可在升序/降序之间切换
    void Ao(object s, ColumnClickEventArgs e) { if (e.Column == 3) { _sa = !_sa; RA(); } }
    // TB() 清理超出上限的旧备份：当备份数量超过 MB（10个）时删除最旧的
    void TB() { var bd = Path.Combine(_ad, "存档管理", "备份存档"); if (!Directory.Exists(bd)) return; var fs = new DirectoryInfo(bd).GetFiles("backup_*.db").OrderByDescending(f => f.LastWriteTime).ToList(); while (fs.Count > MB) { fs[^1].Delete(); fs.RemoveAt(fs.Count - 1); } }
    // SL() 用记事本打开更新日志文件（更新日志.txt）
    void SL() { var lf = Path.Combine(_ad, "更新日志.txt"); if (File.Exists(lf)) Process.Start("notepad.exe", lf); else MessageBox.Show("暂时没有更新日志，请注意查看版本信息。", "更新日志", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    // De() 拖拽进入事件：只接受单个 .db 文件的拖入，显示复制光标
    void De(object s, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { var fs = (string[])e.Data.GetData(DataFormats.FileDrop); if (fs.Length == 1 && fs[0].EndsWith(".db", StringComparison.OrdinalIgnoreCase)) e.Effect = DragDropEffects.Copy; } }
    // Dd() 拖拽释放事件：把拖入的 .db 文件执行换挡操作，然后刷新列表
    void Dd(object s, DragEventArgs e) { var fs = (string[])e.Data.GetData(DataFormats.FileDrop); Lg(">>> 拖拽换挡: " + Path.GetFileName(fs[0]), Color.CornflowerBlue); _ar.Swap(_ad, fs[0]); LS("拖拽换挡完成"); RA(); TB(); }
    // Ck() 检测 .NET SDK 状态：Win10版本检测 → 便携/依赖版识别 → 系统dotnet(含Program Files) → 本地dotnet-sdk → 日志+UI输出
    void Ck()
    {
        // 输出 ServerUI 自身版本号
        Lg("ServerUI 版本: " + VER, Color.DarkOrange);

        // 检测当前EXE是否为自包含便携版（>50MB → 打包了.NET Runtime）
        var exePath = Environment.ProcessPath ?? "";
        bool isPortable = !string.IsNullOrEmpty(exePath) && File.Exists(exePath) && new FileInfo(exePath).Length > 50_000_000;

        // 检测操作系统版本（Win10 Build<22000, Win11 Build>=22000）
        var osVer = Environment.OSVersion;
        bool isWin10Plus = osVer.Platform == PlatformID.Win32NT && osVer.Version.Major >= 10;
        if (!isWin10Plus)
            Lg("系统版本低于 Windows 10，可能会出现兼容性问题，建议升级到 Win10 或更高版本", Or);
        else
        {
            var winVer = osVer.Version.Build >= 22000 ? "11" : "10";
            Lg("系统版本: Windows " + winVer + " (Build " + osVer.Version.Build + ")", Txt2);
        }

        // 版本类型提示
        if (isPortable)
            Lg("本版本为便携版（无依赖版），已内置 .NET 10 运行环境", Gn);
        else
            Lg("本版本为有依赖版，需要系统安装 .NET 10 运行环境才能运行", Txt);

        string sdk = "未安装";
        Color c = Rd;
        bool sysOk = false;
        bool pfOk = false;
        bool localOk = false;

        // 优先级1：系统 PATH 中的 dotnet，版本 ≥10
        try
        {
            var p = new Process { StartInfo = new ProcessStartInfo { FileName = "dotnet", Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
            p.Start(); var v = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit();
            if (p.ExitCode == 0 && !string.IsNullOrEmpty(v))
            {
                if (v.StartsWith("10.")) { sdk = "已就绪 v" + v; c = Gn; sysOk = true; }
                else { Lg("系统已安装 .NET v" + v + "，但需要 ≥10.0 版本", Or); }
            }
        }
        catch { }

        // 优先级2：Program Files 下的 dotnet SDK，版本 ≥10
        if (!sysOk)
        {
            try
            {
                var pfPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
                if (File.Exists(pfPath))
                {
                    var p = new Process { StartInfo = new ProcessStartInfo { FileName = pfPath, Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
                    p.Start(); var v = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit();
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(v) && v.StartsWith("10.")) { sdk = "已就绪 v" + v + " (Program Files)"; c = Gn; pfOk = true; }
                }
            }
            catch { }
        }

        // 优先级3：AUM管理组件\dotnet-sdk\dotnet.exe（便携SDK）
        if (!sysOk && !pfOk)
        {
            var localPath = Path.Combine(_ad, "dotnet-sdk", "dotnet.exe");
            if (File.Exists(localPath))
            {
                try
                {
                    var p = new Process { StartInfo = new ProcessStartInfo { FileName = localPath, Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
                    p.Start(); var v = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit();
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(v) && v.StartsWith("10.")) { sdk = "便携SDK v" + v; c = Or; localOk = true; }
                }
                catch { }
                if (!localOk) { sdk = "便携SDK (版本异常)"; c = Or; localOk = true; }
            }
        }

        // 更新UI顶栏SDK状态
        lbSd.Text = ".NET SDK: [O] " + sdk;
        lbSd.ForeColor = c;

        // 写入运行日志
        if (sysOk || pfOk) Lg("检测到系统已安装 .NET 10 SDK，可用于编译服务端更新", Gn);
        else if (localOk) Lg("检测到本地便携 .NET SDK (dotnet-sdk)，可用于编译服务端更新", Gn);
        else if (isPortable)
        {
            Lg("未检测到 .NET 10 SDK，虽然本程序可运行，但更新时无法编译服务端！", Rd);
            Lg("请将 dotnet-sdk 目录放入 AUM管理组件，或手动安装 .NET 10 SDK", Rd);
        }
        else
        {
            Lg("未检测到 .NET 10 运行环境，本程序可能无法正常工作！", Rd);
            Lg("请安装 .NET 10.0 或改用便携版 (ServerUI-无依赖版.exe) 后重试", Rd);
        }
    }
    void Cd(object s, EventArgs e)
    {
        if (_cdBusy) return;
        if (cbDx.Checked && cbDt.Checked)
        {
            _cdBusy = true;
            var clicked = (CheckBox)s;
            MessageBox.Show("DX11 和 DX12 补丁不能同时启用，请只选择其中一个。", "冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            clicked.Checked = false;
            _cdBusy = false;
            return;
        }
        var files = new[] { "D3D9.dll", "dgVoodoo.conf", "dgVoodooCpl.exe" };
        string srcDir = null; string tag = "";
        if (cbDt.Checked)
        {
            srcDir = Path.Combine(_ad, "DX12补丁"); tag = " (DX12)";
            if (!Directory.Exists(srcDir)) { Lg("DX12补丁目录不存在: " + srcDir, Or); return; }
            if (cbDw.Checked) { var wm = Path.Combine(srcDir, "无水印"); if (Directory.Exists(wm)) { srcDir = wm; tag = " (DX12无水印版)"; } else Lg("DX12无水印目录不存在", Or); }
        }
        else if (cbDx.Checked)
        {
            srcDir = Path.Combine(_ad, "DX11补丁"); tag = " (DX11)";
            if (!Directory.Exists(srcDir)) { Lg("DX11补丁目录不存在: " + srcDir, Or); return; }
            if (cbDw.Checked) { var wm = Path.Combine(srcDir, "无水印"); if (Directory.Exists(wm)) { srcDir = wm; tag = " (DX11无水印版)"; } else Lg("DX11无水印目录不存在", Or); }
        }
        else if (cbDw.Checked)
        {
            Lg("请先选择 DX11 或 DX12 运行模式再启用水印", Or); return;
        }
        if (srcDir != null)
        {
            var allExist = true;
            foreach (var fn in files)
            {
                var src = Path.Combine(srcDir, fn);
                var dst = Path.Combine(_gr, fn);
                if (!File.Exists(dst) || !File.Exists(src) || new FileInfo(src).Length != new FileInfo(dst).Length)
                { allExist = false; break; }
            }
            if (allExist) { Lg("DX补丁文件已存在于游戏目录，无需复制", Txt2); return; }
            foreach (var fn in files)
            {
                var src = Path.Combine(srcDir, fn);
                var dst = Path.Combine(_gr, fn);
                if (File.Exists(src)) { File.Copy(src, dst, true); }
            }
            Lg("DX补丁已复制到游戏目录" + tag, Gn);
        }
        else
        {
            foreach (var fn in files)
            {
                var dst = Path.Combine(_gr, fn);
                if (File.Exists(dst)) { try { File.Delete(dst); } catch { } }
            }
            Lg("DX补丁已从游戏目录移除", Txt2);
        }
    }
    // IS() 一键安装 .NET 10 SDK：调用 dotnet-install.ps1 安装到 dotnet-sdk\，安装完成后自动刷新 SDK 状态
    async System.Threading.Tasks.Task IS()
    {
        btSdk.Enabled = false; btSdk.Text = "安装中...";
        var installer = Path.Combine(_ad, "dotnet-install.ps1");
        var sdkDir = Path.Combine(_ad, "dotnet-sdk");
        if (!File.Exists(installer))
        {
            Lg("dotnet-install.ps1 未找到！请确保文件存在于 AUM管理组件 目录中", Rd);
            btSdk.Enabled = true; btSdk.Text = "安装SDK";
            return;
        }
        Lg("正在下载并安装 .NET 10 SDK 到 dotnet-sdk\\ (约 280MB，请耐心等待)...", Color.CornflowerBlue);
        await System.Threading.Tasks.Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"& '" + installer + "' -Channel 10.0 -InstallDir '" + sdkDir + "' -NoPath\"",
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode == 0 && File.Exists(Path.Combine(sdkDir, "dotnet.exe")))
            {
                Lg(".NET 10 SDK 安装成功！已安装到 dotnet-sdk\\", Gn);
                var vp = new Process { StartInfo = new ProcessStartInfo { FileName = Path.Combine(sdkDir, "dotnet.exe"), Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
                vp.Start(); var ver = vp.StandardOutput.ReadToEnd().Trim(); vp.WaitForExit();
                if (!string.IsNullOrEmpty(ver)) Lg("SDK 版本: v" + ver, Gn);
                Ck();
            }
            else
            {
                Lg(".NET 10 SDK 安装失败，退出代码: " + p.ExitCode, Rd);
                if (!string.IsNullOrEmpty(stderr)) Lg("错误: " + stderr, Rd);
            }
        });
        btSdk.Enabled = true; btSdk.Text = "安装SDK";
    }
    // Rs() 2秒定时刷新：更新服务端运行状态、PVF 是否存在、版本号信息
    void Rs() { bool r = _sv.IsRunning; lbSt.Text = r ? "[O] 运行中" : "[O] 未运行"; lbSt.ForeColor = r ? Gn : Rd; lbPv.Text = _sv.PvfExists(Path.Combine(_ad, "ServerS4A12-AUM")) ? "PVF: [O] 已加载" : "PVF: [O] 未找到"; lbPv.ForeColor = _sv.PvfExists(Path.Combine(_ad, "ServerS4A12-AUM")) ? Gn : Rd; var vf = Path.Combine(_ad, "更新日志.txt"); if (File.Exists(vf)) { var tx = File.ReadAllText(vf, System.Text.Encoding.UTF8); var ix = tx.LastIndexOf("版本:"); if (ix >= 0) { var en = tx.IndexOf('\n', ix); if (en < 0) en = Math.Min(ix + 20, tx.Length); lbLu.Text = "上次更新: " + tx.Substring(ix, en - ix).Trim().Replace("版本:", "").Trim(); } else lbLu.Text = "上次更新: 尚未有log日志无法识别版本，请进行更新"; } else lbLu.Text = "上次更新: 尚未有log日志无法识别版本，请进行更新"; lbVe.Text = "  |  版本: " + _up.GetVersion(_ad); }
    // Rf() 刷新全部信息：调用 Rs() + RA()
    void Rf() { Rs(); RA(); }
    // RA() 刷新存档列表：从切换库读取所有 .db，填充到 ListView，更新当前状态和备份数
    void RA() { lv.Items.Clear(); var list = _ar.List(_ad); var o = _sa ? list.OrderBy(a => a.Modified).ToList() : list.OrderByDescending(a => a.Modified).ToList(); for (int i = 0; i < o.Count; i++) { var it = new ListViewItem((i + 1).ToString()); it.SubItems.Add(o[i].Name); it.SubItems.Add(o[i].SizeDisplay); it.SubItems.Add(o[i].Modified.ToString("yyyy-MM-dd HH:mm")); lv.Items.Add(it); } lbCu.Text = "当前: " + _ar.CurrentInfo(_ad); lbBk.Text = "备份数: " + _ar.BackupCount(_ad); }
    // RI() 增量更新入口：检测服务端是否运行中（如运行则自动停止）→ 显示进度条 → 调用更新 → 最后刷新
    async System.Threading.Tasks.Task RI() { if (_sv.IsRunning) { Lg(">>> 检测到服务端正在运行，正在自动停止以执行增量更新...", Color.Gold); _sv.Stop(); System.Threading.Thread.Sleep(2000); Lg(">>> 服务端已停止，开始更新", Gn); } pb.Visible = true; lbPg.Visible = true; pb.Value = 0; _pv = 0; Lg(">>> 开始增量更新 <<<", Color.CornflowerBlue); _pt.Start(); _up.OutputReceived += OU; _up.Completed += OD; try { await _up.RunIncremental(Path.Combine(_ad, "ServerS4A12-AUM"), _ad); } finally { _up.OutputReceived -= OU; _up.Completed -= OD; _pt.Stop(); } }
    // RF() 全量更新入口：检测服务端是否运行中（如运行则自动停止）→ 显示进度条 → 调用全量更新 → 最后刷新
    async System.Threading.Tasks.Task RF() { if (_sv.IsRunning) { Lg(">>> 检测到服务端正在运行，正在自动停止以执行全量更新...", Color.Gold); _sv.Stop(); System.Threading.Thread.Sleep(2000); Lg(">>> 服务端已停止，开始更新", Gn); } pb.Visible = true; lbPg.Visible = true; pb.Value = 0; _pv = 0; Lg(">>> 开始全量更新 <<<", Color.CornflowerBlue); _pt.Start(); _up.OutputReceived += OU; _up.Completed += OD; try { await _up.RunFull(Path.Combine(_ad, "ServerS4A12-AUM"), _ad); } finally { _up.OutputReceived -= OU; _up.Completed -= OD; _pt.Stop(); } }
    // OU() 更新输出回调：把 UpdateService 实时输出的每行日志追加到 RichTextBox
    void OU(string m) { if (System.Text.RegularExpressions.Regex.IsMatch(m, @"^--- \d{4}-\d{2}-\d{2}")) Lg(m, Cy); else Lg(m); }
    // OD() 更新完成回调：显示结果 → 1.5 秒后隐藏进度条 → 刷新状态
    void OD(bool ok) { pb.Value = 100; lbPg.Text = "100%"; if (ok) LS(">>> 更新完成！如果更新没有效果，请尝试再次点击更新或者全量更新。<<<"); else Lg(">>> 更新失败，请检查网络连接或查看上方日志。<<<", Color.Orange); System.Threading.Thread.Sleep(1500); pb.Visible = false; lbPg.Visible = false; Rf(); }
}
