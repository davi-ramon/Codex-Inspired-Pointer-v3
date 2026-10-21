using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CodexInspiredPointer
{
    internal sealed class ScriptResult
    {
        public int ExitCode;
        public string Output;
    }

    public sealed class Configurator : Form
    {
        private readonly ComboBox sizeBox = new ComboBox();
        private readonly ComboBox borderBox = new ComboBox();
        private readonly CheckBox motionBox = new CheckBox();
        private readonly ComboBox glowColorBox = new ComboBox();
        private readonly ComboBox glowRadiusBox = new ComboBox();
        private readonly ComboBox glowIntensityBox = new ComboBox();
        private readonly PictureBox preview = new PictureBox();
        private readonly Label status = new Label();
        private readonly Button apply = new Button();
        private readonly Button mouseProperties = new Button();
        private readonly Button restore = new Button();
        private readonly string packageRoot = AppDomain.CurrentDomain.BaseDirectory;

        public Configurator()
        {
            Text = "Codex-inspired Pointer - configuracao";
            ClientSize = new Size(970, 730);
            MinimumSize = new Size(850, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(19, 23, 33);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            Icon = SystemIcons.Application;

            var title = new Label();
            title.Text = "Codex-inspired Pointer";
            title.Font = new Font("Segoe UI Semibold", 19);
            title.AutoSize = true;
            title.Location = new Point(22, 16);
            Controls.Add(title);

            var subtitle = new Label();
            subtitle.Text = "Cada tamanho e redesenhado para preservar contorno e nitidez.";
            subtitle.AutoSize = true;
            subtitle.ForeColor = Color.FromArgb(185, 199, 230);
            subtitle.Location = new Point(25, 55);
            Controls.Add(subtitle);

            AddLabel("Tamanho", 25, 91);
            sizeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            sizeBox.Items.AddRange(new object[] {
                "Mini - 70%", "Pequeno - 75%", "Equilibrado - 85%",
                "Medio - 95%", "Grande - 110%", "Extra - 125%", "Maximo - 150%"
            });
            sizeBox.Location = new Point(113, 88);
            sizeBox.Width = 180;
            sizeBox.SelectedIndex = 2;
            sizeBox.SelectedIndexChanged += delegate { RefreshPreview(); };
            sizeBox.TextChanged += delegate { RefreshPreview(); };
            Controls.Add(sizeBox);

            AddLabel("Borda branca", 318, 91);
            borderBox.DropDownStyle = ComboBoxStyle.DropDownList;
            borderBox.Items.AddRange(new object[] {
                "1,3 px", "1,5 px", "1,7 px"
            });
            borderBox.Location = new Point(440, 88);
            borderBox.Width = 125;
            borderBox.SelectedIndex = 1;
            borderBox.SelectedIndexChanged += delegate { RefreshPreview(); };
            borderBox.TextChanged += delegate { RefreshPreview(); };
            Controls.Add(borderBox);

            motionBox.Text = "Movimento e clique ao iniciar o Windows";
            motionBox.AutoSize = true;
            motionBox.Checked = true;
            motionBox.Location = new Point(590, 90);
            Controls.Add(motionBox);

            AddLabel("Cor do brilho", 25, 130);
            glowColorBox.DropDownStyle = ComboBoxStyle.DropDownList;
            glowColorBox.Items.AddRange(new object[] { "Azul", "Ciano", "Roxo", "Branco" });
            glowColorBox.SelectedIndex = 0;
            glowColorBox.Location = new Point(135, 127);
            glowColorBox.Width = 125;
            Controls.Add(glowColorBox);

            AddLabel("Raio", 282, 130);
            glowRadiusBox.DropDownStyle = ComboBoxStyle.DropDownList;
            glowRadiusBox.Items.AddRange(new object[] { "75%", "100%", "120%", "140%" });
            glowRadiusBox.SelectedIndex = 2;
            glowRadiusBox.Location = new Point(330, 127);
            glowRadiusBox.Width = 105;
            Controls.Add(glowRadiusBox);

            AddLabel("Intensidade", 464, 130);
            glowIntensityBox.DropDownStyle = ComboBoxStyle.DropDownList;
            glowIntensityBox.Items.AddRange(new object[] { "60%", "100%", "130%", "180%" });
            glowIntensityBox.SelectedIndex = 2;
            glowIntensityBox.Location = new Point(565, 127);
            glowIntensityBox.Width = 105;
            Controls.Add(glowIntensityBox);
            var glowNote = new Label();
            glowNote.Text = "Brilho aplicado aos ponteiros";
            glowNote.AutoSize = true;
            glowNote.ForeColor = Color.FromArgb(177, 194, 229);
            glowNote.Location = new Point(694, 130);
            Controls.Add(glowNote);

            var previewPanel = new Panel();
            previewPanel.Location = new Point(20, 170);
            previewPanel.Size = new Size(930, 475);
            previewPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                                  AnchorStyles.Left | AnchorStyles.Right;
            previewPanel.BackColor = Color.FromArgb(21, 25, 35);
            Controls.Add(previewPanel);
            preview.Dock = DockStyle.Fill;
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            previewPanel.Controls.Add(preview);

            status.Text = "Os ajustes sao salvos para esta conta do Windows.";
            status.ForeColor = Color.FromArgb(177, 194, 229);
            status.Location = new Point(22, 656);
            status.Size = new Size(920, 23);
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(status);

            apply.Text = "Aplicar ao Windows";
            apply.Location = new Point(22, 685);
            apply.Size = new Size(180, 32);
            apply.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            apply.Click += delegate { StartWork("install"); };
            Controls.Add(apply);

            mouseProperties.Text = "Propriedades do mouse";
            mouseProperties.Location = new Point(218, 685);
            mouseProperties.Size = new Size(190, 32);
            mouseProperties.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            mouseProperties.Click += delegate {
                try { Process.Start("control.exe", "main.cpl,,1"); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Nao foi possivel abrir"); }
            };
            Controls.Add(mouseProperties);

            restore.Text = "Restaurar anteriores";
            restore.Location = new Point(424, 685);
            restore.Size = new Size(175, 32);
            restore.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            restore.Click += delegate {
                if (MessageBox.Show(this, "Restaurar os ponteiros que estavam instalados antes deste pacote?",
                    "Confirmar restauracao", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    StartWork("uninstall");
            };
            Controls.Add(restore);

            var testPanel = new Button();
            testPanel.Text = "Testar ponteiros";
            testPanel.Location = new Point(615, 685);
            testPanel.Size = new Size(185, 32);
            testPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            testPanel.Click += delegate {
                string file = Path.Combine(packageRoot, "CursorTestPanel.exe");
                try {
                    if (!File.Exists(file)) throw new FileNotFoundException("Painel de testes nao encontrado", file);
                    Process.Start(file);
                } catch (Exception ex) {
                    MessageBox.Show(this, ex.Message, "Nao foi possivel abrir o painel");
                }
            };
            Controls.Add(testPanel);

            var close = new Button();
            close.Text = "Fechar";
            close.Location = new Point(818, 685);
            close.Size = new Size(130, 32);
            close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            close.Click += delegate { Close(); };
            Controls.Add(close);

            LoadSettings();
            RefreshPreview();
        }

        private void AddLabel(string text, int x, int y)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            Controls.Add(label);
        }

        private string SelectedPreset()
        {
            int[] sizes = {70, 75, 85, 95, 110, 125, 150};
            int[] borders = {130, 150, 170};
            return string.Format("s{0}-b{1}", sizes[Math.Max(0, sizeBox.SelectedIndex)],
                                 borders[Math.Max(0, borderBox.SelectedIndex)]);
        }

        private void LoadSettings()
        {
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       "Codex Pointer", "settings.ini");
            if (!File.Exists(file)) return;
            foreach (string line in File.ReadAllLines(file))
            {
                if (line.StartsWith("Preset=", StringComparison.Ordinal))
                {
                    string[] parts = line.Substring(7).Split('-');
                    if (parts.Length == 2)
                    {
                        int[] sizes = {70,75,85,95,110,125,150}; int[] borders = {130,150,170};
                        int s, b;
                        if (int.TryParse(parts[0].Substring(1), out s))
                            for (int i=0;i<sizes.Length;i++) if (sizes[i]==s) sizeBox.SelectedIndex=i;
                        if (int.TryParse(parts[1].Substring(1), out b))
                            for (int i=0;i<borders.Length;i++) if (borders[i]==b) borderBox.SelectedIndex=i;
                    }
                }
                if (line == "Motion=0") motionBox.Checked = false;
                if (line.StartsWith("GlowColor=", StringComparison.Ordinal))
                {
                    string[] colors = {"Blue","Cyan","Purple","White"};
                    for (int i=0;i<colors.Length;i++)
                        if (line.Substring(10)==colors[i]) glowColorBox.SelectedIndex=i;
                }
                if (line.StartsWith("GlowRadius=", StringComparison.Ordinal))
                {
                    int[] values={75,100,120,140}; int value;
                    if (int.TryParse(line.Substring(11),out value))
                        for (int i=0;i<values.Length;i++) if (values[i]==value) glowRadiusBox.SelectedIndex=i;
                }
                if (line.StartsWith("GlowIntensity=", StringComparison.Ordinal))
                {
                    int[] values={60,100,130,180}; int value;
                    if (int.TryParse(line.Substring(14),out value))
                        for (int i=0;i<values.Length;i++) if (values[i]==value) glowIntensityBox.SelectedIndex=i;
                }
            }
        }

        private void RefreshPreview()
        {
            if (sizeBox.SelectedIndex < 0 || borderBox.SelectedIndex < 0) return;
            string path = Path.Combine(packageRoot, "presets", SelectedPreset(), "preview.png");
            Image old = preview.Image;
            preview.Image = null;
            if (old != null) old.Dispose();
            if (!File.Exists(path)) { status.Text = "Previa nao encontrada. Mantenha a pasta completa."; return; }
            using (var stream = new MemoryStream(File.ReadAllBytes(path)))
            using (var image = Image.FromStream(stream))
                preview.Image = new Bitmap(image);
        }

        private void StartWork(string action)
        {
            RefreshPreview();
            apply.Enabled = false;
            restore.Enabled = false;
            status.Text = action == "install" ? "Aplicando ponteiros..." : "Restaurando ponteiros...";
            string preset = SelectedPreset();
            bool motion = motionBox.Checked;
            string[] colors = {"Blue","Cyan","Purple","White"};
            int[] radii = {75,100,120,140};
            int[] intensities = {60,100,130,180};
            string glowColor=colors[Math.Max(0,glowColorBox.SelectedIndex)];
            int glowRadius=radii[Math.Max(0,glowRadiusBox.SelectedIndex)];
            int glowIntensity=intensities[Math.Max(0,glowIntensityBox.SelectedIndex)];
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
                e.Result = RunScript(action, preset, motion, glowColor, glowRadius, glowIntensity);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                apply.Enabled = true; restore.Enabled = true;
                if (e.Error != null) {
                    status.Text = "Falha: " + e.Error.Message;
                    MessageBox.Show(this, e.Error.Message, "Falha", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } else {
                    var result = (ScriptResult)e.Result;
                    status.Text = result.ExitCode == 0 ?
                        (action == "install" ? "Esquema aplicado e salvo no Windows." : "Ponteiros anteriores restaurados.") :
                        "Falha ao alterar os ponteiros.";
                    MessageBox.Show(this, result.Output, result.ExitCode == 0 ? "Concluido" : "Falha",
                                    MessageBoxButtons.OK,
                                    result.ExitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }
                worker.Dispose();
            };
            worker.RunWorkerAsync();
        }

        private ScriptResult RunScript(string action, string preset, bool motion,
                                       string glowColor, int glowRadius, int glowIntensity)
        {
            string script = Path.Combine(packageRoot, action == "install" ? "InstallPackage.ps1" : "UninstallPackage.ps1");
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                                             "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(script)) throw new FileNotFoundException("Script do pacote nao encontrado", script);
            var psi = new ProcessStartInfo(powershell);
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"" +
                            (action == "install" ? " -Preset " + preset +
                             " -EnableMotion " + (motion ? "1" : "0") +
                             " -GlowColor " + glowColor + " -GlowRadius " + glowRadius +
                             " -GlowIntensity " + glowIntensity : "");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ScriptResult { ExitCode = process.ExitCode,
                                          Output = (output + Environment.NewLine + error).Trim() };
            }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Configurator());
        }
    }
}
