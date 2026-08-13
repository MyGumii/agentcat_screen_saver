using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace AgentCatScreenSaver
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "/c";
            if (mode == "/snapshot")
            {
                string output = args.Length > 1 ? args[1] : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AgentCatScreenSaver", "lockscreen-preview.png");
                Saver.SaveSnapshot(output);
            }
            else if (mode.StartsWith("/s") || mode == "/test")
                Application.Run(new Saver(mode == "/test"));
            else
                MessageBox.Show(
                    "Agent Cat + Herdr animated screen saver\n\nUse /test to preview or install.ps1 to configure.",
                    "Agent Cat");
        }
    }

    sealed class Saver : Form
    {
        readonly System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        readonly Random random = new Random();
        readonly List<PointF> stars = new List<PointF>();

        DateTime inputArmedAt;
        DateTime lastAgentCatRefresh = DateTime.MinValue;
        Point initialMouse;
        readonly bool preview;
        float phase;
        int refreshBusy;
        Data data = new Data();
        HerdrState herdr = new HerdrState();
        Image catSheet;

        public Saver(bool isPreview)
        {
            preview = isPreview;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(8, 12, 27);
            KeyPreview = true;
            FormBorderStyle = preview ? FormBorderStyle.Sizable : FormBorderStyle.None;
            TopMost = !preview;
            ShowInTaskbar = preview;
            WindowState = preview ? FormWindowState.Normal : FormWindowState.Maximized;

            if (preview)
            {
                ClientSize = new Size(1280, 720);
                StartPosition = FormStartPosition.CenterScreen;
                Text = "Agent Cat + Herdr 화면보호기 미리보기";
            }
            else
            {
                Cursor.Hide();
            }

            try
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CheeseCatRunSheet");
                if (stream != null) catSheet = Image.FromStream(stream);
            }
            catch { }

            KeyDown += delegate { if (!preview) Application.Exit(); };
            MouseDown += delegate { if (!preview && DateTime.UtcNow > inputArmedAt) Application.Exit(); };
            MouseMove += delegate
            {
                if (preview || DateTime.UtcNow < inputArmedAt) return;
                if (Math.Abs(Cursor.Position.X - initialMouse.X) > 8 ||
                    Math.Abs(Cursor.Position.Y - initialMouse.Y) > 8)
                    Application.Exit();
            };

            animationTimer.Interval = 30;
            animationTimer.Tick += delegate
            {
                phase += .12f;
                Invalidate();
            };

            refreshTimer.Interval = 2000;
            refreshTimer.Tick += delegate { QueueRefresh(false); };

            Shown += delegate
            {
                inputArmedAt = DateTime.UtcNow.AddSeconds(1);
                initialMouse = Cursor.Position;
                for (int i = 0; i < 70; i++)
                    stars.Add(new PointF(random.Next(Math.Max(1, Width)), random.Next(Math.Max(1, Height))));
                animationTimer.Start();
                refreshTimer.Start();
                QueueRefresh(true);
            };
        }

        public static void SaveSnapshot(string outputPath)
        {
            string fullPath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            using (Saver saver = new Saver(true))
            {
                saver.FormBorderStyle = FormBorderStyle.None;
                saver.ClientSize = new Size(1920, 1080);
                saver.data = Data.Load();
                saver.herdr = HerdrState.Load();
                saver.phase = (DateTime.Now.Minute % 6 + .35f) / 7.5f;
                saver.stars.Clear();
                Random seeded = new Random(DateTime.Now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture).GetHashCode());
                for (int i = 0; i < 100; i++)
                    saver.stars.Add(new PointF(seeded.Next(1920), seeded.Next(1080)));

                using (Bitmap bitmap = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    saver.DrawScene(graphics);
                    bitmap.Save(fullPath, ImageFormat.Png);
                }
            }
        }

        void QueueRefresh(bool forceAgentCat)
        {
            if (Interlocked.CompareExchange(ref refreshBusy, 1, 0) != 0) return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    HerdrState nextHerdr = HerdrState.Load();
                    Data nextData = null;
                    if (forceAgentCat || DateTime.UtcNow - lastAgentCatRefresh >= TimeSpan.FromSeconds(5))
                    {
                        nextData = Data.Load();
                        lastAgentCatRefresh = DateTime.UtcNow;
                    }

                    herdr = nextHerdr;
                    if (nextData != null) data = nextData;
                }
                finally
                {
                    Interlocked.Exchange(ref refreshBusy, 0);
                    try
                    {
                        if (!IsDisposed && IsHandleCreated)
                            BeginInvoke((Action)delegate { Invalidate(); });
                    }
                    catch { }
                }
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            animationTimer.Stop();
            refreshTimer.Stop();
            if (!preview) Cursor.Show();
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawScene(e.Graphics);
        }

        void DrawScene(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(8, 12, 27));

            foreach (PointF point in stars)
            {
                int alpha = (int)(35 + 70 * (Math.Sin(phase + point.X) + 1) / 2);
                using (Brush brush = new SolidBrush(Color.FromArgb(alpha, 180, 210, 255)))
                    g.FillEllipse(brush, point.X, point.Y, 2, 2);
            }

            float scale = Math.Max(.65f, Math.Min(Width / 1280f, Height / 720f));
            using (Font font = new Font("Segoe UI", 27 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.White))
                g.DrawString("AGENT CAT", font, brush, 50 * scale, 35 * scale);
            using (Font font = new Font("Segoe UI", 13 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(150, 170, 200)))
                g.DrawString("LOCAL ACTIVITY MONITOR", font, brush, 245 * scale, 50 * scale);

            DrawCat(g, Width * .255f, Height * .50f, Math.Min(Width, Height) * .34f, scale);
            DrawPanel(g, scale);

            using (Font font = new Font("Malgun Gothic", 10 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(110, 135, 165)))
                g.DrawString("로컬 상태만 표시 · 입력하면 종료", font, brush,
                    Width / 2f - 90 * scale, Height - 31 * scale);
        }

        void DrawPanel(Graphics g, float scale)
        {
            float x = Width * .505f;
            float y = Height * .085f;
            float w = Width * .46f;
            float h = Height * .825f;

            using (Brush panel = new SolidBrush(Color.FromArgb(112, 18, 30, 52)))
                g.FillRoundedRectangle(panel, x, y, w, h, 28 * scale);

            using (Font font = new Font("Malgun Gothic", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(222, 232, 248)))
                g.DrawString("Agent Cat 사용량", font, brush, x + 22 * scale, y + 18 * scale);

            float gap = 12 * scale;
            float innerX = x + 20 * scale;
            float cardWidth = (w - 40 * scale - gap) / 2f;
            float cardY = y + 51 * scale;
            float cardHeight = 140 * scale;

            ProviderCard(g, innerX, cardY, cardWidth, cardHeight,
                "CODEX", data.codex, data.today, data.week, data.month, data.quota,
                data.codexCount, Color.FromArgb(83, 211, 150), scale);
            ProviderCard(g, innerX + cardWidth + gap, cardY, cardWidth, cardHeight,
                "CLAUDE", data.claude, data.claudeToday, data.claudeWeek, data.claudeMonth,
                data.claudeQuota, data.claudeCount, Color.FromArgb(224, 145, 95), scale);

            float dividerY = y + 208 * scale;
            using (Pen divider = new Pen(Color.FromArgb(55, 133, 155, 188), 1))
                g.DrawLine(divider, x + 20 * scale, dividerY, x + w - 20 * scale, dividerY);

            DrawHerdr(g, x + 20 * scale, y + 222 * scale, w - 40 * scale,
                h - 239 * scale, scale);
        }

        void ProviderCard(Graphics g, float x, float y, float w, float h, string name,
            string status, long today, long week, long month, double? quota, int active,
            Color accent, float scale)
        {
            using (Brush background = new SolidBrush(Color.FromArgb(78, 63, 83, 116)))
                g.FillRoundedRectangle(background, x, y, w, h, 17 * scale);

            bool connected = status == "ok";
            Color muted = Color.FromArgb(112, 132, 159);
            using (Brush dot = new SolidBrush(connected ? accent : muted))
                g.FillEllipse(dot, x + 14 * scale, y + 15 * scale, 8 * scale, 8 * scale);
            using (Font font = new Font("Segoe UI", 13 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(241, 246, 255)))
                g.DrawString(name, font, brush, x + 28 * scale, y + 8 * scale);

            string state = connected ? (active > 0 ? "RUNNING" : "CONNECTED") :
                status == "no_telemetry_yet" ? "WAITING" : "OFFLINE";
            using (Font font = new Font("Segoe UI", 8 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(connected ? accent : muted))
            {
                SizeF size = g.MeasureString(state, font);
                g.DrawString(state, font, brush, x + w - size.Width - 12 * scale, y + 12 * scale);
            }

            using (Font font = new Font("Segoe UI", 23 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.White))
                g.DrawString(connected ? Fmt(today) : "—", font, brush, x + 14 * scale, y + 35 * scale);
            using (Font font = new Font("Malgun Gothic", 9 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(155, 176, 204)))
            {
                g.DrawString("오늘", font, brush, x + 15 * scale, y + 67 * scale);
                g.DrawString("7일 " + (connected ? Fmt(week) : "—") + "  ·  30일 " +
                    (connected ? Fmt(month) : "—"), font, brush, x + 15 * scale, y + 91 * scale);
                string quotaText = quota.HasValue ? "쿼터 잔여 " + quota.Value.ToString("0.#") + "%" :
                    "정확한 쿼터 정보 없음";
                g.DrawString(quotaText, font, brush, x + 15 * scale, y + 115 * scale);
            }
        }

        void DrawHerdr(Graphics g, float x, float y, float w, float h, float scale)
        {
            Color live = Color.FromArgb(91, 218, 157);
            Color dim = Color.FromArgb(112, 132, 159);

            using (Brush dot = new SolidBrush(herdr.available ? live : dim))
                g.FillEllipse(dot, x, y + 5 * scale, 9 * scale, 9 * scale);
            using (Font font = new Font("Segoe UI", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(237, 243, 253)))
                g.DrawString("HERDR", font, brush, x + 17 * scale, y - 2 * scale);

            string state = herdr.available ? "LIVE" : herdr.installed ? "OFFLINE" : "NOT INSTALLED";
            using (Font font = new Font("Segoe UI", 9 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(herdr.available ? live : dim))
            {
                SizeF size = g.MeasureString(state, font);
                g.DrawString(state, font, brush, x + w - size.Width, y + 1 * scale);
            }

            string topology = herdr.available
                ? herdr.workspaceCount + " workspace · " + herdr.tabCount + " tab · " + herdr.paneCount + " panes"
                : "로컬 Herdr 스냅샷 없음";
            using (Font font = new Font("Segoe UI", 9 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(132, 154, 183)))
                g.DrawString(topology, font, brush, x, y + 23 * scale);

            float chipsY = y + 46 * scale;
            float chipsGap = 6 * scale;
            float chipW = (w - chipsGap * 4) / 5f;
            SummaryChip(g, x, chipsY, chipW, herdr.working, "작업", Color.FromArgb(83, 211, 150), scale);
            SummaryChip(g, x + (chipW + chipsGap), chipsY, chipW, herdr.blocked, "막힘", Color.FromArgb(246, 101, 112), scale);
            SummaryChip(g, x + (chipW + chipsGap) * 2, chipsY, chipW, herdr.done, "완료", Color.FromArgb(103, 163, 255), scale);
            SummaryChip(g, x + (chipW + chipsGap) * 3, chipsY, chipW, herdr.idle, "대기", Color.FromArgb(145, 158, 180), scale);
            SummaryChip(g, x + (chipW + chipsGap) * 4, chipsY, chipW, herdr.unknown, "미확인", Color.FromArgb(189, 149, 235), scale);

            float rowsY = y + 94 * scale;
            if (!herdr.available)
            {
                using (Brush background = new SolidBrush(Color.FromArgb(55, 63, 83, 116)))
                    g.FillRoundedRectangle(background, x, rowsY, w, 64 * scale, 14 * scale);
                using (Font font = new Font("Malgun Gothic", 10 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
                using (Brush brush = new SolidBrush(Color.FromArgb(145, 164, 191)))
                    g.DrawString("Herdr가 실행되면 에이전트 상태가 여기에 표시됩니다.", font, brush,
                        x + 15 * scale, rowsY + 22 * scale);
                return;
            }

            if (herdr.agents.Count == 0)
            {
                using (Brush background = new SolidBrush(Color.FromArgb(55, 63, 83, 116)))
                    g.FillRoundedRectangle(background, x, rowsY, w, 58 * scale, 14 * scale);
                using (Font font = new Font("Malgun Gothic", 10 * scale, FontStyle.Regular, GraphicsUnit.Pixel))
                using (Brush brush = new SolidBrush(Color.FromArgb(145, 164, 191)))
                    g.DrawString("감지된 에이전트가 없습니다.", font, brush, x + 15 * scale, rowsY + 20 * scale);
                return;
            }

            int visible = Math.Min(4, herdr.agents.Count);
            float rowHeight = 49 * scale;
            float rowGap = 7 * scale;
            for (int i = 0; i < visible; i++)
                DrawHerdrAgent(g, x, rowsY + i * (rowHeight + rowGap), w, rowHeight,
                    herdr.agents[i], scale);

            if (herdr.agents.Count > visible)
            {
                using (Font font = new Font("Segoe UI", 9 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush brush = new SolidBrush(Color.FromArgb(133, 154, 182)))
                    g.DrawString("+" + (herdr.agents.Count - visible) + " MORE", font, brush,
                        x, rowsY + visible * (rowHeight + rowGap));
            }
        }

        void SummaryChip(Graphics g, float x, float y, float w, int count, string label,
            Color accent, float scale)
        {
            int alpha = count > 0 ? 58 : 28;
            using (Brush background = new SolidBrush(Color.FromArgb(alpha, accent)))
                g.FillRoundedRectangle(background, x, y, w, 36 * scale, 11 * scale);
            using (Font countFont = new Font("Segoe UI", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush countBrush = new SolidBrush(count > 0 ? accent : Color.FromArgb(116, 132, 157)))
                g.DrawString(count.ToString(CultureInfo.InvariantCulture), countFont, countBrush,
                    x + 9 * scale, y + 7 * scale);
            using (Font labelFont = new Font("Malgun Gothic", 8 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(165, 183, 207)))
                g.DrawString(label, labelFont, labelBrush, x + 29 * scale, y + 11 * scale);
        }

        void DrawHerdrAgent(Graphics g, float x, float y, float w, float h,
            HerdrAgent agent, float scale)
        {
            Color accent = StatusColor(agent.status);
            using (Brush background = new SolidBrush(Color.FromArgb(agent.focused ? 76 : 52, 63, 83, 116)))
                g.FillRoundedRectangle(background, x, y, w, h, 13 * scale);
            using (Brush bar = new SolidBrush(accent))
                g.FillRoundedRectangle(bar, x, y + 7 * scale, 4 * scale, h - 14 * scale, 4 * scale);

            string left = agent.agent.ToUpperInvariant() + " · " + agent.location;
            using (Font font = new Font("Segoe UI", 11 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(Color.FromArgb(231, 238, 249)))
            {
                RectangleF bounds = new RectangleF(x + 14 * scale, y + 8 * scale,
                    w * .62f, 18 * scale);
                using (StringFormat format = new StringFormat())
                {
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    format.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(left, font, brush, bounds, format);
                }
            }

            if (agent.focused)
            {
                using (Font font = new Font("Segoe UI", 8 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush brush = new SolidBrush(Color.FromArgb(111, 182, 255)))
                    g.DrawString("FOCUS", font, brush, x + 14 * scale, y + 29 * scale);
            }

            string status = agent.status.ToUpperInvariant();
            using (Font font = new Font("Segoe UI", 9 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush brush = new SolidBrush(accent))
            {
                SizeF size = g.MeasureString(status, font);
                g.DrawString(status, font, brush, x + w - size.Width - 13 * scale, y + 17 * scale);
            }
        }

        void DrawCat(Graphics g, float centerX, float centerY, float size, float scale)
        {
            if (catSheet == null) return;

            bool herdrWorking = herdr.available && herdr.working > 0;
            bool sprint = data.stage == "sprinting" || herdrWorking;
            bool providerActive = data.codexCount + data.claudeCount + data.geminiCount > 0;
            bool run = sprint || data.stage == "running" || data.stage == "walking" || providerActive;
            int row = sprint ? 2 : run ? 1 : 0;
            float frameRate = sprint ? 7.5f : run ? 6f : 3.5f;
            int frame = ((int)(phase * frameRate)) % 6;

            float sourceWidth = catSheet.Width / 6f;
            float sourceHeight = catSheet.Height / 5f;
            float drawWidth = size * 1.62f;
            float drawHeight = drawWidth * (sourceHeight / sourceWidth);
            float x = centerX - drawWidth * .5f;
            float y = centerY - drawHeight * .5f + (float)Math.Sin(phase * frameRate) * 3 * scale;

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (Brush shadow = new SolidBrush(Color.FromArgb(52, 0, 0, 0)))
                g.FillEllipse(shadow, x + drawWidth * .13f, y + drawHeight * .79f,
                    drawWidth * .74f, drawHeight * .12f);

            RectangleF destination = new RectangleF(x, y, drawWidth, drawHeight);
            RectangleF source = new RectangleF(frame * sourceWidth, row * sourceHeight,
                sourceWidth, sourceHeight);
            g.DrawImage(catSheet, destination, source, GraphicsUnit.Pixel);

            if (herdr.blocked > 0)
                DrawCatBadge(g, x + drawWidth * .67f, y - 7 * scale,
                    "!  BLOCKED " + herdr.blocked, Color.FromArgb(246, 101, 112), scale);
            else if (herdr.done > 0)
                DrawCatBadge(g, x + drawWidth * .67f, y - 7 * scale,
                    "DONE " + herdr.done, Color.FromArgb(103, 163, 255), scale);
        }

        void DrawCatBadge(Graphics g, float x, float y, string text, Color accent, float scale)
        {
            using (Font font = new Font("Segoe UI", 10 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF measured = g.MeasureString(text, font);
                using (Brush background = new SolidBrush(Color.FromArgb(205, 28, 38, 59)))
                    g.FillRoundedRectangle(background, x, y, measured.Width + 22 * scale,
                        29 * scale, 12 * scale);
                using (Pen outline = new Pen(Color.FromArgb(190, accent), 1.5f * scale))
                    g.DrawRoundedRectangle(outline, x, y, measured.Width + 22 * scale,
                        29 * scale, 12 * scale);
                using (Brush brush = new SolidBrush(accent))
                    g.DrawString(text, font, brush, x + 11 * scale, y + 7 * scale);
            }
        }

        static Color StatusColor(string status)
        {
            switch (status)
            {
                case "working": return Color.FromArgb(83, 211, 150);
                case "blocked": return Color.FromArgb(246, 101, 112);
                case "done": return Color.FromArgb(103, 163, 255);
                case "idle": return Color.FromArgb(145, 158, 180);
                default: return Color.FromArgb(189, 149, 235);
            }
        }

        static string Fmt(long value)
        {
            if (value >= 1000000000) return (value / 1e9).ToString("0.00", CultureInfo.InvariantCulture) + "B";
            if (value >= 1000000) return (value / 1e6).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            if (value >= 1000) return (value / 1e3).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            return value.ToString("N0");
        }
    }

    sealed class Data
    {
        public string stage = "sleeping", codex = "unknown", claude = "unknown", gemini = "unknown";
        public long today, week, month, claudeToday, claudeWeek, claudeMonth;
        public int processes, codexCount, claudeCount, geminiCount;
        public double? quota, claudeQuota;

        public static Data Load()
        {
            string json = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:8765/v1/snapshot");
                request.Timeout = 1400;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    json = reader.ReadToEnd();
            }
            catch
            {
                try
                {
                    json = File.ReadAllText(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".agentcat", "latest-snapshot.json"));
                }
                catch { }
            }

            if (String.IsNullOrWhiteSpace(json)) return new Data();
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
                Data data = new Data();
                Dictionary<string, object> activity = Dict(root, "activity");
                data.stage = Txt(activity, "motionStage", "sleeping");
                data.processes = (int)Num(activity, "processCount");
                Dictionary<string, object> counts = Dict(activity, "countsByProvider");
                data.codexCount = (int)Num(counts, "codex");
                data.claudeCount = (int)Num(counts, "claude");
                data.geminiCount = (int)Num(counts, "gemini");

                Dictionary<string, object> providers = Dict(root, "providers");
                Dictionary<string, object> codex = Dict(providers, "codex");
                Dictionary<string, object> claude = Dict(providers, "claude");
                data.codex = Txt(codex, "status", "unknown");
                data.claude = Txt(claude, "status", "unknown");
                data.gemini = Txt(Dict(providers, "gemini"), "status", "unknown");

                Dictionary<string, object> codexTokens = Dict(codex, "tokens");
                data.today = Num(codexTokens, "today");
                data.week = Num(codexTokens, "week");
                data.month = Num(codexTokens, "month");
                Dictionary<string, object> claudeTokens = Dict(claude, "tokens");
                data.claudeToday = Num(claudeTokens, "today");
                data.claudeWeek = Num(claudeTokens, "week");
                data.claudeMonth = Num(claudeTokens, "month");
                data.quota = Quota(codex);
                data.claudeQuota = Quota(claude);
                return data;
            }
            catch { return new Data(); }
        }

        static Dictionary<string, object> Dict(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary != null && dictionary.TryGetValue(key, out value))
                return value as Dictionary<string, object> ?? new Dictionary<string, object>();
            return new Dictionary<string, object>();
        }

        static string Txt(Dictionary<string, object> dictionary, string key, string fallback)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : fallback;
        }

        static long Num(Dictionary<string, object> dictionary, string key)
        {
            object value;
            try
            {
                return dictionary != null && dictionary.TryGetValue(key, out value) && value != null
                    ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : 0;
            }
            catch { return 0; }
        }

        static double? Quota(Dictionary<string, object> provider)
        {
            Dictionary<string, object> limits = Dict(provider, "limits");
            object raw;
            object[] quotas = limits.TryGetValue("quotas", out raw) ? raw as object[] : null;
            double? first = null;
            if (quotas != null)
            {
                foreach (object item in quotas)
                {
                    Dictionary<string, object> quota = item as Dictionary<string, object>;
                    if (quota == null || !quota.ContainsKey("remainingPercent") || quota["remainingPercent"] == null)
                        continue;
                    double value = Convert.ToDouble(quota["remainingPercent"], CultureInfo.InvariantCulture);
                    if (!first.HasValue) first = value;
                    if (Txt(quota, "window", "") == "7d") return value;
                }
            }
            return first;
        }
    }

    sealed class HerdrAgent
    {
        public string agent = "agent";
        public string status = "unknown";
        public string location = "workspace";
        public bool focused;
    }

    sealed class HerdrState
    {
        public bool installed;
        public bool available;
        public int workspaceCount, tabCount, paneCount;
        public int working, blocked, done, idle, unknown;
        public readonly List<HerdrAgent> agents = new List<HerdrAgent>();

        public static HerdrState Load()
        {
            HerdrState state = new HerdrState();
            string executable = FindExecutable();
            if (String.IsNullOrEmpty(executable)) return state;
            state.installed = true;

            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "api snapshot",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                StringBuilder output = new StringBuilder();
                using (Process process = new Process())
                {
                    process.StartInfo = info;
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                    {
                        if (args.Data != null) lock (output) output.AppendLine(args.Data);
                    };
                    process.Start();
                    process.BeginOutputReadLine();
                    if (!process.WaitForExit(1500))
                    {
                        try { process.Kill(); } catch { }
                        return state;
                    }
                    process.WaitForExit();
                    if (process.ExitCode != 0) return state;
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                Dictionary<string, object> root = serializer.DeserializeObject(output.ToString()) as Dictionary<string, object>;
                Dictionary<string, object> result = Dict(root, "result");
                Dictionary<string, object> snapshot = Dict(result, "snapshot");
                if (snapshot.Count == 0) return state;

                object[] workspaces = Arr(snapshot, "workspaces");
                object[] tabs = Arr(snapshot, "tabs");
                object[] panes = Arr(snapshot, "panes");
                object[] agents = Arr(snapshot, "agents");
                state.workspaceCount = workspaces.Length;
                state.tabCount = tabs.Length;
                state.paneCount = panes.Length;

                Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (object item in workspaces)
                {
                    Dictionary<string, object> workspace = item as Dictionary<string, object>;
                    if (workspace == null) continue;
                    string id = Txt(workspace, "workspace_id", "");
                    if (id.Length > 0) labels[id] = Txt(workspace, "label", id);
                }

                foreach (object item in agents)
                {
                    Dictionary<string, object> rawAgent = item as Dictionary<string, object>;
                    if (rawAgent == null) continue;
                    HerdrAgent agent = new HerdrAgent();
                    agent.agent = Txt(rawAgent, "agent", "agent");
                    agent.status = Txt(rawAgent, "agent_status", "unknown").ToLowerInvariant();
                    agent.focused = Bool(rawAgent, "focused");
                    string workspaceId = Txt(rawAgent, "workspace_id", "workspace");
                    string label;
                    if (!labels.TryGetValue(workspaceId, out label)) label = workspaceId;
                    string leaf = SafeLeaf(Txt(rawAgent, "cwd", ""));
                    agent.location = label;
                    if (leaf.Length > 0 && !String.Equals(leaf, label, StringComparison.OrdinalIgnoreCase))
                        agent.location += " · " + leaf;
                    state.agents.Add(agent);
                    state.Count(agent.status);
                }

                state.agents.Sort(delegate(HerdrAgent left, HerdrAgent right)
                {
                    int order = Priority(left.status).CompareTo(Priority(right.status));
                    if (order != 0) return order;
                    if (left.focused != right.focused) return left.focused ? -1 : 1;
                    return String.Compare(left.agent, right.agent, StringComparison.OrdinalIgnoreCase);
                });
                state.available = true;
            }
            catch { }

            return state;
        }

        void Count(string status)
        {
            switch (status)
            {
                case "working": working++; break;
                case "blocked": blocked++; break;
                case "done": done++; break;
                case "idle": idle++; break;
                default: unknown++; break;
            }
        }

        static int Priority(string status)
        {
            switch (status)
            {
                case "blocked": return 0;
                case "done": return 1;
                case "working": return 2;
                case "idle": return 3;
                default: return 4;
            }
        }

        static string FindExecutable()
        {
            string explicitPath = Environment.GetEnvironmentVariable("HERDR_EXE");
            if (!String.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;

            string standard = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Herdr", "bin", "herdr.exe");
            if (File.Exists(standard)) return standard;

            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string folder in path.Split(Path.PathSeparator))
            {
                try
                {
                    string candidate = Path.Combine(folder.Trim(), "herdr.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }

        static Dictionary<string, object> Dict(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary != null && dictionary.TryGetValue(key, out value))
                return value as Dictionary<string, object> ?? new Dictionary<string, object>();
            return new Dictionary<string, object>();
        }

        static object[] Arr(Dictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value)
                ? value as object[] ?? new object[0] : new object[0];
        }

        static string Txt(Dictionary<string, object> dictionary, string key, string fallback)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : fallback;
        }

        static bool Bool(Dictionary<string, object> dictionary, string key)
        {
            object value;
            try
            {
                return dictionary != null && dictionary.TryGetValue(key, out value) && value != null &&
                    Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch { return false; }
        }

        static string SafeLeaf(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "";
            try
            {
                string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string leaf = Path.GetFileName(trimmed);
                return String.IsNullOrWhiteSpace(leaf) ? "workspace" : leaf;
            }
            catch { return "workspace"; }
        }
    }

    static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics graphics, Brush brush,
            float x, float y, float width, float height, float radius)
        {
            using (GraphicsPath path = RoundedPath(x, y, width, height, radius))
                graphics.FillPath(brush, path);
        }

        public static void DrawRoundedRectangle(this Graphics graphics, Pen pen,
            float x, float y, float width, float height, float radius)
        {
            using (GraphicsPath path = RoundedPath(x, y, width, height, radius))
                graphics.DrawPath(pen, path);
        }

        static GraphicsPath RoundedPath(float x, float y, float width, float height, float radius)
        {
            float safeRadius = Math.Max(1, Math.Min(radius, Math.Min(width, height)));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(x, y, safeRadius, safeRadius, 180, 90);
            path.AddArc(x + width - safeRadius, y, safeRadius, safeRadius, 270, 90);
            path.AddArc(x + width - safeRadius, y + height - safeRadius, safeRadius, safeRadius, 0, 90);
            path.AddArc(x, y + height - safeRadius, safeRadius, safeRadius, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
