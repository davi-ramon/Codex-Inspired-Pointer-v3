using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CodexInspiredPointer
{
    public sealed class CursorTestPanel : Form
    {
        [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr id);
        [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern IntPtr LoadCursorFromFile(string file);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr instance,string path,uint type,int width,int height,uint flags);
        private readonly Label status = new Label();
        private readonly Label helper = new Label();
        private readonly FlowLayoutPanel board = new FlowLayoutPanel();
        private readonly string root = AppDomain.CurrentDomain.BaseDirectory;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

        public CursorTestPanel()
        {
            Text="Codex-inspired Pointer - painel de testes";
            StartPosition=FormStartPosition.CenterScreen;
            ClientSize=new Size(1190,850);
            MinimumSize=new Size(860,680);
            BackColor=Color.FromArgb(18,22,31);
            ForeColor=Color.White;
            Font=new Font("Segoe UI",10);
            var title=new Label { Text="Painel de testes dos ponteiros", AutoSize=true,
                Font=new Font("Segoe UI Semibold",20),Location=new Point(24,18) };
            Controls.Add(title);
            var intro=new Label { Text="Passe o mouse em cada bloco, mova rápido e clique. Arraste o mapa para testar a mão aberta e fechada.",
                AutoSize=true,Location=new Point(26,62),ForeColor=Color.FromArgb(185,204,232) };
            Controls.Add(intro);
            helper.Location=new Point(26,89); helper.Size=new Size(1115,24);
            helper.ForeColor=Color.FromArgb(119,199,255); Controls.Add(helper);
            board.Location=new Point(18,126); board.Size=new Size(1154,665);
            board.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
            board.AutoScroll=true; board.WrapContents=true;
            Controls.Add(board);
            status.Location=new Point(25,804); status.Size=new Size(1140,26);
            status.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;
            status.Text="Escolha um bloco para testar o ponteiro em tamanho real.";
            Controls.Add(status);

            Add("Seta normal","Seleção e clique",32512);
            Add("Ajuda","Ponto de interrogação",32651);
            Add("Trabalhando","Seta com carregamento",32650);
            Add("Ocupado","Carregamento",32514);
            Add("Precisão","Mira",32515);
            AddText();
            Add("Manuscrito","Caneta",32631);
            Add("Indisponível","Brilho vermelho",32648);
            Add("Vertical","Redimensionar cima/baixo",32645);
            Add("Horizontal","Redimensionar esquerda/direita",32644);
            Add("Diagonal 1","Redimensionar ↖ ↘",32642);
            Add("Diagonal 2","Redimensionar ↗ ↙",32643);
            Add("Mover","Quatro direções",32646);
            Add("Alternativa","Seta para cima",32516);
            AddLink();
            Add("Localização","Selecionar local",32671);
            Add("Pessoa","Selecionar pessoa",32672);
            AddMap();

            timer.Interval=2000;
            timer.Tick+=delegate { RefreshMotionStatus(); };
            timer.Start(); RefreshMotionStatus();
        }

        private Cursor SystemCursor(uint id)
        {
            if (id==32514 || id==32650)
            {
                string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Codex Pointer","assets_active",id==32514?"busy.ani":"working.ani");
                IntPtr animated=LoadCursorFromFile(file);
                if (animated!=IntPtr.Zero) return new Cursor(animated);
            }
            IntPtr handle=LoadCursor(IntPtr.Zero,(IntPtr)id);
            if (handle==IntPtr.Zero) return Cursors.Default;
            return new Cursor(handle);
        }

        private Panel Card(string name,string description)
        {
            var card=new Panel { Size=new Size(270,176), Margin=new Padding(6),
                BackColor=Color.FromArgb(30,37,50) };
            card.Paint+=delegate(object sender,PaintEventArgs e) {
                using(var pen=new Pen(Color.FromArgb(65,86,116)))
                    e.Graphics.DrawRectangle(pen,0,0,card.Width-1,card.Height-1);
            };
            card.Controls.Add(new Label { Text=name,AutoSize=true,Location=new Point(13,10),
                Font=new Font("Segoe UI Semibold",12),ForeColor=Color.White });
            card.Controls.Add(new Label { Text=description,AutoSize=true,Location=new Point(13,40),
                ForeColor=Color.FromArgb(170,188,210) });
            board.Controls.Add(card);
            return card;
        }

        private Panel Area(Panel card,string name,Cursor cursor)
        {
            var area=new Panel { Location=new Point(12,68),Size=new Size(245,94),
                BackColor=Color.FromArgb(16,27,43),Cursor=cursor };
            area.Paint+=delegate(object sender,PaintEventArgs e) {
                using(var pen=new Pen(Color.FromArgb(46,108,163)))
                    e.Graphics.DrawRectangle(pen,0,0,area.Width-1,area.Height-1);
                using(var brush=new SolidBrush(Color.FromArgb(114,157,202)))
                    e.Graphics.DrawString("Mova e clique aqui",Font,brush,24,34);
            };
            area.MouseEnter+=delegate { status.Text=name+" — mova o ponteiro e clique neste bloco."; };
            card.Controls.Add(area);
            return area;
        }

        private void Add(string name,string description,uint id)
        {
            var card=Card(name,description);
            Area(card,name,SystemCursor(id));
        }

        private void AddText()
        {
            var card=Card("Texto","Seleção e edição");
            var box=new TextBox { Location=new Point(12,68),Size=new Size(245,94),Multiline=true,
                Text="Clique aqui e selecione este texto.\r\nO cursor I deve ficar visível.",
                Font=new Font("Segoe UI",11),BackColor=Color.FromArgb(16,27,43),
                ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,
                Cursor=SystemCursor(32513) };
            box.MouseEnter+=delegate { status.Text="Texto — compare o tamanho com a seta."; };
            card.Controls.Add(box);
        }

        private void AddLink()
        {
            var card=Card("Link","Mão de clique");
            var link=new LinkLabel { Location=new Point(12,68),Size=new Size(245,94),
                Text="Clique e arraste neste link de teste",TextAlign=ContentAlignment.MiddleCenter,
                LinkColor=Color.FromArgb(125,184,255),BackColor=Color.FromArgb(16,27,43),
                Cursor=SystemCursor(32649) };
            link.LinkClicked+=delegate { status.Text="Link de teste clicado; nenhuma página foi aberta."; };
            link.MouseEnter+=delegate { status.Text="Link — mova rápido e clique para ver o retorno."; };
            card.Controls.Add(link);
        }

        private void AddMap()
        {
            var card=Card("Mapa para arrastar","Mão aberta / fechada");
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Codex Pointer","assets_active");
            IntPtr openHandle=LoadImage(IntPtr.Zero,Path.Combine(dir,"grab.cur"),2,128,128,0x10);
            IntPtr closedHandle=LoadImage(IntPtr.Zero,Path.Combine(dir,"grabbing.cur"),2,128,128,0x10);
            Cursor open=openHandle!=IntPtr.Zero?new Cursor(openHandle):SystemCursor(32649);
            Cursor closed=closedHandle!=IntPtr.Zero?new Cursor(closedHandle):SystemCursor(32649);
            var map=Area(card,"Mapa",open);
            map.Paint+=delegate(object sender,PaintEventArgs e) {
                using(var pen=new Pen(Color.FromArgb(74,126,100),2))
                    for(int i=0;i<map.Width;i+=30) e.Graphics.DrawLine(pen,i,0,i+65,map.Height);
                using(var brush=new SolidBrush(Color.FromArgb(227,244,234)))
                    e.Graphics.DrawString("Arraste o mapa",Font,brush,48,34);
            };
            bool dragging=false;
            map.MouseDown+=delegate { dragging=true; map.Cursor=closed; status.Text="Mapa — mão fechada durante o arraste."; };
            map.MouseUp+=delegate { dragging=false; map.Cursor=open; status.Text="Mapa — mão aberta após soltar."; };
            map.MouseLeave+=delegate { if (!dragging) map.Cursor=open; };
        }

        private void RefreshMotionStatus()
        {
            bool running=false;
            try {
                using(Mutex marker=Mutex.OpenExisting(@"Local\CodexInspiredPointerMotionV3"))
                    running=true;
            } catch (WaitHandleCannotBeOpenedException) { }
            catch (UnauthorizedAccessException) { }
            helper.Text=running?
                "Auxiliar ativo — movimento na função visível; espera animada pelo Windows.":
                "Auxiliar de movimento inativo — confira o instalador e motion.log.";
            helper.ForeColor=running?Color.FromArgb(113,220,168):Color.FromArgb(255,167,115);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CursorTestPanel());
        }
    }
}
