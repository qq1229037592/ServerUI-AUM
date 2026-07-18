/*
 * ==================================================================
 * ThemeManager.cs — 多主题管理器和 UI 美化工具
 * ==================================================================
 * 
 * 【这个文件是干什么的？】
 *   这个文件专门负责给整个程序的界面"换肤"。您可以把它理解为
 *   "程序的美容化妆师"——它不改变任何按钮的功能逻辑，只改变
 *   按钮的颜色、背景、字体等外观样式。
 * 
 * 【怎么使用？】
 *   在 MainForm.cs 中，只需要在 Build() 方法后面加一行：
 *     ThemeManager.ApplyTheme(this, ThemeManager.Obsidian);
 *   整个窗口就会自动套用暗黑科技风格。
 * 
 * 【三个主题有什么区别？】
 *   1. Obsidian（暗黑科技）— 默认推荐，VS Code 风格深色界面
 *   2. Light（优雅明亮）— 白色背景，适合光线强的环境
 *   3. Cyber（赛博霓虹）— 紫色/青色荧光风格，非常炫酷
 * 
 * 【新手怎么自定义颜色？】
 *   找到下面对应的主题变量（比如 Obsidian），修改颜色数值即可。
 *   颜色公式：ColorTranslator.FromHtml("#十六进制颜色码")
 *   在线查色：https://www.color-hex.com/
 * ==================================================================
 */

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ServerUI;

// ====================================================================
// 主题数据类 — 存储一个主题的所有颜色设置
// ====================================================================
// 就像一个"颜料盒"，里面装了 12 种不同用途的颜色。
// 每种颜色都有名字（属性名），看到名字就知道这个颜色用在哪。
// 例如：BgWindow 是窗口背景色，TextPrimary 是主要的文字颜色。
// ====================================================================
public class Theme
{
    /// <summary>主题名称（仅用于显示，不影响功能）</summary>
    public string Name { get; set; }

    // ===== 背景色系列 =====
    /// <summary>主窗口背景色 — 整个窗口最底层的颜色</summary>
    public Color BgWindow { get; set; }
    /// <summary>卡片/面板背景色 — 用来区分功能区域的底色</summary>
    public Color BgCard { get; set; }
    /// <summary>交替背景色 — 表格列表里奇偶行不同色，方便阅读</summary>
    public Color BgCardAlt { get; set; }
    /// <summary>日志区域背景色 — 运行日志的黑色底</summary>
    public Color BgLog { get; set; }
    /// <summary>工具栏背景色 — 顶部状态栏和底部链接栏的背景</summary>
    public Color BgToolbar { get; set; }

    // ===== 边框和线条 =====
    /// <summary>边框颜色 — 按钮、输入框、卡片的边缘线</summary>
    public Color Border { get; set; }

    // ===== 文字颜色系列 =====
    /// <summary>主文字色 — 标题、按钮上最重要的文字</summary>
    public Color TextPrimary { get; set; }
    /// <summary>次要文字色 — 提示信息、时间显示等辅助文字</summary>
    public Color TextSecondary { get; set; }

    // ===== 状态/功能颜色 =====
    /// <summary>激活/成功色 — 表示"运行中"、"已就绪"等正面状态</summary>
    public Color Active { get; set; }
    /// <summary>危险/停止色 — 表示"已停止"、"错误"等负面状态</summary>
    public Color Danger { get; set; }
    /// <summary>警告/重启色 — 表示"需要关注"、"重启中"等中间状态</summary>
    public Color Warning { get; set; }
    /// <summary>信息/链接色 — 蓝色系，用于链接和提示信息</summary>
    public Color Info { get; set; }
    /// <summary>青色/高亮色 — 用于特殊高亮显示（版本号、时间标记）</summary>
    public Color Cyan { get; set; }
}

// ====================================================================
// 主题管理器静态类 — 提供主题定义和应用方法
// ====================================================================
// 这个类不需要创建对象（static 类），直接在代码里这样调用：
//   ThemeManager.ApplyTheme(当前窗口, ThemeManager.Obsidian);
// ====================================================================
public static class ThemeManager
{
    // ===== 当前正在使用的主题 =====
    // 默认使用 Obsidian（暗黑科技风），可以随时切换
    public static Theme Current { get; set; } = Obsidian;

    // ================================================================
    // 主题 1：Obsidian（暗黑科技）— 强烈推荐作为默认主题
    // ================================================================
    // 颜色灵感来自微软 VS Code 编辑器的 Dark+ 主题
    // 深灰色调为主，长时间使用不刺眼，专业感强
    // 适合：所有用户，尤其是喜欢深色模式的用户
    // ================================================================
    public static readonly Theme Obsidian = new Theme
    {
        Name          = "Obsidian（暗黑科技）",
        BgWindow      = ColorTranslator.FromHtml("#1E1E1E"),  // 主背景 — 极深灰，类似 VS Code 的侧边栏底色
        BgCard        = ColorTranslator.FromHtml("#252526"),  // 卡片背景 — 比主背景稍亮一点点，形成层次感
        BgCardAlt     = ColorTranslator.FromHtml("#2D2D30"),  // 交替色 — 用在列表的偶数行，形成斑马线效果
        BgLog         = ColorTranslator.FromHtml("#171717"),  // 日志底色 — 最深的纯黑，突出日志文字
        BgToolbar     = ColorTranslator.FromHtml("#232326"),  // 工具栏背景 — 介于主背景和卡片之间的灰色
        Border        = ColorTranslator.FromHtml("#3E3E42"),  // 边框色 — 暗灰色虚线边框，不抢眼但清晰
        TextPrimary   = ColorTranslator.FromHtml("#D4D4D4"),  // 主文字 — 柔和的灰白色，不刺眼
        TextSecondary = ColorTranslator.FromHtml("#858585"),  // 次要文字 — 偏暗的灰色，用于提示信息
        Active        = ColorTranslator.FromHtml("#6A9955"),  // 成功绿 — 沉静的墨绿色，表示"运行中"
        Danger        = ColorTranslator.FromHtml("#F44747"),  // 危险红 — 鲜艳但不刺眼的红色，表示"已停止"
        Warning       = ColorTranslator.FromHtml("#D4A263"),  // 警告橙 — 暖橙色，表示"重启"等中间状态
        Info          = ColorTranslator.FromHtml("#569CD6"),  // 信息蓝 — VS Code 经典的链接蓝色
        Cyan          = ColorTranslator.FromHtml("#4EC9B0"),  // 青色 — 青绿色，用于版本号等特殊高亮
    };

    // ================================================================
    // 主题 2：Light（优雅明亮）— 适合光线强或喜欢亮色界面的用户
    // ================================================================
    // 浅灰色调为主，干净清爽，阅读友好
    // 适合：办公室环境、喜欢纸质风格的用户
    // ================================================================
    public static readonly Theme Light = new Theme
    {
        Name          = "Light（优雅明亮）",
        BgWindow      = ColorTranslator.FromHtml("#F0F2F5"),  // 主背景 — 浅暖灰，比纯白更护眼
        BgCard        = ColorTranslator.FromHtml("#FFFFFF"),  // 卡片背景 — 纯白，形成清晰的卡片边界
        BgCardAlt     = ColorTranslator.FromHtml("#F5F5F5"),  // 交替色 — 极浅的灰，用于表格斑马线
        BgLog         = ColorTranslator.FromHtml("#FAFAFA"),  // 日志底色 — 近乎纯白，清晰显示日志文字
        BgToolbar     = ColorTranslator.FromHtml("#E8EAED"),  // 工具栏背景 — 浅灰，与主背景略有区分
        Border        = ColorTranslator.FromHtml("#DADCE0"),  // 边框色 — 浅灰边框，柔和细腻
        TextPrimary   = ColorTranslator.FromHtml("#202124"),  // 主文字 — 深灰色，比纯黑更柔和
        TextSecondary = ColorTranslator.FromHtml("#5F6368"),  // 次要文字 — 中灰色，提示信息
        Active        = ColorTranslator.FromHtml("#1B7A34"),  // 成功绿 — 深绿色，在浅色背景下清晰可见
        Danger        = ColorTranslator.FromHtml("#C5221F"),  // 危险红 — 深红色，显眼但不刺眼
        Warning       = ColorTranslator.FromHtml("#E37400"),  // 警告橙 — 暖橙色，在浅色背景下醒目
        Info          = ColorTranslator.FromHtml("#1967D2"),  // 信息蓝 — Google 风格的蓝色
        Cyan          = ColorTranslator.FromHtml("#00897B"),  // 青色 — 深青色，用于高亮
    };

    // ================================================================
    // 主题 3：Cyber（赛博霓虹）— 炫酷的赛博朋克风格
    // ================================================================
    // 紫色/青色荧光系，极具个性
    // 适合：想要与众不同的用户，DNF 老玩家可能喜欢这种风格
    // ================================================================
    public static readonly Theme Cyber = new Theme
    {
        Name          = "Cyber（赛博霓虹）",
        BgWindow      = ColorTranslator.FromHtml("#0D0221"),  // 主背景 — 深紫黑色，像夜空
        BgCard        = ColorTranslator.FromHtml("#1A0B2E"),  // 卡片背景 — 深紫色，有层次感
        BgCardAlt     = ColorTranslator.FromHtml("#24143B"),  // 交替色 — 稍亮的紫色
        BgLog         = ColorTranslator.FromHtml("#080115"),  // 日志底色 — 最深的紫黑色
        BgToolbar     = ColorTranslator.FromHtml("#120728"),  // 工具栏背景 — 中等深紫色
        Border        = ColorTranslator.FromHtml("#3D1B5C"),  // 边框色 — 暗紫色边框
        TextPrimary   = ColorTranslator.FromHtml("#E0D0FF"),  // 主文字 — 淡紫色，柔和梦幻
        TextSecondary = ColorTranslator.FromHtml("#9B7EBF"),  // 次要文字 — 中紫色
        Active        = ColorTranslator.FromHtml("#00FFCC"),  // 成功绿 — 荧光青色，赛博风经典色
        Danger        = ColorTranslator.FromHtml("#FF0055"),  // 危险红 — 电光红色，非常醒目
        Warning       = ColorTranslator.FromHtml("#FFCC00"),  // 警告橙 — 霓虹黄色
        Info          = ColorTranslator.FromHtml("#00BFFF"),  // 信息蓝 — 亮蓝色，像发光霓虹灯
        Cyan          = ColorTranslator.FromHtml("#FF00FF"),  // 青色 — 品红色（洋红），赛博风标志色
    };

    // ================================================================
    // 核心方法：给整个窗口「换肤」
    // ================================================================
    // 这个方法会遍历窗口上的所有控件（按钮、标签、面板等），
    // 根据控件的类型给它们涂上对应的主题颜色。
    //
    // 参数说明：
    //   form  — 要换肤的窗口对象（MainForm 实例）
    //   theme — 要使用的主题（Obsidian / Light / Cyber）
    //
    // 使用示例（在 MainForm 构造函数末尾加上）：
    //   ThemeManager.ApplyTheme(this, ThemeManager.Obsidian);
    //
    // 想换主题？把最后一个参数改成 ThemeManager.Light 或 ThemeManager.Cyber
    // ================================================================
    public static void ApplyTheme(Form form, Theme theme)
    {
        // 记录当前使用的主题，方便其他地方读取
        Current = theme;

        // 设置窗口自身的背景色
        form.BackColor = theme.BgWindow;

        // 递归遍历窗口上的所有控件，逐个设置颜色
        // 因为控件是"嵌套"的（面板里面还有面板），所以要用递归
        ApplyControlTheme(form.Controls, theme);
    }

    // ================================================================
    // 递归遍历控件的方法（内部使用，您不需要直接调用）
    // ================================================================
    // 这个方法会检查每个控件的类型，然后根据类型设置颜色。
    // 如果控件里面还有子控件（比如 GroupBox 里面还有按钮），
    // 方法会继续深入检查子控件，直到所有控件都被处理完毕。
    //
    // 参数说明：
    //   controls — 控件集合（某个容器里的所有控件）
    //   theme    — 当前使用的主题
    // ================================================================
    private static void ApplyControlTheme(Control.ControlCollection controls, Theme theme)
    {
        // 遍历集合中的每一个控件
        foreach (Control ctrl in controls)
        {
            // --------------------------------------------------------
            // 1. 处理面板/分组框（Panel / GroupBox / TableLayoutPanel）
            // --------------------------------------------------------
            // 这些控件通常作为"容器"，用来把按钮、标签分组
            // 我们给它们设置卡片背景色，让每个功能区域像"卡片"一样清晰
            // --------------------------------------------------------
            if (ctrl is Panel || ctrl is GroupBox || ctrl is TableLayoutPanel)
            {
                ctrl.BackColor = theme.BgCard;
                ctrl.ForeColor = theme.TextPrimary;

                // GroupBox 默认有丑陋的 3D 边框，改成 Flat 风格
                if (ctrl is GroupBox gbox)
                {
                    gbox.FlatStyle = FlatStyle.Flat;
                }
            }

            // --------------------------------------------------------
            // 2. 处理按钮（Button）
            // --------------------------------------------------------
            // 按钮分为两种：
            //   - 主要功能按钮（开始/停止/重启/更新）— 用品牌色
            //   - 次要操作按钮（打开目录/清空等）— 用温和的背景色
            //
            // 【重要】这里只改颜色和样式，不改按钮的功能！
            // --------------------------------------------------------
            if (ctrl is Button btn)
            {
                // 去掉按钮的 3D 立体效果，改为扁平风格（更现代）
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 1;           // 加 1 像素细边框
                btn.FlatAppearance.BorderColor = theme.Border; // 边框颜色用主题定义

                // 根据按钮的 Text（文字内容）自动匹配颜色
                string text = btn.Text;

                // 注意：按文字匹配时，需要和 MainForm.cs 中 Btn() 方法设置的文字一致
                if (text.Contains("开始游戏"))
                {
                    // "开始游戏"按钮 — 用绿色（表示"启动"、"运行"）
                    btn.BackColor = theme.Active;
                    btn.ForeColor = Color.White;
                }
                else if (text.Contains("停止服务端"))
                {
                    // "停止服务端"按钮 — 用红色（表示"危险"、"停止"）
                    btn.BackColor = theme.Danger;
                    btn.ForeColor = Color.White;
                }
                else if (text.Contains("重启服务端"))
                {
                    // "重启服务端"按钮 — 用橙色（表示"警告"、"注意"）
                    btn.BackColor = theme.Warning;
                    btn.ForeColor = Color.White;
                }
                else if (text.Contains("增量更新") || text.Contains("全量更新"))
                {
                    // 更新按钮 — 用蓝色（表示"信息"、"操作"）
                    btn.BackColor = theme.Info;
                    btn.ForeColor = Color.White;
                }
                else if (text.Contains("GM 工具"))
                {
                    // GM 工具按钮 — 使用金色/青色特殊颜色
                    btn.BackColor = theme.Cyan;
                    btn.ForeColor = Color.White;
                }
                else if (text.Contains("安装 SDK"))
                {
                    // 安装 SDK 按钮 — 用警告色，因为它是"需要操作"的按钮
                    btn.BackColor = theme.Warning;
                    btn.ForeColor = Color.White;
                }
                else
                {
                    // 其他次要按钮（打开目录、清空、复制、刷新存档等）
                    // 使用比卡片背景稍亮一点的颜色，低调但可点击
                    btn.BackColor = theme.BgCardAlt;
                    btn.ForeColor = theme.TextPrimary;
                }
            }

            // --------------------------------------------------------
            // 3. 处理标签（Label）
            // --------------------------------------------------------
            // 判断哪些标签是"主要文字"（标题、状态），哪些是"次要文字"（提示）
            // 主要文字用 TextPrimary 色，次要文字用 TextSecondary 色
            // --------------------------------------------------------
            if (ctrl is Label lbl)
            {
                // 如果标签的 ForeColor 是灰色/暗色 → 认为是"次要文字"
                // 或者标签名称包含 time/sub/版本 等关键词 → 也认为是次要文字
                // 否则认为是"主要文字"
                if (lbl.ForeColor == Color.Gray ||
                    lbl.ForeColor == Color.FromArgb(133, 133, 133) ||
                    lbl.ForeColor == Color.FromArgb(85, 85, 85) ||
                    (lbl.Name != null && (
                        lbl.Name.Contains("Ve") ||     // 版本号标签
                        lbl.Name.Contains("Sd") ||     // SDK 状态标签
                        lbl.Name.Contains("Bk") ||     // 备份数标签
                        lbl.Name.Contains("Dr") ||     // 拖拽提示标签
                        lbl.Name.Contains("Pg")        // 进度标签
                    )))
                {
                    lbl.ForeColor = theme.TextSecondary;
                }
                else
                {
                    lbl.ForeColor = theme.TextPrimary;
                }

                // 标签背景色通常设为透明（跟随父容器）
                // 但有些标签在 Build() 里设了固定背景色，这里无需覆盖
            }

            // --------------------------------------------------------
            // 4. 处理复选框（CheckBox）
            // --------------------------------------------------------
            // 复选框的文字颜色调暗一点，因为它们是"附加选项"，不是主要功能
            // --------------------------------------------------------
            if (ctrl is CheckBox chk)
            {
                chk.ForeColor = theme.TextSecondary;
            }

            // --------------------------------------------------------
            // 5. 处理列表视图（ListView）— 存档文件列表
            // --------------------------------------------------------
            // 去掉默认的 3D 边框，改成扁平风格
            // 让列表阅读更清晰
            // --------------------------------------------------------
            if (ctrl is ListView lv)
            {
                // 注意：WinForms 的 ListView 控件没有 BackgroundColor 属性
                // 我们使用 BackColor 来设置列表背景色
                lv.BackColor = theme.BgCard;
                lv.ForeColor = theme.TextPrimary;
                lv.BorderStyle = BorderStyle.None;  // 无边框，更简洁
            }

            // --------------------------------------------------------
            // 6. 处理富文本框（RichTextBox）— 运行日志
            // --------------------------------------------------------
            // 日志区域应该用深色背景 + 浅色文字，模拟终端控制台风格
            // --------------------------------------------------------
            if (ctrl is RichTextBox rt)
            {
                rt.BackColor = theme.BgLog;
                rt.ForeColor = theme.TextPrimary;
            }

            // --------------------------------------------------------
            // 7. 处理进度条（ProgressBar）
            // --------------------------------------------------------
            // 更新进度条的颜色
            // --------------------------------------------------------
            if (ctrl is ProgressBar pb)
            {
                pb.BackColor = theme.BgCardAlt;
                pb.ForeColor = theme.Active;
            }

            // --------------------------------------------------------
            // 8. 处理链接标签（LinkLabel）
            // --------------------------------------------------------
            // GM 工具链接和仓库链接的颜色
            // --------------------------------------------------------
            if (ctrl is LinkLabel ll)
            {
                ll.ForeColor = theme.Info;
                ll.LinkColor = theme.Info;
                ll.ActiveLinkColor = theme.Cyan;
                ll.VisitedLinkColor = theme.Info;
            }

            // ============================================================
            // 【递归关键】— 如果当前控件里面还有子控件，继续深入处理
            // ============================================================
            // 举个例子：
            //   GroupBox（游戏控制）里面有个 TableLayoutPanel
            //   TableLayoutPanel 里面又有 Button（开始游戏）、CheckBox 等
            //   如果不递归，就只能处理到 GroupBox 这一层，里面的按钮就没被美化
            // ============================================================
            if (ctrl.HasChildren)
            {
                ApplyControlTheme(ctrl.Controls, theme);
            }
        }
    }
}
