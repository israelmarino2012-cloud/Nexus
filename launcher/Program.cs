// Nexus Library - launcher / home page for the Nexus emulator.
// Plain C# 5 + WinForms (compiles with the csc.exe that ships with Windows).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace NexusLauncher
{
    static class Theme
    {
        public static readonly Color Bg      = Color.FromArgb(15, 17, 21);
        public static readonly Color Surface = Color.FromArgb(22, 25, 32);
        public static readonly Color Raised  = Color.FromArgb(30, 34, 43);
        public static readonly Color Border  = Color.FromArgb(39, 44, 56);
        public static readonly Color Text    = Color.FromArgb(215, 219, 230);
        public static readonly Color Muted   = Color.FromArgb(123, 130, 150);
        public static readonly Color Accent  = Color.FromArgb(124, 92, 255);
    }

    static class Native
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }

    // ------------------------------------------------------------------ data
    class Library
    {
        public string DataDir;
        public string CoversDir;
        string listFile;
        public List<string> Roms = new List<string>();

        public Library()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            DataDir = exeDir;
            try
            {
                string probe = Path.Combine(exeDir, ".write_test");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
            }
            catch
            {
                DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexus");
            }
            Directory.CreateDirectory(DataDir);
            CoversDir = Path.Combine(DataDir, "covers");
            Directory.CreateDirectory(CoversDir);
            listFile = Path.Combine(DataDir, "library.txt");
            Load();
        }

        void Load()
        {
            Roms.Clear();
            try
            {
                if (File.Exists(listFile))
                    foreach (string line in File.ReadAllLines(listFile, Encoding.UTF8))
                        if (line.Trim().Length > 0 && !Roms.Contains(line.Trim()))
                            Roms.Add(line.Trim());
            }
            catch { }
        }

        public void Save()
        {
            try { File.WriteAllLines(listFile, Roms.ToArray(), Encoding.UTF8); }
            catch { }
        }

        public bool Add(string path)
        {
            if (!File.Exists(path)) return false;
            foreach (string r in Roms)
                if (string.Equals(r, path, StringComparison.OrdinalIgnoreCase)) return false;
            Roms.Add(path);
            return true;
        }

        public static string TitleOf(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        public static string SafeName(string title)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in title)
            {
                if ("&*/:`<>?\\|\"".IndexOf(c) >= 0) sb.Append('_');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public string CoverPath(string rom)
        {
            return Path.Combine(CoversDir, SafeName(TitleOf(rom)) + ".png");
        }
    }

    static class CoverFetcher
    {
        const string Base =
            "https://thumbnails.libretro.com/Nintendo%20-%20Super%20Nintendo%20Entertainment%20System/Named_Boxarts/";

        static List<string> indexNames;
        static readonly object indexLock = new object();

        public static List<string> Candidates(string title)
        {
            List<string> list = new List<string>();
            string n0 = title.Trim();
            string n1 = Regex.Replace(n0, @"\s*\[[^\]]*\]", "").Trim();
            string n2 = n1.Replace("(U)", "(USA)").Replace("(E)", "(Europe)").Replace("(J)", "(Japan)");
            list.Add(n0);
            list.Add(n1);
            list.Add(n2);
            if (n1.IndexOf('(') < 0)
            {
                list.Add(n1 + " (USA)");
                list.Add(n1 + " (Europe)");
                list.Add(n1 + " (Japan)");
            }
            return list.Distinct().ToList();
        }

        static string Norm(string s)
        {
            s = Regex.Replace(s, @"\[[^\]]*\]|\([^)]*\)", " ");
            s = Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();
            return s;
        }

        static int RegionScore(string name)
        {
            if (name.IndexOf("(USA", StringComparison.Ordinal) >= 0) return 0;
            if (name.IndexOf("(World", StringComparison.Ordinal) >= 0) return 1;
            if (name.IndexOf("(Europe", StringComparison.Ordinal) >= 0) return 2;
            if (name.IndexOf("(Japan", StringComparison.Ordinal) >= 0) return 3;
            return 4;
        }

        // List of all cover names available online (downloaded once, then cached on disk).
        static List<string> LoadIndex(string cacheFile)
        {
            lock (indexLock)
            {
                if (indexNames != null) return indexNames;
                List<string> names = new List<string>();
                try
                {
                    if (File.Exists(cacheFile))
                        names.AddRange(File.ReadAllLines(cacheFile, Encoding.UTF8));
                }
                catch { }
                if (names.Count == 0)
                {
                    try
                    {
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                        using (WebClient wc = new WebClient())
                        {
                            wc.Encoding = Encoding.UTF8;
                            wc.Headers[HttpRequestHeader.UserAgent] = "NexusLauncher/1.0";
                            string html = wc.DownloadString(Base);
                            foreach (Match m in Regex.Matches(html, "href=\"([^\"]+?)\\.png\""))
                            {
                                string n = WebUtility.HtmlDecode(Uri.UnescapeDataString(m.Groups[1].Value));
                                if (n.Length > 0) names.Add(n);
                            }
                            if (names.Count > 0)
                                File.WriteAllLines(cacheFile, names.ToArray(), Encoding.UTF8);
                        }
                    }
                    catch { }
                }
                indexNames = names;
                return names;
            }
        }

        static HashSet<string> Tokens(string norm)
        {
            return new HashSet<string>(norm.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        // Finds the online cover whose name best matches the file name.
        static string FindBest(string title, List<string> names)
        {
            string key = Norm(title);
            if (key.Length == 0 || names.Count == 0) return null;

            string best = null;
            int bestScore = int.MaxValue;
            foreach (string n in names)
            {
                if (Norm(n) == key)
                {
                    int sc = RegionScore(n);
                    if (sc < bestScore) { best = n; bestScore = sc; }
                }
            }
            if (best != null) return best;

            HashSet<string> a = Tokens(key);
            double bestJ = 0.0;
            foreach (string n in names)
            {
                HashSet<string> b = Tokens(Norm(n));
                if (b.Count == 0) continue;
                int inter = 0;
                foreach (string t in a) if (b.Contains(t)) inter++;
                int union = a.Count + b.Count - inter;
                if (union == 0) continue;
                double j = (double)inter / union;
                if (j > bestJ || (j == bestJ && best != null && RegionScore(n) < RegionScore(best)))
                {
                    bestJ = j;
                    best = n;
                }
            }
            return bestJ >= 0.8 ? best : null;
        }

        static bool TryDownload(string name, string destPath)
        {
            string url = Base + Uri.EscapeDataString(name) + ".png";
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "NexusLauncher/1.0";
                    byte[] data = wc.DownloadData(url);
                    if (data != null && data.Length > 200)
                    {
                        File.WriteAllBytes(destPath, data);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // Runs on a worker thread. Returns true if a cover was saved to destPath.
        public static bool Download(string title, string destPath, string indexFile)
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }

            string match = FindBest(title, LoadIndex(indexFile));
            if (match != null && TryDownload(match, destPath)) return true;

            foreach (string name in Candidates(title))
                if (TryDownload(Library.SafeName(name), destPath)) return true;
            return false;
        }
    }

    // ------------------------------------------------------------------- UI
    static class LogoArt
    {
        static Color Lerp(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Draw(Graphics g, Rectangle r)
        {
            float s = r.Width;
            using (GraphicsPath p = RoundRect(r, (int)(s * 0.22f)))
            using (SolidBrush bg = new SolidBrush(Theme.Surface))
                g.FillPath(bg, p);

            Color cyan = Color.FromArgb(56, 214, 255);
            float cx = r.X + s / 2f, cy = r.Y + s / 2f, R = s * 0.30f;
            double[] angles = new double[] { -90, -18, 54, 126, 198 };
            float lineW = Math.Max(1f, s * 0.012f);
            float nodeR = s * 0.045f;

            for (int i = 0; i < angles.Length; i++)
            {
                double rad = angles[i] * Math.PI / 180.0;
                float x = cx + R * (float)Math.Cos(rad);
                float y = cy + R * (float)Math.Sin(rad);
                Color col = Lerp(Theme.Accent, cyan, i / 4f);
                using (Pen pen = new Pen(Color.FromArgb(200, col), lineW))
                    g.DrawLine(pen, cx, cy, x, y);
                using (SolidBrush b = new SolidBrush(col))
                    g.FillEllipse(b, x - nodeR, y - nodeR, nodeR * 2, nodeR * 2);
            }

            float c1 = s * 0.10f, c2 = s * 0.045f;
            using (SolidBrush w = new SolidBrush(Color.FromArgb(240, 242, 250)))
                g.FillEllipse(w, cx - c1, cy - c1, c1 * 2, c1 * 2);
            using (SolidBrush d = new SolidBrush(Theme.Bg))
                g.FillEllipse(d, cx - c2, cy - c2, c2 * 2, c2 * 2);
        }
    }

    // Start screen shown while the library is empty.
    class Hero : Panel
    {
        public Button ImportButton;
        Label tip;
        const int ContentHeight = 400;

        public Hero()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;

            ImportButton = new Button();
            ImportButton.Text = "Adicionar jogos";
            ImportButton.FlatStyle = FlatStyle.Flat;
            ImportButton.FlatAppearance.BorderSize = 0;
            ImportButton.BackColor = Theme.Accent;
            ImportButton.ForeColor = Color.White;
            ImportButton.Font = new Font("Segoe UI Semibold", 12f);
            ImportButton.Size = new Size(240, 52);
            ImportButton.Cursor = Cursors.Hand;
            Controls.Add(ImportButton);

            tip = new Label();
            tip.AutoSize = true;
            tip.Text = "ou arraste os arquivos para c\u00e1  (.sfc  .smc  .zip)";
            tip.Font = new Font("Segoe UI", 9.5f);
            tip.ForeColor = Theme.Muted;
            tip.BackColor = Color.Transparent;
            Controls.Add(tip);

            Resize += delegate { LayoutAll(); };
            LayoutAll();
        }

        int Top0()
        {
            int top = (Height - ContentHeight) / 2;
            return top < 10 ? 10 : top;
        }

        void LayoutAll()
        {
            int cx = Width / 2;
            int top = Top0();
            ImportButton.Location = new Point(cx - ImportButton.Width / 2, top + 262);
            int tw = TextRenderer.MeasureText(tip.Text, tip.Font).Width;
            tip.Location = new Point(cx - tw / 2, top + 330);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int cx = Width / 2;
            int top = Top0();

            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(new Rectangle(cx - 300, top - 120, 600, 600));
                using (PathGradientBrush pg = new PathGradientBrush(gp))
                {
                    pg.CenterColor = Color.FromArgb(44, Theme.Accent);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, Theme.Accent) };
                    g.FillPath(pg, gp);
                }
            }

            LogoArt.Draw(g, new Rectangle(cx - 64, top, 128, 128));

            string title = "N E X U S";
            using (Font f = new Font("Segoe UI Semibold", 30f))
            using (SolidBrush b = new SolidBrush(Theme.Text))
            {
                SizeF sz = g.MeasureString(title, f);
                g.DrawString(title, f, b, cx - sz.Width / 2f, top + 148);
            }

            string sub = "Emulador de Super Nintendo";
            using (Font f = new Font("Segoe UI", 11f))
            using (SolidBrush b = new SolidBrush(Theme.Muted))
            {
                SizeF sz = g.MeasureString(sub, f);
                g.DrawString(sub, f, b, cx - sz.Width / 2f, top + 212);
            }
        }
    }

    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.Raised; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Raised; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Raised; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }

    class GameCard : Panel
    {
        public string RomPath;
        public PictureBox Cover;
        Label titleLabel;
        bool hover;

        public GameCard(string rom, string title, Image cover)
        {
            RomPath = rom;
            Width = 176;
            Height = 228;
            Margin = new Padding(10);
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;

            Cover = new PictureBox();
            Cover.SetBounds(8, 8, 160, 160);
            Cover.SizeMode = PictureBoxSizeMode.Zoom;
            Cover.BackColor = Theme.Raised;
            Cover.Image = cover;
            Controls.Add(Cover);

            titleLabel = new Label();
            titleLabel.SetBounds(8, 172, 160, 48);
            titleLabel.ForeColor = Theme.Text;
            titleLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            titleLabel.Text = title;
            titleLabel.AutoEllipsis = true;
            Controls.Add(titleLabel);

            foreach (Control c in new Control[] { this, Cover, titleLabel })
            {
                c.MouseEnter += delegate { SetHover(true); };
                c.MouseLeave += delegate { SetHover(false); };
            }
        }

        void SetHover(bool h)
        {
            bool inside = ClientRectangle.Contains(PointToClient(Cursor.Position));
            hover = h || inside;
            if (!inside) hover = false;
            BackColor = hover ? Theme.Raised : Theme.Surface;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(hover ? Theme.Accent : Theme.Border, hover ? 2f : 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    class MainForm : Form
    {
        Library lib = new Library();
        FlowLayoutPanel grid;
        Label empty;
        Panel header;
        Hero hero;
        TextBox search;
        Dictionary<string, Image> coverCache = new Dictionary<string, Image>();
        string corePath;

        public MainForm()
        {
            Text = "Nexus";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1040, 700);
            MinimumSize = new Size(640, 420);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            corePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NexusCore.exe");

            // ---- header
            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 76;
            header.BackColor = Theme.Bg;
            Controls.Add(header);

            Label logo = new Label();
            logo.Text = "N E X U S";
            logo.ForeColor = Theme.Text;
            logo.Font = new Font("Segoe UI Semibold", 18f);
            logo.AutoSize = true;
            logo.Location = new Point(28, 20);
            header.Controls.Add(logo);

            Button btnFolder = MakeButton("Adicionar pasta", false);
            Button btnRom = MakeButton("+  Adicionar jogos", true);
            header.Controls.Add(btnRom);
            header.Controls.Add(btnFolder);
            btnRom.Click += delegate { ImportFiles(); };
            btnFolder.Click += delegate { ImportFolder(); };

            search = new TextBox();
            search.BackColor = Theme.Surface;
            search.ForeColor = Theme.Text;
            search.BorderStyle = BorderStyle.FixedSingle;
            search.Font = new Font("Segoe UI", 10f);
            search.Width = 220;
            search.TextChanged += delegate { Rebuild(); };
            header.Controls.Add(search);

            Label hint = new Label();
            hint.Text = "buscar";
            hint.ForeColor = Theme.Muted;
            hint.AutoSize = true;
            hint.Font = new Font("Segoe UI", 8f);
            header.Controls.Add(hint);

            MethodInvoker layoutHeader = delegate
            {
                btnRom.Location = new Point(header.Width - btnRom.Width - 28, 20);
                btnFolder.Location = new Point(btnRom.Left - btnFolder.Width - 10, 20);
                search.Location = new Point(btnFolder.Left - search.Width - 16, 24);
                hint.Location = new Point(search.Left, 6);
            };
            header.Resize += delegate { layoutHeader(); };
            layoutHeader();

            // ---- grid
            grid = new FlowLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.AutoScroll = true;
            grid.Padding = new Padding(18, 8, 18, 18);
            grid.BackColor = Theme.Bg;
            Controls.Add(grid);
            grid.BringToFront();

            empty = new Label();
            empty.Text = "Nenhum jogo encontrado";
            empty.Visible = false;
            empty.ForeColor = Theme.Muted;
            empty.Font = new Font("Segoe UI", 12f);
            empty.TextAlign = ContentAlignment.MiddleCenter;
            empty.Dock = DockStyle.Fill;
            empty.BackColor = Theme.Bg;
            Controls.Add(empty);
            empty.BringToFront();

            hero = new Hero();
            hero.Dock = DockStyle.Fill;
            hero.ImportButton.Click += delegate { ImportFiles(); };
            Controls.Add(hero);
            hero.BringToFront();

            foreach (Control dropTarget in new Control[] { this, header, grid, empty, hero })
            {
                dropTarget.AllowDrop = true;
                dropTarget.DragEnter += OnDragEnterFiles;
                dropTarget.DragDrop += OnDragDropFiles;
            }

            Rebuild();
        }

        void OnDragEnterFiles(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        void OnDragDropFiles(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null) AddPaths(files);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                if (Native.DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0)
                    Native.DwmSetWindowAttribute(Handle, 19, ref on, 4);
            }
            catch { }
        }

        Button MakeButton(string text, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = primary ? Theme.Accent : Theme.Border;
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = primary ? Theme.Accent : Theme.Raised;
            b.ForeColor = primary ? Color.White : Theme.Text;
            b.Font = new Font("Segoe UI", 9.5f);
            b.Size = new Size(primary ? 130 : 112, 34);
            b.Cursor = Cursors.Hand;
            return b;
        }

        // ---------------------------------------------------------- importing
        static readonly string[] RomExts = new string[] { ".sfc", ".smc", ".swc", ".fig", ".bs", ".zip" };

        static bool IsRom(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(RomExts, ext) >= 0;
        }

        void ImportFiles()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Adicionar jogos";
                dlg.Multiselect = true;
                dlg.Filter = "Jogos de SNES (*.sfc;*.smc;*.swc;*.fig;*.bs;*.zip)|*.sfc;*.smc;*.swc;*.fig;*.bs;*.zip|Todos os arquivos (*.*)|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddPaths(dlg.FileNames);
            }
        }

        void ImportFolder()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Escolha uma pasta com os seus jogos";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddPaths(new string[] { dlg.SelectedPath });
            }
        }

        void AddPaths(string[] paths)
        {
            int added = 0;
            foreach (string p in paths)
            {
                try
                {
                    if (Directory.Exists(p))
                    {
                        foreach (string f in Directory.GetFiles(p, "*.*", SearchOption.AllDirectories))
                            if (IsRom(f) && lib.Add(f)) added++;
                    }
                    else if (File.Exists(p) && IsRom(p))
                    {
                        if (lib.Add(p)) added++;
                    }
                }
                catch { }
            }
            if (added > 0)
            {
                lib.Roms.Sort(delegate(string a, string b)
                {
                    return string.Compare(Library.TitleOf(a), Library.TitleOf(b), StringComparison.OrdinalIgnoreCase);
                });
                lib.Save();
                Rebuild();
            }
        }

        // ------------------------------------------------------------- covers
        Image Placeholder(string title)
        {
            Bitmap bmp = new Bitmap(160, 160);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int h = 0;
                foreach (char c in title) h = (h * 31 + c) & 0x7fffffff;
                Color c1 = Color.FromArgb(40 + h % 40, 30 + (h / 7) % 30, 90 + (h / 13) % 80);
                Color c2 = Color.FromArgb(20, 24, 40);
                using (LinearGradientBrush br = new LinearGradientBrush(new Rectangle(0, 0, 160, 160), c1, c2, 55f))
                    g.FillRectangle(br, 0, 0, 160, 160);

                string initials = "";
                foreach (string w in title.Split(new char[] { ' ', '-', '_', '(' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (char.IsLetterOrDigit(w[0])) initials += char.ToUpper(w[0]);
                    if (initials.Length >= 3) break;
                }
                if (initials.Length == 0) initials = "?";
                using (Font f = new Font("Segoe UI Semibold", 30f))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(200, 230, 232, 245)))
                {
                    SizeF sz = g.MeasureString(initials, f);
                    g.DrawString(initials, f, tb, (160 - sz.Width) / 2f, (160 - sz.Height) / 2f);
                }
            }
            return bmp;
        }

        Image LoadCoverFile(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                byte[] data = File.ReadAllBytes(file);
                using (MemoryStream ms = new MemoryStream(data))
                using (Image img = Image.FromStream(ms))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        void ApplyCover(GameCard card, string rom)
        {
            string file = lib.CoverPath(rom);
            Image img = LoadCoverFile(file);
            if (img != null) { card.Cover.Image = img; return; }
            card.Cover.Image = Placeholder(Library.TitleOf(rom));

            string title = Library.TitleOf(rom);
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = CoverFetcher.Download(title, file, Path.Combine(lib.CoversDir, "_index.txt"));
                if (!ok) return;
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        if (card.IsDisposed) return;
                        Image got = LoadCoverFile(file);
                        if (got != null) card.Cover.Image = got;
                    }));
                }
                catch { }
            });
        }

        // ------------------------------------------------------------- layout
        void Rebuild()
        {
            string filter = search.Text.Trim();
            grid.SuspendLayout();
            foreach (Control c in grid.Controls.Cast<Control>().ToList())
            {
                grid.Controls.Remove(c);
                c.Dispose();
            }

            int shown = 0;
            foreach (string rom in lib.Roms)
            {
                string title = Library.TitleOf(rom);
                if (filter.Length > 0 && title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                GameCard card = new GameCard(rom, title, null);
                card.ContextMenuStrip = BuildMenu(card);
                EventHandler play = delegate { Play(card.RomPath); };
                card.Click += play;
                card.Cover.Click += play;
                foreach (Control c in card.Controls) c.Click += play;
                grid.Controls.Add(card);
                ApplyCover(card, rom);
                shown++;
            }
            grid.ResumeLayout();

            bool has = lib.Roms.Count > 0;
            header.Visible = has;
            grid.Visible = has;
            hero.Visible = !has;
            empty.Visible = has && shown == 0;
            if (empty.Visible) empty.BringToFront();
        }

        ContextMenuStrip BuildMenu(GameCard card)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.BackColor = Theme.Surface;
            m.ForeColor = Theme.Text;
            m.ShowImageMargin = false;
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());

            ToolStripMenuItem play = new ToolStripMenuItem("Jogar");
            play.Click += delegate { Play(card.RomPath); };
            ToolStripMenuItem choose = new ToolStripMenuItem("Escolher capa...");
            choose.Click += delegate { ChooseCover(card); };
            ToolStripMenuItem reload = new ToolStripMenuItem("Baixar capa de novo");
            reload.Click += delegate
            {
                try { File.Delete(lib.CoverPath(card.RomPath)); } catch { }
                ApplyCover(card, card.RomPath);
            };
            ToolStripMenuItem remove = new ToolStripMenuItem("Remover da biblioteca");
            remove.Click += delegate
            {
                lib.Roms.RemoveAll(delegate(string r) { return string.Equals(r, card.RomPath, StringComparison.OrdinalIgnoreCase); });
                lib.Save();
                Rebuild();
            };
            m.Items.Add(play);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(choose);
            m.Items.Add(reload);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(remove);
            return m;
        }

        void ChooseCover(GameCard card)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Escolher imagem de capa";
                dlg.Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Image img = Image.FromFile(dlg.FileName))
                    using (Bitmap copy = new Bitmap(img))
                        copy.Save(lib.CoverPath(card.RomPath), ImageFormat.Png);
                    ApplyCover(card, card.RomPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "N\u00e3o foi poss\u00edvel usar esta imagem.\r\n" + ex.Message, "Nexus");
                }
            }
        }

        // --------------------------------------------------------------- play
        void Play(string rom)
        {
            if (!File.Exists(corePath))
            {
                MessageBox.Show(this, "O NexusCore.exe n\u00e3o foi encontrado ao lado do Nexus.exe.", "Nexus");
                return;
            }
            if (!File.Exists(rom))
            {
                MessageBox.Show(this, "Este arquivo n\u00e3o existe mais:\r\n" + rom, "Nexus");
                return;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(corePath, "\"" + rom + "\"");
                psi.WorkingDirectory = Path.GetDirectoryName(corePath);
                psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                Hide();
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { p.WaitForExit(); } catch { }
                    try
                    {
                        BeginInvoke(new MethodInvoker(delegate { Show(); Activate(); }));
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "N\u00e3o foi poss\u00edvel abrir o emulador.\r\n" + ex.Message, "Nexus");
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
