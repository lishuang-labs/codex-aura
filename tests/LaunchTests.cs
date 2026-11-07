using System;
using System.IO;
using System.Web.Script.Serialization;
using DragonCodexBoot;

internal static class LaunchTests {
 private static int checks;
 private static void Check(bool value,string label) { if(!value)throw new Exception(label);checks++; }
 private static void Reject(Config original,Action<Config> change,string label) {
  JavaScriptSerializer s=new JavaScriptSerializer();Config copy=s.Deserialize<Config>(s.Serialize(original));change(copy);
  bool rejected=false;try { Entry.ValidateLaunchConfig(copy); } catch(ArgumentException) { rejected=true; }Check(rejected,label);
 }
 public static int Main(string[] args) {
  try {
   Config c=new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(args[0]));Entry.ValidateLaunchConfig(c);
   Check(Entry.IsLaunchRequest(new string[]{"--launch"}),"Explicit launch accepted");
   foreach(string[] input in new string[][] {new string[0],new[]{"--preview"},new[]{"--self-test"},new[]{"--preview-frames"},
    new[]{"--launch","--preview"},new[]{"--launch","--integrate-entrypoints"},new[]{"--restore-entrypoints"},new[]{"--LAUNCH"}})
    Check(!Entry.IsLaunchRequest(input),"No mixed, implicit or legacy launch route");
   Check(!Entry.IsLaunchRequest(null),"Null arguments never launch");
   c.Video=null;c.ScreenFrames=null;c.HoldAt=Double.NaN;c.TransitionStart=Double.NaN;
   Entry.ValidateLaunchConfig(c);Check(true,"Procedural launch needs neither MP4 nor video calibration");
   Reject(c,x=>x.AppLaunch="cmd.exe /c echo unsafe","Reject executable injection");
   Reject(c,x=>x.AppLaunch+=" extra","Reject extra shell arguments");
   Reject(c,x=>x.ProcessNames=new[]{"*"},"Reject wildcard process discovery");
   Reject(c,x=>x.ProcessNames=null,"Reject missing process names");
   Reject(c,x=>x.ProcessPathContains=" ","Reject unbounded process matching");
   Reject(c,x=>x.AutoReplaceEntrypoints=true,"Launch never enables shortcut replacement");
   Reject(c,x=>x.ScanAllLocalDrives=true,"Launch never enables drive scans");
   Reject(c,x=>x.MaxWaitSeconds=Double.PositiveInfinity,"Reject infinite wait");
   Reject(c,x=>x.MaxWaitSeconds=0,"Reject missing wait bound");
   Reject(c,x=>x.MaxWaitSeconds=301,"Reject excessive wait");
   Reject(c,x=>x.Launch=null,"Reject null launch settings");
   Reject(c,x=>x.PlayerWidth=0,"Reject unusable host size");
   c.Launch.UseDwmTransition=false;Entry.ValidateLaunchConfig(c);Check(true,"Crossfade fallback is a valid launch configuration");
   WindowReadiness readiness=new WindowReadiness();IntPtr a=new IntPtr(101),b=new IntPtr(102);
   Check(!readiness.Observe(a,true,0),"New handle must stabilize");
   Check(!readiness.Observe(a,true,.5),"Short visibility is insufficient");
   Check(readiness.Observe(a,true,.7),"Stable responsive handle becomes ready");
   Check(!readiness.Observe(a,false,1),"Unresponsive handle resets readiness");
   Check(!readiness.Observe(a,true,1.2),"Recovered handle needs a new stable interval");
   Check(readiness.Observe(a,true,2),"Recovered window stabilizes");
   Check(!readiness.Observe(b,true,2.1),"Replacement handle starts a new interval");
   Check(!readiness.Observe(IntPtr.Zero,true,3),"Missing window clears readiness");
   Check(!readiness.Observe(b,true,4),"Reappearing handle is revalidated");
   foreach(double duration in new[]{8.0,16,120}) {
    LaunchTimeline t=new LaunchTimeline(duration,2);
    t.Advance(t.HoldSeconds+.5,false);
    Check(t.Waiting&&Math.Abs(t.Seconds-t.HoldSeconds)<1e-9,"Wait at portrait before final fade");
    Check(Math.Abs(t.WaitedSeconds-.5)<1e-9&&t.Blend==0,"Only actual holding time consumes wait budget");
    double now=t.HoldSeconds+.5;t.Advance(now+.2,true);
    Check(!t.Waiting&&t.Blend>0,"Late readiness resumes transition");
    t.Advance(now+.3,false);
    Check(t.Waiting&&t.Blend==0&&!t.Complete,"Window loss cancels partial transition");
    t.Advance(now+duration,true);
    Check(t.Complete&&t.Blend==1,"Recovered target reaches completed handoff");
   }
   LaunchTimeline timeout=new LaunchTimeline(16,2);timeout.Advance(16.7,false);
   Check(timeout.TimedOut&&!timeout.Complete,"Timeout closes through the failure route, not fake success");
   timeout.Advance(30,true);Check(timeout.TimedOut&&!timeout.Complete,"Late readiness cannot reset expired timeout");
   bool rejectedClock=false;try { timeout.Advance(29,true); } catch(ArgumentException) { rejectedClock=true; }
   Check(rejectedClock,"Reject backwards clock");
   Console.WriteLine("PASS: "+checks+" launch routing, config, readiness and timeline assertions. No client or desktop API calls.");return 0;
  } catch(Exception e) { Console.Error.WriteLine(e);return 1; }
 }
}
