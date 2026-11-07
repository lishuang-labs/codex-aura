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
 private static string ReadShared(string path) {
  using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
  using(StreamReader reader=new StreamReader(stream))return reader.ReadToEnd();
 }
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
 private static UIElement ReadyContent() {
  DockPanel panel=new DockPanel { Margin=new Thickness(28) };
  StackPanel navigation=new StackPanel { Width=220 };
  for(int i=0;i<9;i++)navigation.Children.Add(new Button { Content="WORKSPACE / PROJECT "+i,Margin=new Thickness(0,6,24,6),Height=38 });
  DockPanel.SetDock(navigation,Dock.Left);panel.Children.Add(navigation);
  StackPanel main=new StackPanel();
  main.Children.Add(new TextBlock { Text="AURA / WORKSPACE READY",Foreground=Brushes.White,FontSize=24,Margin=new Thickness(15,55,15,35) });
  main.Children.Add(new TextBox { Text="Disposable interface fixture",Height=180,Margin=new Thickness(15,10,15,20) });
  main.Children.Add(new Button { Content="NEW TASK",Height=42,Margin=new Thickness(15,20,15,20) });panel.Children.Add(main);return panel;
 }
 private static int Fixture(string directory,string mode) {
  Application app=new Application();
  Window window=new Window { Title="Codex Aura · disposable test window",Width=960,Height=640,Left=180,Top=130,
   Background=new SolidColorBrush(Color.FromRgb(34,29,44)),Content=ReadyContent() };
  bool splash=mode.StartsWith("splash",StringComparison.Ordinal),contentShown=false;
  if(splash) { window.Background=Brushes.AliceBlue;window.Content=new TextBlock { Text="◌",FontSize=50,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center }; }
  if(mode=="maximized")window.WindowState=WindowState.Maximized;
  Stopwatch clock=new Stopwatch();bool blocked=false,moved=false;
  DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(40) };
  timer.Tick+=delegate {
   if(splash&&mode!="splash-timeout"&&!contentShown&&clock.Elapsed.TotalSeconds>12) {
    contentShown=true;window.Background=new SolidColorBrush(Color.FromRgb(34,29,44));window.Content=ReadyContent();
    File.WriteAllText(Path.Combine(directory,"content-ready"),"ready");
   }
   if(mode=="timeout"&&!blocked&&File.Exists(Path.Combine(directory,"block-fixture"))) { blocked=true;Thread.Sleep(20000); }
   if(mode=="late"&&!blocked&&File.Exists(Path.Combine(directory,"block-fixture"))) { blocked=true;Thread.Sleep(10000); }
   if(mode=="resize"&&!moved&&clock.Elapsed.TotalSeconds>3) { moved=true;window.Left=230;window.Top=80;window.Width=1100;window.Height=710; }
   if(File.Exists(Path.Combine(directory,"close-fixture"))||File.Exists(Path.Combine(directory,"close-fixture-"+Process.GetCurrentProcess().Id)))window.Close();
  };
  window.Loaded+=delegate { clock.Start();timer.Start(); };
  window.Closed+=delegate { timer.Stop(); };
  return app.Run(window);
 }
 private static void Run(string root,string mode) {
  string folder=Path.Combine(root,".local","launch-tests",mode+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  string executable=Path.Combine(folder,"DragonCodexBoot.exe");File.Copy(Path.Combine(root,"build","DragonCodexBoot","DragonCodexBoot.exe"),executable);
  JavaScriptSerializer json=new JavaScriptSerializer();Config config=json.Deserialize<Config>(File.ReadAllText(Path.Combine(root,"config","launcher.example.json")));
  config.Preview.DurationSeconds=8;config.Preview.CharacterPath="missing-fixture-image.png";
  config.MaxWaitSeconds=mode=="late"?8:mode.StartsWith("splash")?10:2;
  config.ProcessNames=new[]{"LaunchWindowTests"};config.ProcessPathContains="LaunchWindowTests.exe";
  config.AppLaunch=@"shell:AppsFolder\CodexAura.Fixture!App"; // Even a regression cannot launch the actual client.
  config.Launch.UseDwmTransition=mode!="fallback"&&mode!="splash-fallback";
  File.WriteAllText(Path.Combine(folder,"launcher.json"),json.Serialize(config));
  Process fixture=null,launcher=null;
  try {
   fixture=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--fixture \""+folder+"\" "+mode) { UseShellExecute=false });
   IntPtr handle=WaitForFixture(fixture);Bounds before=(Bounds)Native("ClientBounds",handle);
   launcher=Process.Start(mode=="vanish"
    ?new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--recovery-launcher \""+folder+"\" vanish") { UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden }
    :new ProcessStartInfo(executable,"--launch") { UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden });
   string logPath=Path.Combine(folder,"logs","launcher.log");Stopwatch clock=Stopwatch.StartNew();bool signal=false,measured=false,remeasured=false;
   double firstReadyAt=-1;bool captured=false;
   while(!launcher.WaitForExit(50)) {
    if(clock.Elapsed.TotalSeconds>30)throw new Exception("Launcher failed to exit within test bound.");
    if(mode=="normal"||mode=="fallback") {
     if(firstReadyAt<0&&File.Exists(logPath)&&ReadShared(logPath).Contains("overlay-matched="))firstReadyAt=clock.Elapsed.TotalSeconds;
     if(!captured&&firstReadyAt>=0&&clock.Elapsed.TotalSeconds-firstReadyAt>=.45) { CaptureOpening(launcher,folder);captured=true; }
    }
    if(!measured&&clock.Elapsed.TotalSeconds>2) {
     CheckOverlay(launcher,handle,mode);measured=true;
     if(mode=="timeout"||mode=="late")File.WriteAllText(Path.Combine(folder,"block-fixture"),"block");
    }
    if(mode=="resize"&&!remeasured&&clock.Elapsed.TotalSeconds>5) { CheckOverlay(launcher,handle,"after resize");remeasured=true; }
    if(mode=="vanish"&&!signal&&File.Exists(logPath)&&ReadShared(logPath).Contains("handoff-started")) {
     File.WriteAllText(Path.Combine(folder,"close-fixture"),"close");signal=true;
    }
    if(mode.StartsWith("splash")&&!signal&&clock.Elapsed.TotalSeconds>9) {
     string pendingLog=ReadShared(logPath);
     Check(pendingLog.Contains("waiting-for-window")&&!pendingLog.Contains("handoff-started"),mode+": responsive loading page remains covered after animation duration");signal=true;
    }
   }
   string log=ReadShared(logPath);
   if(mode=="normal"||mode=="fallback")Check(captured,"Captured the first second of the opening");
   Check(log.IndexOf("opening-hidden")>=0&&log.IndexOf("first-frame-rendered")>log.IndexOf("opening-hidden")&&log.IndexOf("first-frame-rendered")<log.IndexOf("overlay-matched="),mode+": native window stays hidden until the first rendered frame");
   Check(log.Contains("existing-window-reused")&&(mode=="vanish"||!log.Contains("app-launch-requested")),mode+": only fixture reused or recovery intercepted");
   Check(!Directory.Exists(Path.Combine(folder,".integration")),mode+": no shortcut integration");
   if(mode=="timeout"||mode=="vanish"||mode=="splash-timeout") {
    Check(launcher.ExitCode==2&&log.Contains("window-wait-timeout"),mode+": bounded timeout reports failure");
    if(mode=="vanish")Check(signal&&log.Contains("closed-without-window"),"Lost fade target closes without stale handoff");
    if(mode=="splash-timeout")Check(signal&&!log.Contains("handoff-started")&&!log.Contains("handed-off-to-real-window"),"Never fade into a permanently loading client as a successful handoff");
   } else {
    Check(launcher.ExitCode==0&&log.Contains("handed-off-to-real-window foreground=True"),mode+": foreground handoff confirmed");
    Bounds after=(Bounds)Native("ClientBounds",handle);
    if(mode!="resize")Check(before.Left==after.Left&&before.Top==after.Top&&before.Width==after.Width&&before.Height==after.Height,mode+": target geometry unchanged");
    if(mode=="fallback"||mode=="splash-fallback")Check(log.Contains("dwm-disabled-crossfade")&&!log.Contains("live-window-connected"),"Explicit no-DWM fallback works");
    else Check(log.Contains("live-window-connected")&&!log.Contains("dwm-update-failed"),mode+": DWM thumbnail succeeds");
    if(mode=="late")Check(log.Contains("waiting-for-window")&&log.Contains("window-available"),"Late responsive window resumes held portrait");
    if(mode.StartsWith("splash"))Check(signal&&File.Exists(Path.Combine(folder,"content-ready"))&&log.Contains("window-available"),"Handoff waits for actual interface content instead of window responsiveness");
   }
   Console.WriteLine("PASS: "+mode+" ("+clock.Elapsed.TotalSeconds.ToString("0.0")+"s), report "+folder);
  } finally {
   // These are only processes created by this test; never enumerate or stop real client processes.
   if(launcher!=null) { if(!launcher.HasExited)launcher.Kill();launcher.Dispose(); }
   if(fixture!=null) { if(!fixture.HasExited) { File.WriteAllText(Path.Combine(folder,"close-fixture"),"close");if(!fixture.WaitForExit(1000))fixture.Kill(); }fixture.Dispose(); }
  }
 }
 // Replace only the activation boundary with a test signal; exercise the actual BootWindow lifecycle.
 private static int RecoveryLauncher(string folder,string mode) {
  Application app=new Application();
  Config config=new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(Path.Combine(folder,"launcher.json")));
  int attempts=0;
  Action activation=delegate {
   attempts++;File.WriteAllText(Path.Combine(folder,"activation-requests"),attempts.ToString());
   if(mode=="activation-failure"&&attempts==1)throw new System.ComponentModel.Win32Exception(2,"Synthetic activation failure");
  };
  BootWindow window=(BootWindow)Activator.CreateInstance(typeof(BootWindow),BindingFlags.Instance|BindingFlags.NonPublic,null,
   new object[]{config,folder,true,activation},null);
  int result=app.Run(window);return result!=0?result:window.ExitCode;
 }
 private static Process StartFixture(string folder) {
  return Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--fixture \""+folder+"\" normal") { UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden });
 }
 private static void RunRecovery(string root,string mode) {
  string folder=Path.Combine(root,".local","launch-tests",mode+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  Config config=new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(Path.Combine(root,"config","launcher.example.json")));
  config.Preview.DurationSeconds=8;config.Preview.CharacterPath="missing-fixture-image.png";config.MaxWaitSeconds=mode=="missing"?11:6;
  config.ProcessNames=new[]{"LaunchWindowTests"};config.ProcessPathContains="LaunchWindowTests.exe";
  config.AppLaunch=@"shell:AppsFolder\CodexAura.Fixture!App";
  File.WriteAllText(Path.Combine(folder,"launcher.json"),new JavaScriptSerializer().Serialize(config));
  Process old=null,replacement=null,launcher=null;bool closedOld=false,measured=false;int seenRequests=0;
  try {
   if(mode=="closing") { old=StartFixture(folder);WaitForFixture(old); }
   launcher=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--recovery-launcher \""+folder+"\" "+mode) { UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden });
   string logPath=Path.Combine(folder,"logs","launcher.log"),requestsPath=Path.Combine(folder,"activation-requests");
   Stopwatch clock=Stopwatch.StartNew();double replacementAt=-1;IntPtr handle=IntPtr.Zero;
   while(!launcher.WaitForExit(50)) {
    if(clock.Elapsed.TotalSeconds>25)throw new Exception("Recovery fixture exceeded its timeout.");
    string log=File.Exists(logPath)?ReadShared(logPath):"";
    if(old!=null&&!closedOld&&log.Contains(mode=="restarting"?"overlay-matched=":"existing-window-reused")) {
     File.WriteAllText(Path.Combine(folder,"close-fixture-"+old.Id),"close");
     Check(old.WaitForExit(2000),"Old fixture exits while launcher is starting");closedOld=true;
    }
    int requests=0;
    if(File.Exists(requestsPath))Int32.TryParse(ReadShared(requestsPath),out requests);
    if(requests>seenRequests) {
     seenRequests=requests;
     bool create=mode!="missing"&&((mode!="dropped"&&mode!="activation-failure")||requests>=2);
     if(create&&mode=="restarting"&&requests==1) { old=StartFixture(folder);WaitForFixture(old); }
     else if(create&&replacement==null) { replacement=StartFixture(folder);handle=WaitForFixture(replacement);replacementAt=clock.Elapsed.TotalSeconds; }
    }
    if(replacement!=null&&!measured&&clock.Elapsed.TotalSeconds-replacementAt>1.5) { CheckOverlay(launcher,handle,mode);measured=true; }
   }
   string finalLog=ReadShared(logPath);
   int attempts=Int32.Parse(File.ReadAllText(requestsPath));
   Check(!Directory.Exists(Path.Combine(folder,".integration")),mode+": recovery does not install shortcuts");
   if(mode=="missing") {
    Check(attempts==LaunchRecovery.MaxAttempts,"Absent client receives only three bounded attempts");
    Check(launcher.ExitCode==2&&finalLog.Contains("initial-window-wait-timeout"),"Absent client reports startup failure");
    Check(!finalLog.Contains("overlay-matched=")&&!finalLog.Contains("handed-off-to-real-window"),"No animation or successful handoff without a stable client window");
   } else {
    Check(measured&&replacement!=null&&!replacement.HasExited,mode+": replacement stays alive after animation");
    Check(launcher.ExitCode==0&&finalLog.Contains("handed-off-to-real-window foreground=True"),mode+": recovered window receives handoff");
    Check(attempts==(mode=="closing"?1:2),mode+": recovery stops requesting activation after the window appears");
    if(mode=="closing")Check(closedOld&&finalLog.IndexOf("app-activation-attempt=1")<finalLog.IndexOf("overlay-matched="),"Closing old window cannot start the animation before replacement is stable");
    if(mode=="restarting")Check(closedOld&&finalLog.IndexOf("app-activation-attempt=2")>finalLog.IndexOf("overlay-matched="),"Window lost during animation triggers a fresh activation");
    if(mode=="activation-failure")Check(finalLog.Contains("app-launch-failed:"),"Transient activation exception is recovered");
   }
   Console.WriteLine("PASS: "+mode+" ("+clock.Elapsed.TotalSeconds.ToString("0.0")+"s), report "+folder);
  } finally {
   if(launcher!=null) { if(!launcher.HasExited)launcher.Kill();launcher.Dispose(); }
   foreach(Process process in new[]{old,replacement})if(process!=null) {
    if(!process.HasExited) { File.WriteAllText(Path.Combine(folder,"close-fixture-"+process.Id),"close");if(!process.WaitForExit(1000))process.Kill(); }process.Dispose();
   }
  }
 }
 [STAThread]
 public static int Main(string[] args) {
  try {
   if(args[0]=="--fixture")return Fixture(args[1],args[2]);
   if(args[0]=="--recovery-launcher")return RecoveryLauncher(args[1],args[2]);

   foreach(string mode in args.Length>1?args[1].Split(','):new[]{"normal","fallback","late","timeout","vanish","resize","maximized","closing","dropped","restarting","activation-failure","missing","splash","splash-fallback","splash-timeout"}) {
    if(mode=="closing"||mode=="dropped"||mode=="restarting"||mode=="activation-failure"||mode=="missing")RunRecovery(args[0],mode);
    else Run(args[0],mode);
   }
   Console.WriteLine("PASS: "+checks+" assertions against disposable Windows fixtures; no real Codex launch.");return 0;
  } catch(Exception e) { Console.Error.WriteLine(e);return 1; }
 }
}
