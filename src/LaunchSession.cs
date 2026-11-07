using System;

namespace DragonCodexBoot {
 // Optional DWM presentation; disabling it still hands input to the real window.
 public sealed class LaunchConfig {
  public LaunchConfig() { UseDwmTransition=true; }
  public bool UseDwmTransition { get; set; }
 }

 // Window readiness is a visible/responsive/stable heuristic, never an account or model status.
 public sealed class WindowReadiness {
  private IntPtr previous;
  private double since;
  public bool Observe(IntPtr candidate,bool responsive,double now) {
   if(candidate==IntPtr.Zero||!responsive) { previous=IntPtr.Zero;return false; }
   if(candidate!=previous) { previous=candidate;since=now;return false; }
   return now-since>=.6;
  }
 }

 // Visual heuristic: reject blank/splash frames; never inspect text, accounts or model state.
 public static class StartupContent {
  public const int Width=160,Height=100;
  public static bool HasInterface(byte[] pixels) {
   if(pixels==null||pixels.Length!=Width*Height*4)return false;
   int[] regions=new int[9];int edges=0;
   for(int y=12;y<92;y++)for(int x=4;x<Width-4;x++) {
    // Ignore native title-bar controls and the centered startup logo/spinner.
    if(x>=64&&x<=96&&y>=35&&y<=65)continue;
    int i=(y*Width+x)*4,contrast=0;
    for(int channel=0;channel<3;channel++)contrast=Math.Max(contrast,Math.Abs(pixels[i+channel]-pixels[i-4+channel]));
    if(contrast<24)continue;
    edges++;regions[(y*3/Height)*3+x*3/Width]++;
   }
   int populated=0;foreach(int count in regions)if(count>=6)populated++;
   return edges>=50&&populated>=3;
  }
 }

 // Require multiple recent frames, and forget results when the handle or visible content changes.
 public sealed class ContentReadiness {
  private IntPtr window;
  private int samples;
  private double since,last;
  public void Observe(IntPtr candidate,bool hasInterface,double now) {
   if(candidate!=window||!hasInterface||now-last>1.5) { window=candidate;samples=0;since=now; }
   last=now;if(hasInterface)samples++;
  }
  public bool IsReady(IntPtr candidate,double now) {
   return candidate!=IntPtr.Zero&&candidate==window&&samples>=3&&last-since>=1&&now-last<=1.5;
  }
 }

 // Recover a dropped activation or a closing old window without flooding the shell with requests.
 public sealed class LaunchRecovery {
  public const int MaxAttempts=3;
  private const double RetryDelaySeconds=3,MissingGraceSeconds=.75;
  private double nextAttempt,missingSince=-1;
  private bool seenWindow;
  public int Attempts { get; private set; }
  public bool ShouldRequest(bool windowPresent,double now) {
   if(windowPresent) { seenWindow=true;missingSince=-1;return false; }
   if(missingSince<0)missingSince=now;
   if(Attempts>=MaxAttempts||now<nextAttempt||(seenWindow&&now-missingSince<MissingGraceSeconds))return false;
   Attempts++;nextAttempt=now+RetryDelaySeconds*Attempts;return true;
  }
 }

 // The same 16-second storyboard runs until the portrait hold, then waits for a real window.
 // Pure clock state makes timeout, late readiness and window loss testable without Codex.
 public sealed class LaunchTimeline {
  private double previous;
  public readonly double Duration,HoldSeconds,MaxWaitSeconds;
  public double Seconds { get; private set; }
  public double WaitedSeconds { get; private set; }
  public bool Waiting { get; private set; }
  public bool TimedOut { get { return WaitedSeconds>=MaxWaitSeconds; } }
  public bool Complete { get { return Seconds>=Duration; } }
  public double Blend { get { return Geometry.Ease((Seconds-HoldSeconds)/(Duration-HoldSeconds)); } }
  public LaunchTimeline(double duration,double maxWait) {
   if(Double.IsNaN(duration)||Double.IsInfinity(duration)||duration<8||duration>120
    ||Double.IsNaN(maxWait)||Double.IsInfinity(maxWait)||maxWait<1||maxWait>300)
    throw new ArgumentException("Invalid launch timeline bounds.");
   Duration=duration;HoldSeconds=duration*14.6/16;MaxWaitSeconds=maxWait;
  }
  public void Advance(double now,bool ready) {
   if(Double.IsNaN(now)||Double.IsInfinity(now)||now<previous)throw new ArgumentException("Launch clock must be finite and monotonic.");
   double delta=now-previous;previous=now;
   if(TimedOut)return;
   if(!ready&&Seconds>HoldSeconds)Seconds=HoldSeconds;
   if(Seconds<HoldSeconds) {
    double step=Math.Min(delta,HoldSeconds-Seconds);Seconds+=step;delta-=step;
   }
   Waiting=Seconds>=HoldSeconds&&!ready;
   if(Waiting)WaitedSeconds+=delta;
   else Seconds=Math.Min(Duration,Seconds+delta);
  }
 }
}
