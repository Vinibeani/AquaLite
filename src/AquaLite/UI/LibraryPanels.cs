using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AquaLite {
    public sealed class SensorPanel : UserControl {
        public event EventHandler ActivateSensor;
        readonly TextBox search=new TextBox();
        readonly ComboBox category=new ComboBox();
        readonly DataGridView grid=new DataGridView();
        readonly Label information=new Label();
        readonly Label[] summary=new Label[3];
        Reading reading;
        string rowSignature="";
        public SensorPanel(){
            BackColor=Palette.Background;ForeColor=Palette.Text;
            TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,102));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,94));Controls.Add(layout);
            TableLayoutPanel cards=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,Margin=Padding.Empty};
            for(int i=0;i<3;i++){cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));Card card=new Card{Dock=DockStyle.Fill,Margin=new Padding(i==0?0:7,0,i==2?0:7,5)};summary[i]=new Label{Dock=DockStyle.Fill,Padding=new Padding(16,12,8,6),Font=Palette.Font(12,true),ForeColor=Palette.Text,Text="Aguardando leitura…"};card.Controls.Add(summary[i]);cards.Controls.Add(card,i,0);}layout.Controls.Add(cards,0,0);
            Panel filters=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};layout.Controls.Add(filters,0,1);
            filters.Controls.Add(new Label{Text="Buscar sensor",Location=new Point(0,7),Size=new Size(300,21),ForeColor=Palette.Muted,Font=Palette.Font(9)});
            search.SetBounds(0,32,420,30);search.BackColor=Palette.Card;search.ForeColor=Palette.Text;search.BorderStyle=BorderStyle.FixedSingle;search.Font=Palette.Font(10);filters.Controls.Add(search);
            category.DropDownStyle=ComboBoxStyle.DropDownList;category.BackColor=Palette.Card;category.ForeColor=Palette.Text;category.FlatStyle=FlatStyle.Flat;category.Items.AddRange(new string[]{"Todos os componentes","CPU","GPU","Memória","Armazenamento","Placa-mãe","Rede","Sistema"});category.SelectedIndex=0;category.SetBounds(440,32,200,30);filters.Controls.Add(category);
            filters.Resize+=delegate{category.SetBounds(Math.Max(200,filters.Width-226),32,226,30);search.Width=Math.Max(170,filters.Width-244);};search.TextChanged+=delegate{RefreshRows();};category.SelectedIndexChanged+=delegate{RefreshRows();};
            grid.Dock=DockStyle.Fill;grid.BackgroundColor=Palette.Card;grid.BorderStyle=BorderStyle.None;grid.GridColor=Palette.Border;grid.ForeColor=Palette.Text;grid.Font=Palette.Font(9);grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AllowUserToResizeRows=false;grid.ReadOnly=true;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.EnableHeadersVisualStyles=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.ColumnHeadersHeight=36;grid.RowTemplate.Height=34;
            grid.DefaultCellStyle.BackColor=Palette.Card;grid.DefaultCellStyle.ForeColor=Palette.Text;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(30,70,74);grid.DefaultCellStyle.SelectionForeColor=Palette.Text;grid.DefaultCellStyle.Padding=new Padding(8,0,0,0);grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(17,25,38);
            grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(28,40,56);grid.ColumnHeadersDefaultCellStyle.ForeColor=Palette.Muted;grid.ColumnHeadersDefaultCellStyle.Font=Palette.Font(9,true);grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(8,0,0,0);
            string[] names={"Componente","Sensor","Leitura","Origem"};int[] weights={20,37,18,25};for(int i=0;i<names.Length;i++){int c=grid.Columns.Add("c"+i,names[i]);grid.Columns[c].FillWeight=weights[i];grid.Columns[c].SortMode=DataGridViewColumnSortMode.NotSortable;}layout.Controls.Add(grid,0,2);
            Panel footer=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,12,0,0)};layout.Controls.Add(footer,0,3);
            information.ForeColor=Palette.Muted;information.Font=Palette.Font(9);information.Location=new Point(0,0);information.Size=new Size(600,76);information.Text="As leituras disponíveis aparecem aqui. Sensores ausentes são indicados como indisponíveis.";footer.Controls.Add(information);
            FlatButton activate=new FlatButton("Habilitar sensores"){Width=190,Height=38,Anchor=AnchorStyles.Top|AnchorStyles.Right};activate.Click+=delegate{if(ActivateSensor!=null)ActivateSensor(this,EventArgs.Empty);};footer.Controls.Add(activate);footer.Resize+=delegate{activate.Location=new Point(footer.Width-190,4);information.Width=Math.Max(180,footer.Width-212);};
        }
        static string Value(double? value,string unit){if(!value.HasValue||double.IsNaN(value.Value)||double.IsInfinity(value.Value))return "Indisponível";return value.Value.ToString(unit=="GB"||unit=="V"?"0.00":"0.0")+(string.IsNullOrEmpty(unit)?"":" "+unit);}
        public void UpdateReading(Reading current){reading=current;summary[0].Text="PROCESSADOR\n"+Value(current.CpuTemperature,"°C")+"   ·   "+current.Cpu.ToString("0")+"%";summary[1].Text="PLACA DE VÍDEO\n"+Value(current.GpuTemperature,"°C")+"   ·   "+Value(current.GpuLoad,"%");summary[2].Text="MEMÓRIA RAM\n"+current.MemoryUsedGB.ToString("0.0")+" / "+current.MemoryTotalGB.ToString("0.0")+" GB   ·   "+current.MemoryPercent.ToString("0")+"%";RefreshRows();}
        void RefreshRows(){if(reading==null)return;string term=search.Text.Trim();string group=category.SelectedIndex>0?category.Text:null;
            List<SensorReading> sensors=(reading.Sensors??new List<SensorReading>()).Where(s=>(group==null||string.Equals(s.Group,group,StringComparison.OrdinalIgnoreCase))&&(term.Length==0||(s.Name+" "+s.Group+" "+s.Source+" "+s.Hardware).IndexOf(term,StringComparison.CurrentCultureIgnoreCase)>=0)).ToList();
            string signature=string.Join("|",sensors.Select(s=>s.Id).ToArray());if(signature!=rowSignature||grid.Rows.Count!=sensors.Count){rowSignature=signature;grid.Rows.Clear();foreach(SensorReading s in sensors)grid.Rows.Add(s.Group,s.Name,Value(s.Value,s.Unit),s.Source);}
            else for(int i=0;i<sensors.Count;i++)grid.Rows[i].Cells[2].Value=Value(sensors[i].Value,sensors[i].Unit);
            for(int i=0;i<sensors.Count;i++){grid.Rows[i].Cells[2].Style.ForeColor=sensors[i].Value.HasValue?Palette.Accent:Palette.Muted;grid.Rows[i].Cells[1].ToolTipText=sensors[i].Hardware;grid.Rows[i].Cells[3].ToolTipText=sensors[i].Source;}
            information.Text=sensors.Count+" sensor(es) nesta seleção. Atualização automática.\n"+(reading.SensorStatus??reading.TemperatureSource)+"\nO AquaLite não altera a bomba nem as ventoinhas.";
        }
    }

    public sealed class ThemeBrowser : UserControl {
        public event EventHandler Applied,CreateNew;
        public string SelectedTheme {get;private set;}
        readonly FlowLayoutPanel grid=new FlowLayoutPanel();
        readonly TextBox search=new TextBox();
        readonly Label count=new Label(),selection=new Label();
        readonly List<Panel> tiles=new List<Panel>();
        readonly List<Image> pictures=new List<Image>();
        public ThemeBrowser(string selected){SelectedTheme=selected;BackColor=Palette.Background;
            TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Margin=Padding.Empty};root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,76));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(root);
            Panel top=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};root.Controls.Add(top,0,0);
            search.Location=new Point(0,5);search.Size=new Size(370,30);search.BackColor=Palette.Card;search.ForeColor=Palette.Text;search.BorderStyle=BorderStyle.FixedSingle;search.Font=Palette.Font(11);top.Controls.Add(search);
            count.Location=new Point(0,43);count.Size=new Size(500,23);count.ForeColor=Palette.Muted;count.Font=Palette.Font(9);top.Controls.Add(count);
            FlatButton create=new FlatButton("Criar meu tema"){Width=174,Height=36};create.Click+=delegate{if(CreateNew!=null)CreateNew(this,EventArgs.Empty);};top.Controls.Add(create);top.Resize+=delegate{create.Location=new Point(top.Width-174,3);search.Width=Math.Max(210,top.Width-198);};
            grid.Dock=DockStyle.Fill;grid.AutoScroll=true;grid.BackColor=Palette.Background;grid.Padding=new Padding(0,0,8,0);grid.Margin=Padding.Empty;root.Controls.Add(grid,0,1);
            Panel footer=new Card{Dock=DockStyle.Fill,Margin=new Padding(0,10,0,0)};root.Controls.Add(footer,0,2);selection.Location=new Point(15,11);selection.Size=new Size(520,45);selection.Font=Palette.Font(10,true);selection.ForeColor=Palette.Text;footer.Controls.Add(selection);
            FlatButton apply=new FlatButton("Aplicar na minha tela",true){Width=203,Height=41};apply.Click+=delegate{if(!string.IsNullOrEmpty(SelectedTheme)&&Applied!=null)Applied(this,EventArgs.Empty);};footer.Controls.Add(apply);footer.Resize+=delegate{apply.Location=new Point(footer.Width-218,12);selection.Width=Math.Max(180,footer.Width-245);};
            if(Directory.Exists(LibraryTheme.Root))foreach(string dir in Directory.GetDirectories(LibraryTheme.Root)){string id=Path.GetFileName(dir);if(!File.Exists(Path.Combine(dir,"theme.json")))continue;try{LibraryTheme theme=LibraryTheme.Load(id);string name=string.IsNullOrWhiteSpace(theme.Name)?"Tema "+id:theme.Name;Panel tile=new Card{Width=180,Height=224,Margin=new Padding(0,0,12,12),Tag=id,Cursor=Cursors.Hand};PictureBox picture=new PictureBox{Location=new Point(9,9),Size=new Size(162,162),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Black};string path=Path.Combine(dir,"preview.png");if(File.Exists(path))using(Image original=Image.FromFile(path)){picture.Image=new Bitmap(original);pictures.Add(picture.Image);}tile.Controls.Add(picture);Label label=new Label{Text=name,Location=new Point(10,179),Size=new Size(160,36),Font=Palette.Font(9,true),ForeColor=Palette.Text,Tag=name};tile.Controls.Add(label);EventHandler choose=delegate{SelectedTheme=id;SelectTile(name);};tile.Click+=choose;picture.Click+=choose;label.Click+=choose;tile.DoubleClick+=delegate{choose(tile,EventArgs.Empty);if(Applied!=null)Applied(this,EventArgs.Empty);};tiles.Add(tile);grid.Controls.Add(tile);}catch(Exception ex){System.Diagnostics.Debug.WriteLine(ex.Message);}}
            search.TextChanged+=delegate{int total=0;foreach(Panel tile in tiles){string name=tile.Controls[1].Text;bool visible=(name+" "+tile.Tag).IndexOf(search.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0;tile.Visible=visible;if(visible)total++;}count.Text=total+" tema(s) • busque pelo nome acima";};
            count.Text=tiles.Count+" temas na biblioteca • busque pelo nome acima";Panel chosen=tiles.Find(p=>(string)p.Tag==selected);SelectTile(chosen==null?null:chosen.Controls[1].Text);
        }
        void SelectTile(string name){foreach(Panel tile in tiles){bool active=(string)tile.Tag==SelectedTheme;tile.BackColor=active?Color.FromArgb(29,68,70):Palette.Card;tile.Controls[1].ForeColor=active?Palette.Accent:Palette.Text;}selection.Text=string.IsNullOrEmpty(name)?"Selecione uma prévia para aplicar.\nO tema atual continua na tela.":name+"\nPronto para aplicar na sua tela.";}
        protected override void Dispose(bool disposing){if(disposing){foreach(Image picture in pictures)picture.Dispose();pictures.Clear();}base.Dispose(disposing);}
    }
}
