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
