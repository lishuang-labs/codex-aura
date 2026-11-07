using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DragonCodexBoot {
 // Independent presentation settings; original launch/handoff settings remain in Config.
 public sealed class PreviewConfig {
  public PreviewConfig() {
   WelcomeText="欢迎回来，创造者。";DurationSeconds=16;
   CharacterPath="assets/character-placeholder.png";CharacterIsPlaceholder=true;AudioPath="";
  }
  public string WelcomeText { get; set; }
  public double DurationSeconds { get; set; }
  public string CharacterPath { get; set; }
  public bool CharacterIsPlaceholder { get; set; }
  public string AudioPath { get; set; }
 }

 // Pure timeline, normalized to the 16-second storyboard, independent of frame rate.
 public static class PreviewTimeline {
  public static double Fade(double t,double start,double length) { return Geometry.Ease((t-start)/length); }
  public static double StoryTime(double seconds,double duration) { return Math.Max(0,Math.Min(16,seconds*16/duration)); }
  public static double Intro(double t) { return Fade(t,.1,.65)*(1-Fade(t,1.5,.65)); }
  public static double Hud(double t) { return Fade(t,1.6,1.5)*(1-Fade(t,7,1.35)); }
  public static double Character(double t) { return Fade(t,9.8,1.65); }
  public static double Title(double t) { return Fade(t,11.4,1.25); }
  public static double Scene(double t) { return 1-Fade(t,14.6,1.4); }
 }

 public static class PreviewEntry {
  public static string LocalPath(string root,string value) {
   if(String.IsNullOrWhiteSpace(value))return null;
   // Media is local only; never resolve URLs, UNC shares or device paths.
   if(value.IndexOf("://",StringComparison.Ordinal)>=0||value.StartsWith(@"\\")||value.StartsWith("//"))
    throw new ArgumentException("预览素材仅支持本地文件，不支持 URL 或网络共享。");
   return Path.GetFullPath(Path.Combine(root,value));
  }
  public static void Validate(Config c) {
   if(c==null||c.Preview==null)throw new ArgumentException("缺少 Preview 配置。");
   if(c.AutoReplaceEntrypoints||c.ScanAllLocalDrives)throw new ArgumentException("必须关闭 AutoReplaceEntrypoints 和 ScanAllLocalDrives。");
   double duration=c.Preview.DurationSeconds;
   if(Double.IsNaN(duration)||Double.IsInfinity(duration)||duration<8||duration>120)
    throw new ArgumentException("DurationSeconds 必须在 8 至 120 秒之间。");
   if(Double.IsNaN(c.Volume)||Double.IsInfinity(c.Volume)||c.Volume<0||c.Volume>1)
    throw new ArgumentException("Volume 必须在 0 至 1 之间。");
   if(c.Preview.WelcomeText==null||c.Preview.WelcomeText.Length>120)
    throw new ArgumentException("欢迎文字最多 120 个字符，可设为空字符串。");
   LocalPath(AppDomain.CurrentDomain.BaseDirectory,c.Preview.CharacterPath);
   LocalPath(AppDomain.CurrentDomain.BaseDirectory,c.Preview.AudioPath);
  }
  public static int Run(string[] args,string root) {
   bool headless=args.Contains("--preview-frames")||args.Contains("--self-test");
   try {
    // Only these exact modes are accepted. In particular, combining preview with integration is rejected.
    if(args.Length>1||(args.Length==1&&args[0]!="--preview"&&args[0]!="--preview-frames"&&args[0]!="--self-test"))
     throw new ArgumentException("预览仅支持 --preview、--preview-frames、--self-test；启动客户端请单独使用 --launch。");
    Config c=new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(Path.Combine(root,"launcher.json")));
    Validate(c);
    if(args.Contains("--self-test"))return Entry.RunSelfTest(root);
    if(args.Contains("--preview-frames")) { ExportFrames(c,root);return 0; }
    Application app=new Application();
    app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e) {
     e.Handled=true;MessageBox.Show(e.Exception.Message,"独立动画预览");app.Shutdown(1);
    };
    return app.Run(new PreviewWindow(c,root));
   } catch(Exception e) {
    File.WriteAllText(Path.Combine(root,"preview-error.txt"),e.ToString());
    if(!headless)MessageBox.Show(e.Message,"独立动画预览");
    return 1;
   }
  }
  private static void ExportFrames(Config c,string root) {
   string folder=Path.Combine(root,"qa","preview");Directory.CreateDirectory(folder);
   PreviewScene scene=new PreviewScene(c,root);
   scene.Width=1600;scene.Height=900;scene.Measure(new Size(1600,900));scene.Arrange(new Rect(0,0,1600,900));
   foreach(double t in new double[] {1,3,6,8.6,10,12.8,15.2,16}) {
    scene.Time=t;scene.InvalidateVisual();scene.UpdateLayout();
    RenderTargetBitmap bitmap=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32);bitmap.Render(scene);
    PngBitmapEncoder encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using(Stream output=File.Create(Path.Combine(folder,"frame-"+t.ToString("00.0",CultureInfo.InvariantCulture)+".png")))encoder.Save(output);
   }
   File.WriteAllText(Path.Combine(folder,"frames.json"),new JavaScriptSerializer().Serialize(new {
    storyboardSeconds=new double[]{1,3,6,8.6,10,12.8,15.2,16},durationSeconds=c.Preview.DurationSeconds,
    renderer="Same WPF PreviewScene as interactive preview",characterStatus=scene.AssetStatus,
    scope="Offscreen storyboard rendering only; no client launch, discovery, shortcuts or DWM calls."
   }));
  }
 }

 // This window does not inherit BootWindow and never touches its native-window code.
 public sealed class PreviewWindow:Window {
  private readonly PreviewScene scene;
  private readonly Config cfg;
  private readonly Stopwatch clock=new Stopwatch();
  private readonly DispatcherTimer timer;
  private readonly TextBlock controls;
  private readonly MediaPlayer audio=new MediaPlayer();
  private double offset;
  private bool paused,audioReady;
  public PreviewWindow(Config config,string root) {
   cfg=config;Title="Codex · Sci-fi Preview / 独立动画预览";
   InputMethod.SetIsInputMethodEnabled(this,false);
   Background=Brushes.Black;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   ResizeMode=ResizeMode.CanResize;MinWidth=640;MinHeight=420;
   Width=Math.Min(1280,SystemParameters.WorkArea.Width*.9);Height=Math.Min(770,SystemParameters.WorkArea.Height*.9);
   Grid layout=new Grid();layout.RowDefinitions.Add(new RowDefinition());
   layout.RowDefinitions.Add(new RowDefinition { Height=new GridLength(38) });
   scene=new PreviewScene(config,root) { Width=1600,Height=900 };
   Viewbox fit=new Viewbox { Stretch=Stretch.Uniform,Child=scene };layout.Children.Add(fit);
   controls=new TextBlock { Foreground=new SolidColorBrush(Color.FromRgb(181,173,194)),FontSize=12,
    VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(18,0,12,0) };
   Border footer=new Border { Background=new SolidColorBrush(Color.FromRgb(18,16,23)),Child=controls };
   Grid.SetRow(footer,1);layout.Children.Add(footer);Content=layout;
   timer=new DispatcherTimer(DispatcherPriority.Render) { Interval=TimeSpan.FromMilliseconds(1000.0/60) };
   timer.Tick+=delegate { UpdateFrame(); };
   PreviewKeyDown+=OnKey;
   Loaded+=delegate {
    string audioPath=PreviewEntry.LocalPath(root,cfg.Preview.AudioPath);
    if(audioPath!=null&&File.Exists(audioPath)) {
     audio.Volume=cfg.Volume;
     audio.MediaOpened+=delegate { audioReady=true;audio.Position=TimeSpan.FromSeconds(CurrentSeconds);if(!paused)audio.Play(); };
     audio.MediaFailed+=delegate { audioReady=false;scene.AudioStatus="音频不可用 · 静音继续"; };
     audio.Open(new Uri(audioPath));
    } else if(audioPath!=null)scene.AudioStatus="音频路径不存在 · 静音继续";
    clock.Start();timer.Start();UpdateFrame();
   };
   Closed+=delegate { timer.Stop();clock.Stop();audio.Close(); };
  }
  private double CurrentSeconds { get { return offset+clock.Elapsed.TotalSeconds; } }
  private void UpdateFrame() {
   double seconds=CurrentSeconds;
   if(seconds>=cfg.Preview.DurationSeconds) { Close();return; }
   scene.Time=PreviewTimeline.StoryTime(seconds,cfg.Preview.DurationSeconds);scene.InvalidateVisual();
   controls.Text=String.Format(CultureInfo.InvariantCulture,"独立预览  ·  {0:00.0} / {1:00.0}s    {2}    Space 暂停 / 继续    R 重播    1–5 分镜    Esc 退出",seconds,cfg.Preview.DurationSeconds,paused?"已暂停":"播放中");
  }
  private void Seek(double storyTime) {
   offset=storyTime/16*cfg.Preview.DurationSeconds;clock.Reset();if(!paused)clock.Start();
   if(audioReady)audio.Position=TimeSpan.FromSeconds(offset);UpdateFrame();
  }
  private void OnKey(object sender,KeyEventArgs e) {
   if(e.Key==Key.Escape) { e.Handled=true;Close();return; }
   if(e.Key==Key.Space) {
    paused=!paused;
    if(paused) { clock.Stop();audio.Pause(); } else { clock.Start();if(audioReady)audio.Play(); }
    e.Handled=true;UpdateFrame();
   } else if(e.Key==Key.R) { Seek(0);e.Handled=true; }
   else if(e.Key>=Key.D1&&e.Key<=Key.D5) { Seek(new double[]{1,6,9,12.8,15.2}[(int)e.Key-(int)Key.D1]);e.Handled=true; }
  }
 }

 // Decode once using the Windows GIF compositor, including frame disposal and transparency.
 // The preview clock selects frames, so pause, seek and replay require no separate timer.
 public sealed class CharacterAnimation {
  private readonly List<BitmapSource> frames=new List<BitmapSource>();
  private readonly List<double> ends=new List<double>();
  public int FrameCount { get { return frames.Count; } }
  public double DurationSeconds { get; private set; }
  private static System.Drawing.Image OpenGif(string path) {
   // GDI+ also reports invalid image data as OutOfMemoryException. Limit this translation
   // to the file decoder; allocation failures in frame caching must still propagate.
   try { return System.Drawing.Image.FromFile(path); }
   catch(OutOfMemoryException e) { throw new InvalidDataException("GIF 无法解码或解码器内存不足。",e); }
  }
  public CharacterAnimation(string path) {
   using(System.Drawing.Image source=OpenGif(path)) {
    System.Drawing.Imaging.FrameDimension dimension=System.Drawing.Imaging.FrameDimension.Time;
    int count=source.GetFrameCount(dimension),w=source.Width,h=source.Height;
    // Bound decoded memory as well as file size; reject oversized GIFs into the existing fallback.
    if(w<1||h<1||w>4096||h>4096||count>1000||(long)w*h*4*count>128L*1024*1024)
     throw new NotSupportedException("GIF 解码内存超出 128 MB 限制。");
    byte[] delays=null;
    if(source.PropertyIdList.Contains(0x5100))delays=source.GetPropertyItem(0x5100).Value;
    using(System.Drawing.Bitmap canvas=new System.Drawing.Bitmap(w,h,System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
     for(int i=0;i<count;i++) {
      source.SelectActiveFrame(dimension,i);
      using(System.Drawing.Graphics graphics=System.Drawing.Graphics.FromImage(canvas)) {
       graphics.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
       graphics.Clear(System.Drawing.Color.Transparent);graphics.DrawImageUnscaled(source,0,0);
      }
      System.Drawing.Imaging.BitmapData data=canvas.LockBits(new System.Drawing.Rectangle(0,0,w,h),
       System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
      try {
       int stride=w*4;byte[] pixels=new byte[stride*h];
       for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*stride,stride);
       BitmapSource frame=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,pixels,stride);frame.Freeze();frames.Add(frame);
      } finally { canvas.UnlockBits(data); }
      int delay=delays!=null&&delays.Length>=(i+1)*4?BitConverter.ToInt32(delays,i*4):10;
      DurationSeconds+=delay<=0?.1:Math.Max(.02,delay/100.0);ends.Add(DurationSeconds);
     }
    }
   }
  }
  public BitmapSource FrameAt(double seconds) {
   double t=Math.Max(0,seconds)%DurationSeconds;
   for(int i=0;i<ends.Count;i++)if(t<ends[i])return frames[i];
   return frames[frames.Count-1];
  }
 }

 // A single retained WPF drawing surface: procedural HUD and particles, independent image layer.
 public sealed class PreviewScene:FrameworkElement {
  private readonly Config cfg;
  private readonly Dictionary<string,SolidColorBrush> brushes=new Dictionary<string,SolidColorBrush>();
  private readonly Dictionary<string,FormattedText> texts=new Dictionary<string,FormattedText>();
  private readonly List<Particle> particles=new List<Particle>();
  private BitmapSource character;
  private CharacterAnimation characterAnimation;
  private bool featherCharacter;
  private Rect characterRect;
  private readonly Brush atmosphere,halo;
  public double Time { get; set; }
  public bool IsLaunch { get; set; }
  public string AssetStatus { get; private set; }
  public string AudioStatus { get; set; }
  private struct Particle { public double X,Y,SX,SY,Size,Seed; }
  public PreviewScene(Config config,string root) {
   cfg=config;ClipToBounds=true;AudioStatus="";
   atmosphere=Radial("#FF352B3B","#0015111C",new Point(.54,.43),.8,.85);
   halo=Radial("#584F3A64","#00000000",new Point(.72,.48),.6,.8);
   LoadCharacter(root);BuildParticles();
  }
  private static Brush Radial(string inner,string outer,Point center,double rx,double ry) {
   RadialGradientBrush b=new RadialGradientBrush((Color)ColorConverter.ConvertFromString(inner),(Color)ColorConverter.ConvertFromString(outer));
   b.Center=center;b.GradientOrigin=center;b.RadiusX=rx;b.RadiusY=ry;b.Freeze();return b;
  }
  private SolidColorBrush Ink(string color) {
   SolidColorBrush b;if(!brushes.TryGetValue(color,out b)) { b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));b.Freeze();brushes[color]=b; }return b;
  }
  private void LoadCharacter(string root) {
   string path=PreviewEntry.LocalPath(root,cfg.Preview.CharacterPath);
   try {
    if(path==null||!File.Exists(path))throw new FileNotFoundException();
    string extension=Path.GetExtension(path).ToLowerInvariant();
    if(extension!=".png"&&extension!=".jpg"&&extension!=".jpeg"&&extension!=".gif")throw new NotSupportedException();
    if(new FileInfo(path).Length>40*1024*1024)throw new InvalidDataException();
    featherCharacter=extension==".gif"||extension==".jpg"||extension==".jpeg";
    if(extension==".gif") { characterAnimation=new CharacterAnimation(path);character=characterAnimation.FrameAt(0); }
    else {
     BitmapImage image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
     image.DecodePixelWidth=1200;image.UriSource=new Uri(path);image.EndInit();image.Freeze();character=image;
    }
    bool placeholder=cfg.Preview.CharacterIsPlaceholder||Path.GetFileName(path).Equals("character-placeholder.png",StringComparison.OrdinalIgnoreCase);
    AssetStatus=placeholder?"PLACEHOLDER / 待替换人物剪影":characterAnimation!=null&&characterAnimation.FrameCount>1?"ANIMATED GIF / 人物动图":"STILL IMAGE / 静态人物素材";
   } catch(Exception e) {
    if(!(e is IOException||e is InvalidDataException||e is NotSupportedException||e is ArgumentException||e is System.Runtime.InteropServices.COMException||e is System.IO.FileFormatException))throw;
    featherCharacter=false;character=Placeholder();AssetStatus="PLACEHOLDER / 素材不可用，显示占位剪影";
   }
   double fit=Math.Min(800.0/character.PixelWidth,820.0/character.PixelHeight);
   characterRect=new Rect(1175-character.PixelWidth*fit/2,900-character.PixelHeight*fit,character.PixelWidth*fit,character.PixelHeight*fit);
  }
  public static BitmapSource Placeholder() {
   DrawingVisual visual=new DrawingVisual();
   using(DrawingContext d=visual.RenderOpen()) {
    System.Windows.Media.Geometry shape=System.Windows.Media.Geometry.Parse("M 100,1100 C 150,900 235,760 310,720 C 220,690 210,560 270,420 C 250,260 310,180 400,160 C 355,120 350,45 380,15 C 385,85 415,115 455,125 C 495,105 545,105 580,135 C 610,100 640,60 645,15 C 680,100 660,160 630,205 C 700,280 665,380 680,435 L 715,475 L 682,493 C 684,540 650,565 615,585 L 620,665 C 750,690 830,785 865,925 L 935,1100 Z");
    LinearGradientBrush fill=new LinearGradientBrush(Color.FromRgb(45,38,57),Color.FromRgb(12,11,17),90);
    d.DrawGeometry(fill,new Pen(new SolidColorBrush(Color.FromRgb(142,119,159)),3),shape);
    Pen seam=new Pen(new SolidColorBrush(Color.FromRgb(83,68,99)),2);
    d.DrawGeometry(null,seam,System.Windows.Media.Geometry.Parse("M 420,245 C 300,440 450,610 290,845 M 450,290 C 345,480 515,600 370,740 M 620,665 C 550,750 520,920 575,1100 M 310,720 C 420,780 470,900 460,1100"));
   }
   RenderTargetBitmap image=new RenderTargetBitmap(1000,1100,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();return image;
  }
  private void BuildParticles() {
   Random rng=new Random(8261);
   FormatConvertedBitmap bitmap=new FormatConvertedBitmap(character,PixelFormats.Bgra32,null,0);
   int w=bitmap.PixelWidth,h=bitmap.PixelHeight;byte[] data=new byte[w*h*4];bitmap.CopyPixels(data,w*4,0);
   // Opaque PNG portraits need the same edge blending as JPEG; preserve genuine cutout alpha.
   if(!featherCharacter) {
    featherCharacter=true;
    for(int i=3;i<data.Length;i+=4)if(data[i]<255) { featherCharacter=false;break; }
   }
   for(int i=0;i<1500;i++) {
    int x=0,y=0;
    for(int attempt=0;attempt<80;attempt++) { x=rng.Next(w);y=rng.Next(h);if(data[(y*w+x)*4+3]>50)break; }
    double angle=rng.NextDouble()*Math.PI*2;
    particles.Add(new Particle { X=characterRect.X+x*characterRect.Width/w,Y=characterRect.Y+y*characterRect.Height/h,
     SX=800+Math.Cos(angle)*(200+rng.NextDouble()*750),SY=450+Math.Sin(angle)*(120+rng.NextDouble()*500),
     Size=.5+rng.NextDouble()*1.3,Seed=rng.NextDouble()*6.28 });
   }
  }
  private void Line(DrawingContext d,string color,double width,double x1,double y1,double x2,double y2) {
   d.DrawLine(new Pen(Ink(color),width),new Point(x1,y1),new Point(x2,y2));
  }
  private void Text(DrawingContext d,string value,double x,double y,double size,string color,bool bold,bool center) {
   string key=value+"|"+size+"|"+color+"|"+bold;
   FormattedText text;
   if(!texts.TryGetValue(key,out text)) {
    text=new FormattedText(value,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,
     new Typeface(new FontFamily("Segoe UI, Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,Ink(color),1);
    texts[key]=text;
   }
   d.DrawText(text,new Point(center?x-text.Width/2:x,y));
  }
  private void Mark(DrawingContext d,double x,double y,double r,string color) {
   Point[] p={new Point(x,y-r),new Point(x+r*.32,y-r*.32),new Point(x+r,y),new Point(x+r*.32,y+r*.32),new Point(x,y+r),new Point(x-r*.32,y+r*.32),new Point(x-r,y),new Point(x-r*.32,y-r*.32)};
   for(int i=0;i<p.Length;i++)d.DrawLine(new Pen(Ink(color),1.1),p[i],p[(i+1)%p.Length]);
   d.DrawEllipse(null,new Pen(Ink(color),.7),new Point(x,y),r*.24,r*.24);
  }
  private static Point Polar(double radius,double degrees) { double a=degrees*Math.PI/180;return new Point(800+Math.Cos(a)*radius,446+Math.Sin(a)*radius); }
  private void Arc(DrawingContext d,double radius,double start,double sweep,string color,double width) {
   StreamGeometry g=new StreamGeometry();using(StreamGeometryContext c=g.Open()) {
    c.BeginFigure(Polar(radius,start),false,false);
    c.ArcTo(Polar(radius,start+sweep),new Size(radius,radius),0,sweep>180,SweepDirection.Clockwise,true,false);
   }g.Freeze();d.DrawGeometry(null,new Pen(Ink(color),width),g);
  }
  protected override void OnRender(DrawingContext d) {
   base.OnRender(d);double t=Time;
   d.DrawRectangle(Brushes.Black,null,new Rect(0,0,1600,900));
   d.PushOpacity(PreviewTimeline.Scene(t));
   // Launch reveals the entire window after its first rendered frame; the mark must already be visible then.
   double intro=IsLaunch?1-PreviewTimeline.Fade(t,1.5,.65):PreviewTimeline.Intro(t);
   if(intro>0) { d.PushOpacity(intro);Mark(d,800,370,32,"#C0B8AF");Text(d,"Codex",800,421,44,"#EBE4D3",true,true);
    Text(d,"创 作 序 章",800,487,13,"#8A8485",false,true);Line(d,"#514953",1,744,535,856,535);d.Pop(); }
   double hud=PreviewTimeline.Hud(t);
   if(hud>0) { d.PushOpacity(hud);DrawHud(d,t);d.Pop(); }
   if(t>7.4)DrawReveal(d,t);
   d.Pop();
  }
  private void DrawHud(DrawingContext d,double t) {
   d.DrawRectangle(Ink("#1B171F"),null,new Rect(0,0,1600,900));d.DrawRectangle(atmosphere,null,new Rect(0,0,1600,900));
   // Low-contrast diagonal panes echo the reference's purple-grey glass surface.
   d.DrawGeometry(Ink("#183F3549"),null,System.Windows.Media.Geometry.Parse("M 1120,0 L 1370,0 L 940,900 L 690,900 Z"));
   for(int y=0;y<900;y+=5)Line(d,"#0BDFD5D0",.6,0,y,1600,y);
   Mark(d,84,77,15,"#C2B5B7");Text(d,"Codex",112,53,27,"#E8DDD4",true,false);
   Text(d,"A U R A  /  V I S U A L  S T U D Y",113,91,9,"#978892",false,false);
   Text(d,"MOTION DESIGN   /   001",1515,66,11,"#C5B5C4",false,true);
   Line(d,"#66565F",1,70,135,1530,135);Line(d,"#50454E",1,70,764,1530,764);
   Text(d,"01 / ORBITAL COMPOSITION",85,157,10,"#AA98A8",false,false);
   Text(d,"DECORATIVE INTERFACE",1280,157,10,"#AA98A8",false,false);
   double expand=.72+.28*PreviewTimeline.Fade(t,1.6,1.8);
   d.PushTransform(new ScaleTransform(expand,expand,800,446));
   d.DrawEllipse(Radial("#245D4B65","#00000000",new Point(.5,.5),.5,.5),null,new Point(800,446),305,305);
   foreach(double r in new double[]{192,205,231,242,263,270})d.DrawEllipse(null,new Pen(Ink(r==263?"#BAA49E":"#74616F"),r==263?1.3:.65),new Point(800,446),r,r);
   for(int i=0;i<180;i++) {
    Point a=Polar(270,i*2+t*2),b=Polar(i%5==0?282:275,i*2+t*2);
    d.DrawLine(new Pen(Ink(i%5==0?"#C4B5AF":"#74636E"),i%5==0?1.4:.65),a,b);
   }
   for(int i=0;i<12;i++) {
    double start=i*30+t*7;Arc(d,253,start,19,"#9E858A",5);Arc(d,253,start+2,4,"#E5D2BE",5);
    Arc(d,218,i*30-t*10,17,"#9A8293",1.5);
   }
   Arc(d,186,t*16,118,"#D0B6BE",1.3);Arc(d,186,t*16+180,75,"#A790A6",1.3);
   for(int i=0;i<4;i++) {
    Point p=Polar(295,i*90);d.DrawEllipse(Ink("#DAC7BC"),null,p,3,3);
    Line(d,"#9E8994",1,p.X-8,p.Y,p.X+8,p.Y);Line(d,"#9E8994",1,p.X,p.Y-8,p.X,p.Y+8);
   }
   Text(d,"O R B I T  /  S T U D Y",800,370,10,"#B09BAF",false,true);
   Text(d,"创 作 星 域",800,410,28,"#E7D9D3",true,true);
   Line(d,"#85707F",1,730,461,870,461);Text(d,"C O D E X",800,480,19,"#CEBACB",false,true);
   Text(d,"纯视觉演绎 · 非系统状态",800,527,11,"#9F8D9F",false,true);d.Pop();
   Panel(d,87,267,"FORM / 形态",new string[]{"CONCENTRIC   /   同心","RADIAL   /   放射","ORBITAL   /   环绕"});
   Panel(d,1198,267,"PALETTE / 色谱",new string[]{"PLUM GREY   /   紫灰","WARM IVORY   /   暖白","DUSTED ROSE   /   灰粉"});
   for(int i=0;i<92;i++) {
    double h=5+Math.Abs(Math.Sin(i*1.8+t*1.2)*Math.Cos(i*.19-t*.6))*25;
    Line(d,"#8F7C8F",.9,90+i*3.3,627-h/2,90+i*3.3,627+h/2);
   }
   Text(d,"WAVEFORM / PROCEDURAL",90,656,9,"#A78F9F",false,false);
   for(int i=0;i<5;i++)Line(d,"#695667",1,1200,594+i*13,1465-i*26,594+i*13);
   double sy=180+(t*92)%550;
   for(int i=0;i<12;i++)Line(d,i==0?"#C2AEA6":"#099A829D",i==0?1:3,75,sy-i*3,1525,sy-i*3);
   Text(d,"S C E N E  0 2  /  A U R A",87,799,11,"#9B859A",false,false);
   Text(d,"FICTIONAL GRAPHICS  ·  NO ACCOUNT OR MODEL STATUS",1125,799,10,"#9B859A",false,true);
   foreach(double x in new double[]{70,1530})foreach(double y in new double[]{135,764}) {
    Line(d,"#B59C9F",1,x,y,x+(x<800?18:-18),y);Line(d,"#B59C9F",1,x,y,x,y+(y<450?18:-18));
   }
  }
  private void Panel(DrawingContext d,double x,double y,string title,string[] rows) {
   Text(d,title,x,y,13,"#D5C1C6",true,false);Line(d,"#786373",1,x,y+33,x+305,y+33);
   for(int i=0;i<rows.Length;i++) {
    double ry=y+58+i*61;Text(d,"0"+(i+1),x,ry,10,"#89758B",false,false);
    Text(d,rows[i],x+32,ry-2,11,"#C0AABD",false,false);Line(d,"#5E4D60",.7,x,ry+29,x+305,ry+29);
    d.DrawRectangle(Ink("#9C859D"),null,new Rect(x+280,ry+2,17,3));
   }
  }
  private void DrawReveal(DrawingContext d,double t) {
   double appear=PreviewTimeline.Fade(t,7.4,.9);
   d.PushOpacity(appear);d.DrawRectangle(halo,null,new Rect(0,0,1600,900));
   // Curved threads and independent motes continue moving around the character layer.
   double threads=PreviewTimeline.Fade(t,9.8,2);
   d.PushOpacity(threads*.65);
   for(int i=0;i<18;i++) {
    StreamGeometry curve=new StreamGeometry();using(StreamGeometryContext c=curve.Open()) {
     double y=240+i*34+Math.Sin(t*.45+i)*24;c.BeginFigure(new Point(50,y+180),false,false);
     c.BezierTo(new Point(440,y-150),new Point(940,y+160),new Point(1550,y-200),true,false);
    }curve.Freeze();d.DrawGeometry(null,new Pen(Ink(i%3==0?"#8B746F89":"#315F526C"),i%3==0?1.2:.7),curve);
   }d.Pop();
   double alpha=PreviewTimeline.Character(t);
   if(alpha>0) {
    d.PushOpacity(alpha);
    BitmapSource frame=characterAnimation==null?character:characterAnimation.FrameAt(Math.Max(0,t-9.8)*cfg.Preview.DurationSeconds/16);
    if(featherCharacter) {
     // Feather opaque image edges at render time; keep the original file and full composition.
     LinearGradientBrush horizontal=new LinearGradientBrush();horizontal.StartPoint=new Point(0,0);horizontal.EndPoint=new Point(1,0);
     horizontal.MappingMode=BrushMappingMode.Absolute;horizontal.StartPoint=new Point(characterRect.Left,0);horizontal.EndPoint=new Point(characterRect.Right,0);
     horizontal.GradientStops.Add(new GradientStop(Colors.Transparent,0));horizontal.GradientStops.Add(new GradientStop(Colors.Black,.13));
     horizontal.GradientStops.Add(new GradientStop(Colors.Black,.9));horizontal.GradientStops.Add(new GradientStop(Colors.Transparent,1));
     LinearGradientBrush vertical=new LinearGradientBrush();vertical.MappingMode=BrushMappingMode.Absolute;
     vertical.StartPoint=new Point(0,characterRect.Top);vertical.EndPoint=new Point(0,characterRect.Bottom);
     vertical.GradientStops.Add(new GradientStop(Colors.Transparent,0));vertical.GradientStops.Add(new GradientStop(Colors.Black,.12));
     vertical.GradientStops.Add(new GradientStop(Colors.Black,1));
     d.PushOpacityMask(horizontal);d.PushOpacityMask(vertical);d.DrawImage(frame,characterRect);d.Pop();d.Pop();
    } else d.DrawImage(frame,characterRect);
    d.Pop();
   }
   double gather=PreviewTimeline.Fade(t,7.9,3.1),dissolve=1-PreviewTimeline.Fade(t,10.7,1.7);
   d.PushOpacity(dissolve);
   foreach(Particle p in particles) {
    double q=Geometry.Ease((gather-.12*Math.Sin(p.Seed))/.88);
    double x=p.SX+(p.X-p.SX)*q+Math.Sin(t*1.5+p.Seed)*70*(1-q);
    double y=p.SY+(p.Y-p.SY)*q+Math.Cos(t+p.Seed)*45*(1-q);
    d.DrawEllipse(Ink(p.Size>1.4?"#CCDBCCCE":"#7AAB96B3"),null,new Point(x,y),p.Size,p.Size);
   }d.Pop();
   d.PushOpacity(threads);
   for(int i=0;i<85;i++) {
    double x=(i*139.7+t*(3+i%4))%1570,y=100+(i*73.1)%740+Math.Sin(t*.5+i)*13;
    d.DrawEllipse(Ink(i%4==0?"#BECEB9B4":"#4F95859E"),null,new Point(x,y),i%4==0?1.3:.7,i%4==0?1.3:.7);
   }
   // Keep copy legible while retaining the reference's left-title / right-portrait composition.
   LinearGradientBrush shade=new LinearGradientBrush();shade.StartPoint=new Point(0,0);shade.EndPoint=new Point(1,0);
   shade.GradientStops.Add(new GradientStop(Color.FromArgb(210,5,7,10),0));shade.GradientStops.Add(new GradientStop(Color.FromArgb(0,5,7,10),1));
   d.DrawRectangle(shade,null,new Rect(0,0,870,900));d.Pop();
   d.PushOpacity(PreviewTimeline.Title(t));
   Mark(d,317,310,24,"#BEACB5");
   // Welcome copy wraps within its own panel and never crosses the character.
   FormattedText welcome=new FormattedText(cfg.Preview.WelcomeText,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,
    new Typeface("Microsoft YaHei UI"),22,Ink("#CBBFC7"),1);welcome.MaxTextWidth=540;welcome.MaxTextHeight=100;
   welcome.TextAlignment=TextAlignment.Center;welcome.Trimming=TextTrimming.CharacterEllipsis;d.DrawText(welcome,new Point(47,357));
   d.PushTransform(new SkewTransform(-9,0,317,520));Text(d,"Codex",317,454,108,"#F1E6CE",true,true);d.Pop();
   Text(d,"创 意 由 此 展 开",317,602,16,"#B4A7B9",false,true);
   Line(d,"#756575",1,240,653,394,653);Text(d,"A  S P A C E  F O R  W H A T ' S  N E X T",317,686,9,"#85758F",false,true);
   Text(d,AssetStatus,1110,838,12,"#D2B6C8",false,true);
   Text(d,(IsLaunch?"Codex Aura":"独立动画预览")+(characterAnimation!=null&&characterAnimation.FrameCount>1?" · 按原始帧速循环":" · 人物为静态图层"),1110,862,10,"#9E899F",false,true);
   if(!String.IsNullOrEmpty(AudioStatus))Text(d,AudioStatus,80,850,12,"#D2B6C8",false,false);
   d.Pop();d.Pop();
  }
 }
}
