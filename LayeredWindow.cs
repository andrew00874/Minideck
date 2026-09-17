using System;
using System.Drawing;
using System.Runtime.InteropServices;
namespace MiniDeck {
 static class LayeredWindow {
  [StructLayout(LayoutKind.Sequential)]struct Point {public int X,Y;public Point(int x,int y){X=x;Y=y;}}
  [StructLayout(LayoutKind.Sequential)]struct Size {public int Width,Height;public Size(int w,int h){Width=w;Height=h;}}
  [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend {public byte Op,Flags,Alpha,Format;}
  [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref Point destination,ref Size size,IntPtr source,ref Point origin,uint key,ref Blend blend,uint flags);
  [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
  public static void Present(IntPtr handle,Bitmap frame,System.Drawing.Point location){
   IntPtr dc=CreateCompatibleDC(IntPtr.Zero),bitmap=IntPtr.Zero,old=IntPtr.Zero;
   try {bitmap=frame.GetHbitmap(Color.FromArgb(0));old=SelectObject(dc,bitmap);
    Point dest=new Point(location.X,location.Y),origin=new Point(0,0);Size size=new Size(frame.Width,frame.Height);Blend blend=new Blend{Op=0,Alpha=255,Format=1};
    if(!UpdateLayeredWindow(handle,IntPtr.Zero,ref dest,ref size,dc,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
   }finally{if(old!=IntPtr.Zero)SelectObject(dc,old);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);DeleteDC(dc);}
  }
 }
}
