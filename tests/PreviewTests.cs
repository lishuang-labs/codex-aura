using System;
using System.IO;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DragonCodexBoot;

internal static class PreviewTests {
 private static int checks;
 private static string IntegrationState(string runtime) {
  string directory=Path.Combine(runtime,".integration");
  if(!Directory.Exists(directory))return "absent";
  string[] files=Directory.GetFiles(directory,"*",SearchOption.AllDirectories);Array.Sort(files,StringComparer.Ordinal);
  System.Text.StringBuilder result=new System.Text.StringBuilder("present");
  using(SHA256 sha=SHA256.Create())foreach(string file in files) {
   using(FileStream stream=File.OpenRead(file))result.Append(file).Append(Convert.ToBase64String(sha.ComputeHash(stream)));
  }
  return result.ToString();
 }
 private static void Check(bool value,string label) { if(!value)throw new Exception(label);checks++; }
 private static void Reject(Config original,Action<Config> change,string label) {
  JavaScriptSerializer s=new JavaScriptSerializer();Config c=s.Deserialize<Config>(s.Serialize(original));change(c);
  bool rejected=false;try { PreviewEntry.Validate(c); } catch(ArgumentException) { rejected=true; }Check(rejected,label);
 }
 private static byte[] Render(PreviewScene scene,double t,int width=800,int height=450,string output=null) {
  scene.Width=width;scene.Height=height;
  scene.Measure(new Size(width,height));scene.Arrange(new Rect(0,0,width,height));
  scene.Time=t;scene.InvalidateVisual();scene.UpdateLayout();
  RenderTargetBitmap b=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);b.Render(scene);
  if(output!=null) {
   Directory.CreateDirectory(Path.GetDirectoryName(output));
   PngBitmapEncoder encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));
   using(Stream stream=File.Create(output))encoder.Save(stream);
  }
  byte[] data=new byte[width*height*4];b.CopyPixels(data,width*4,0);return data;
 }
 // Fully visible HUD backgrounds must reach every edge, including after resizing the same scene.
 private static bool FilledEdges(byte[] pixels,int width,int height) {
  for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
   if(x>0&&x<width-1&&y>0&&y<height-1)continue;
   int i=(y*width+x)*4;
   if(pixels[i+3]!=255||pixels[i]+pixels[i+1]+pixels[i+2]<40)return false;
  }
  return true;
 }
 // Synthetic animation keeps the public tests independent of any user's character artwork.
 private static string CreateGifFixture(string runtime) {
  string path=Path.Combine(runtime,"qa","synthetic.gif");Directory.CreateDirectory(Path.GetDirectoryName(path));
  using(BinaryWriter writer=new BinaryWriter(File.Create(path))) {
   // GIF89a, 1x1 canvas, two-entry global palette (red/blue).
   writer.Write(new byte[]{71,73,70,56,57,97,1,0,1,0,128,0,0,255,0,0,0,0,255});
   for(int i=0;i<2;i++) {
    writer.Write(new byte[]{33,249,4,0,(byte)(i==0?4:8),0,0,0});
    writer.Write(new byte[]{44,0,0,0,0,1,0,1,0,0});
    // LZW: clear, color index, end; delays are exactly 40 and 80 milliseconds.
    writer.Write(new byte[]{2,2,(byte)(i==0?68:76),1,0});
   }
   writer.Write((byte)59);
  }
  return path;
 }
 [STAThread]
 public static int Main(string[] args) {
  try {
   string root=args[0],runtime=Path.Combine(root,"build","DragonCodexBoot");
   string integrationBefore=IntegrationState(runtime);
   JavaScriptSerializer s=new JavaScriptSerializer();
   Config c=s.Deserialize<Config>(File.ReadAllText(Path.Combine(root,"config","launcher.example.json")));
   Config actual=s.Deserialize<Config>(File.ReadAllText(Path.Combine(runtime,"launcher.json")));
   PreviewEntry.Validate(c);PreviewEntry.Validate(actual);
   Check(c.Volume==0,"Public configuration is muted; actual volume is user-configurable");
   Check(!actual.AutoReplaceEntrypoints&&!actual.ScanAllLocalDrives,"Actual config disables integration");
   Config empty=s.Deserialize<Config>("{}");PreviewEntry.Validate(empty);
   Check(empty.Preview.DurationSeconds==16&&!empty.AutoReplaceEntrypoints,"Omitted preview settings have safe defaults");
   Reject(c,x=>x.AutoReplaceEntrypoints=true,"Reject auto integration");
   Reject(c,x=>x.ScanAllLocalDrives=true,"Reject full-drive scan");
   Reject(c,x=>x.Preview=null,"Reject null preview settings");
   Reject(c,x=>x.Preview.DurationSeconds=Double.NaN,"Reject NaN duration");
   Reject(c,x=>x.Preview.DurationSeconds=Double.PositiveInfinity,"Reject infinite duration");
   Reject(c,x=>x.Preview.DurationSeconds=7.9,"Reject too-short duration");
   Reject(c,x=>x.Preview.DurationSeconds=121,"Reject unbounded duration");
   Reject(c,x=>x.Volume=-1,"Reject negative volume");
   Reject(c,x=>x.Volume=1.1,"Reject volume over one");
   Reject(c,x=>x.Preview.WelcomeText=new String('长',121),"Reject excessive welcome copy");
   Reject(c,x=>x.Preview.CharacterPath="https://example.com/private.png","Reject remote image");
   Reject(c,x=>x.Preview.CharacterPath=@"\\server\share\image.png","Reject UNC image");
   Reject(c,x=>x.Preview.AudioPath="https://example.com/audio.mp3","Reject remote audio");
   foreach(double duration in new double[]{8,16,45,120}) {
    Check(PreviewTimeline.StoryTime(duration/2,duration)==8,"Duration scales midpoint");
    Check(PreviewTimeline.StoryTime(duration,duration)==16,"Duration reaches ending");
   }
   Check(PreviewTimeline.Intro(1)==1&&PreviewTimeline.Hud(1)==0,"Opening isolates title");
   Check(PreviewTimeline.Hud(6)==1&&PreviewTimeline.Intro(6)==0,"HUD dominates scene two");
   Check(PreviewTimeline.Hud(8.5)==0,"HUD fades before portrait");
   Check(PreviewTimeline.Character(12)==1&&PreviewTimeline.Title(13)==1,"Portrait and welcome resolve");
   Check(PreviewTimeline.Scene(16)==0,"Final scene is black");
   Check(c.Preview.CharacterPath=="assets/character-placeholder.png"&&c.Preview.CharacterIsPlaceholder,"Public template uses the bundled code-drawn placeholder");
   PreviewScene portrait=new PreviewScene(c,runtime);Check(portrait.AssetStatus.Contains("PLACEHOLDER"),"Public checkout renders without personal assets");Render(portrait,12.8);
   string gifPath=CreateGifFixture(runtime);CharacterAnimation gif=new CharacterAnimation(gifPath);
   Check(gif.FrameCount==2&&Math.Abs(gif.DurationSeconds-.12)<.000001,"Synthetic GIF frame count and unequal delays are decoded");
   Check(Object.ReferenceEquals(gif.FrameAt(0),gif.FrameAt(.02)),"Hold GIF frame until its delay expires");
   Check(!Object.ReferenceEquals(gif.FrameAt(0),gif.FrameAt(.05)),"Advance GIF frame at original cadence");
   Check(Object.ReferenceEquals(gif.FrameAt(0),gif.FrameAt(gif.DurationSeconds+.005)),"GIF loops from the beginning");
   byte[] first=new byte[4],later=new byte[first.Length];
   gif.FrameAt(0).CopyPixels(first,4,0);gif.FrameAt(.06).CopyPixels(later,4,0);
   long frameDiff=0;for(int i=0;i<first.Length;i++)frameDiff+=Math.Abs(first[i]-later[i]);
   Check(frameDiff>100,"Actual GIF frame pixels move, not a static first frame");
   c.Preview.CharacterPath=gifPath;c.Preview.CharacterIsPlaceholder=false;
   PreviewScene animated=new PreviewScene(c,runtime);Check(animated.AssetStatus.Contains("ANIMATED GIF"),"Animated character is labeled correctly");
   Render(animated,12.8);
   c.Preview.CharacterPath="assets/character-placeholder.png";c.Preview.CharacterIsPlaceholder=true;
   PreviewScene scene=new PreviewScene(c,runtime);
   Check(scene.AssetStatus.Contains("PLACEHOLDER"),"Bundled silhouette stays labeled");
   byte[] a=Render(scene,3),b=Render(scene,6);long diff=0;for(int i=0;i<a.Length;i++)diff+=Math.Abs(a[i]-b[i]);
   Check(diff>100000,"HUD motion changes rendered pixels");
   foreach(int[] size in new[]{new[]{800,450},new[]{800,500},new[]{800,600},new[]{1050,450},new[]{1761,1104}}) {
    b=Render(scene,6,size[0],size[1],Path.Combine(runtime,"qa","preview","hud-"+size[0]+"x"+size[1]+".png"));
    Check(FilledEdges(b,size[0],size[1]),"HUD fills all four edges without letterboxing at "+size[0]+"x"+size[1]);
   }
   b=Render(scene,16);long rgb=0;for(int i=0;i<b.Length;i+=4)rgb+=b[i]+b[i+1]+b[i+2];
   Check(rgb==0,"Rendered ending is fully black");
   scene.IsLaunch=true;b=Render(scene,0);long openingRgb=0;
   for(int i=0;i<b.Length;i+=4)openingRgb+=b[i]+b[i+1]+b[i+2];
   Check(openingRgb>100000,"Launch first frame already contains the Codex mark and title");
   scene.IsLaunch=false;b=Render(scene,0);long previewRgb=0;
   for(int i=0;i<b.Length;i+=4)previewRgb+=b[i]+b[i+1]+b[i+2];
   Check(previewRgb==0,"Independent preview retains its original black opening");
   c.Preview.CharacterPath="missing-image.png";scene=new PreviewScene(c,runtime);
   Check(scene.AssetStatus.Contains("素材不可用"),"Missing image uses explicit fallback");Render(scene,12.8);
   string bad=Path.Combine(runtime,"qa","invalid-character.png");Directory.CreateDirectory(Path.GetDirectoryName(bad));File.WriteAllText(bad,"not an image");
   c.Preview.CharacterPath=bad;scene=new PreviewScene(c,runtime);
   Check(scene.AssetStatus.Contains("PLACEHOLDER"),"Corrupt image uses fallback");
   string badGif=Path.Combine(runtime,"qa","invalid-character.gif");File.WriteAllText(badGif,"not a gif");
   c.Preview.CharacterPath=badGif;scene=new PreviewScene(c,runtime);
   Check(scene.AssetStatus.Contains("PLACEHOLDER"),"Corrupt GIF uses fallback");
   string custom=Path.Combine(runtime,"qa","custom-character.png");File.Copy(Path.Combine(runtime,"assets","character-placeholder.png"),custom,true);
   c.Preview.CharacterPath=custom;c.Preview.CharacterIsPlaceholder=false;c.Preview.WelcomeText="用户自己的欢迎文字";
   scene=new PreviewScene(c,runtime);Check(scene.AssetStatus.Contains("STILL IMAGE"),"Custom local PNG loads as still image");Render(scene,12.8);
   string previewSource=File.ReadAllText(Path.Combine(root,"src","SciFiPreview.cs"));
   Check(!previewSource.Contains("Native.")&&!previewSource.Contains("Process.Start")&&!previewSource.Contains("new BootWindow"),"Preview has no native-client or process-launch calls");
   string entry=File.ReadAllText(Path.Combine(root,"src","DragonCodexBoot.cs"));
   int start=entry.IndexOf("public static int Main(string[] args)");int end=entry.IndexOf("internal static int RunSelfTest",start);
   string main=entry.Substring(start,end-start);Check(main.Contains("return PreviewEntry.Run(")&&main.Contains("if(IsLaunchRequest(args))"),"Preview remains the default and client launch needs an exact explicit mode");
   Check(IntegrationState(runtime)==integrationBefore,"Preview must not create or change installed entrypoint state");
   Console.WriteLine("PASS: "+checks+" preview safety, config, timeline, asset and pixel assertions. No client or shortcut operations.");return 0;
  } catch(Exception e) { Console.Error.WriteLine(e);return 1; }
 }
}
