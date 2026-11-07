using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DragonCodexBoot;

// Opt-in Windows integration tests use only a task-owned blank fixture, never real Codex.
internal static class LaunchWindowTests {
 private static int checks;
 private delegate bool EnumCallback(IntPtr window,IntPtr state);
 [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback,IntPtr state);
 [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
 [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
 private static IntPtr FindOverlay(int processId) {
  IntPtr result=IntPtr.Zero;
  EnumWindows(delegate(IntPtr window,IntPtr state) {
   uint id;GetWindowThreadProcessId(window,out id);
   if(id==processId&&(bool)Native("IsWindowVisible",window)&&((Bounds)Native("ClientBounds",window)).Width>=450) { result=window;return false; }
   return true;
  },IntPtr.Zero);return result;
 }
 private static void CheckOverlay(Process process,IntPtr target,string label) {
  IntPtr overlay=FindOverlay(process.Id);Check(overlay!=IntPtr.Zero,label+": overlay exists");
  Bounds a=(Bounds)Native("ClientBounds",overlay),b=(Bounds)Native("VisibleBounds",target);
  Check(Math.Abs(a.Left-b.Left)<=1&&Math.Abs(a.Top-b.Top)<=1&&Math.Abs(a.Width-b.Width)<=1&&Math.Abs(a.Height-b.Height)<=1,
   label+": exact visible-window bounds; actual="+a.Left+","+a.Top+","+a.Width+","+a.Height+" expected="+b.Left+","+b.Top+","+b.Width+","+b.Height);
  int style=(int)Native("GetWindowLong",overlay,-20);
  Check((style&0x40000)==0&&(style&0x80)!=0,label+": no taskbar or Alt-Tab app entry");
  int cloaked;Check(DwmGetWindowAttribute(overlay,14,out cloaked,4)==0&&cloaked==0,label+": prepared animation is exposed after the first frame");
 }
 private static void Check(bool ok,string label) { if(!ok)throw new Exception(label);checks++; }
 private static void CaptureOpening(Process process,string folder) {
  IntPtr overlay=FindOverlay(process.Id);Check(overlay!=IntPtr.Zero,"Opening overlay exists");
  Bounds bounds=(Bounds)Native("ClientBounds",overlay);
  using(System.Drawing.Bitmap frame=new System.Drawing.Bitmap(bounds.Width,bounds.Height)) {
   using(System.Drawing.Graphics graphics=System.Drawing.Graphics.FromImage(frame))graphics.CopyFromScreen(bounds.Left,bounds.Top,0,0,frame.Size);
   frame.Save(Path.Combine(folder,"opening.png"),System.Drawing.Imaging.ImageFormat.Png);
   int bright=0;
   for(int y=bounds.Height/3;y<bounds.Height*2/3;y+=2)for(int x=bounds.Width/3;x<bounds.Width*2/3;x+=2) {
    System.Drawing.Color color=frame.GetPixel(x,y);if(color.R>130&&color.G>120&&color.B>110)bright++;
   }
   Check(bright>100,"Opening screen pixels contain the title instead of an empty black surface");
  }
 }
 private static object Native(string method,params object[] args) {
  return typeof(Entry).Assembly.GetType("DragonCodexBoot.Native").GetMethod(method).Invoke(null,args);
 }
 private static IntPtr WaitForFixture(Process process) {
  Stopwatch clock=Stopwatch.StartNew();
  while(clock.Elapsed.TotalSeconds<8) {
   process.Refresh();if(process.HasExited)throw new Exception("Fixture exited before launch.");
   if(process.MainWindowHandle!=IntPtr.Zero)return process.MainWindowHandle;
   Thread.Sleep(40);
  }
  throw new Exception("Fixture window unavailable.");
 }
 private static int Fixture(string directory,string mode) {
  Application app=new Application();
  Window window=new Window { Title="Codex Aura · disposable test window",Width=960,Height=640,Left=180,Top=130,
   Background=new SolidColorBrush(Color.FromRgb(34,29,44)),Content=new TextBlock { Text="AURA / WINDOW HANDOFF TEST",FontSize=28,
    Foreground=Brushes.LightGray,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center } };
  if(mode=="maximized")window.WindowState=WindowState.Maximized;
  Stopwatch clock=new Stopwatch();bool blocked=false,moved=false;
  DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(40) };
  timer.Tick+=delegate {
   if(mode=="timeout"&&!blocked&&clock.Elapsed.TotalSeconds>.3) { blocked=true;Thread.Sleep(20000); }
   if(mode=="late"&&!blocked&&clock.Elapsed.TotalSeconds>.3) { blocked=true;Thread.Sleep(10000); }
   if(mode=="resize"&&!moved&&clock.Elapsed.TotalSeconds>3) { moved=true;window.Left=230;window.Top=80;window.Width=1100;window.Height=710; }
   if(File.Exists(Path.Combine(directory,"close-fixture")))window.Close();
  };
  window.Loaded+=delegate { clock.Start();timer.Start(); };
  window.Closed+=delegate { timer.Stop(); };
  return app.Run(window);
 }
 private static void Run(string root,string mode) {
  string folder=Path.Combine(root,".local","launch-tests",mode+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  string executable=Path.Combine(folder,"DragonCodexBoot.exe");File.Copy(Path.Combine(root,"build","DragonCodexBoot","DragonCodexBoot.exe"),executable);
  JavaScriptSerializer json=new JavaScriptSerializer();Config config=json.Deserialize<Config>(File.ReadAllText(Path.Combine(root,"config","launcher.example.json")));
  config.Preview.DurationSeconds=8;config.Preview.CharacterPath="missing-fixture-image.png";config.MaxWaitSeconds=mode=="late"?6:1;
  config.ProcessNames=new[]{"LaunchWindowTests"};config.ProcessPathContains="LaunchWindowTests.exe";
  config.AppLaunch=@"shell:AppsFolder\CodexAura.Fixture!App"; // Even a regression cannot launch the actual client.
  config.Launch.UseDwmTransition=mode!="fallback";
  File.WriteAllText(Path.Combine(folder,"launcher.json"),json.Serialize(config));
  Process fixture=null,launcher=null;
  try {
   fixture=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--fixture \""+folder+"\" "+mode) { UseShellExecute=false });
   IntPtr handle=WaitForFixture(fixture);Bounds before=(Bounds)Native("ClientBounds",handle);
   launcher=Process.Start(new ProcessStartInfo(executable,"--launch") { UseShellExecute=false });
   string logPath=Path.Combine(folder,"logs","launcher.log");Stopwatch clock=Stopwatch.StartNew();bool signal=false,measured=false,remeasured=false;
   double firstReadyAt=-1;bool captured=false;
   while(!launcher.WaitForExit(50)) {
    if(clock.Elapsed.TotalSeconds>30)throw new Exception("Launcher failed to exit within test bound.");
    if(mode=="normal"||mode=="fallback") {
     if(firstReadyAt<0&&File.Exists(logPath)&&File.ReadAllText(logPath).Contains("overlay-matched="))firstReadyAt=clock.Elapsed.TotalSeconds;
     if(!captured&&firstReadyAt>=0&&clock.Elapsed.TotalSeconds-firstReadyAt>=.45) { CaptureOpening(launcher,folder);captured=true; }
    }
    if(!measured&&clock.Elapsed.TotalSeconds>2) { CheckOverlay(launcher,handle,mode);measured=true; }
    if(mode=="resize"&&!remeasured&&clock.Elapsed.TotalSeconds>5) { CheckOverlay(launcher,handle,"after resize");remeasured=true; }
    if(mode=="vanish"&&!signal&&File.Exists(logPath)&&File.ReadAllText(logPath).Contains("handoff-started")) {
     File.WriteAllText(Path.Combine(folder,"close-fixture"),"close");signal=true;
    }
   }
   string log=File.ReadAllText(logPath);
   if(mode=="normal"||mode=="fallback")Check(captured,"Captured the first second of the opening");
   Check(log.IndexOf("opening-hidden")>=0&&log.IndexOf("first-frame-rendered")>log.IndexOf("opening-hidden")&&log.IndexOf("first-frame-rendered")<log.IndexOf("overlay-matched="),mode+": native window stays hidden until the first rendered frame");
   Check(log.Contains("existing-window-reused")&&!log.Contains("app-launch-requested"),mode+": only fixture reused");
   Check(!Directory.Exists(Path.Combine(folder,".integration")),mode+": no shortcut integration");
   if(mode=="timeout"||mode=="vanish") {
    Check(launcher.ExitCode==2&&log.Contains("window-wait-timeout"),mode+": bounded timeout reports failure");
    if(mode=="vanish")Check(signal&&log.Contains("closed-without-window"),"Lost fade target closes without stale handoff");
   } else {
    Check(launcher.ExitCode==0&&log.Contains("handed-off-to-real-window foreground=True"),mode+": foreground handoff confirmed");
    Bounds after=(Bounds)Native("ClientBounds",handle);
    if(mode!="resize")Check(before.Left==after.Left&&before.Top==after.Top&&before.Width==after.Width&&before.Height==after.Height,mode+": target geometry unchanged");
    if(mode=="fallback")Check(log.Contains("dwm-disabled-crossfade")&&!log.Contains("live-window-connected"),"Explicit no-DWM fallback works");
    else Check(log.Contains("live-window-connected")&&!log.Contains("dwm-update-failed"),mode+": DWM thumbnail succeeds");
    if(mode=="late")Check(log.Contains("waiting-for-window")&&log.Contains("window-available"),"Late responsive window resumes held portrait");
   }
   Console.WriteLine("PASS: "+mode+" ("+clock.Elapsed.TotalSeconds.ToString("0.0")+"s), report "+folder);
  } finally {
   // These are only processes created by this test; never enumerate or stop real client processes.
   if(launcher!=null) { if(!launcher.HasExited)launcher.Kill();launcher.Dispose(); }
   if(fixture!=null) { if(!fixture.HasExited) { File.WriteAllText(Path.Combine(folder,"close-fixture"),"close");if(!fixture.WaitForExit(1000))fixture.Kill(); }fixture.Dispose(); }
  }
 }
 [STAThread]
 public static int Main(string[] args) {
  try {
   if(args[0]=="--fixture")return Fixture(args[1],args[2]);

   foreach(string mode in args.Length>1?args[1].Split(','):new[]{"normal","fallback","late","timeout","vanish","resize","maximized"})Run(args[0],mode);
   Console.WriteLine("PASS: "+checks+" assertions against disposable Windows fixtures; no real Codex launch.");return 0;
  } catch(Exception e) { Console.Error.WriteLine(e);return 1; }
 }
}
