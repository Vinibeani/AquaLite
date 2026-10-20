using System;
namespace AquaLite {
 public static class PlaybackTiming {
  public static int Remaining(string profile,double elapsedMilliseconds){
   double interval=profile=="Fluido"?1000.0/30:profile=="Equilibrado"?50:100;
   return Math.Max(1,(int)Math.Ceiling(interval-Math.Max(0,elapsedMilliseconds)));
  }
 }
}
