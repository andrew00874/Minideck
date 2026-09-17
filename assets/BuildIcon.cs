using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Svg;
class BuildIcon {
 static void Main(string[] args) {
  SvgDocument doc=SvgDocument.Open<SvgDocument>(args[0]);
  int[] sizes={16,20,24,32,48,64,128,256};byte[][] png=new byte[sizes.Length][];
  using(Bitmap source=doc.Draw(1024,1024)) {
   for(int i=0;i<sizes.Length;i++)using(Bitmap b=new Bitmap(sizes[i],sizes[i])) {
    using(Graphics g=Graphics.FromImage(b)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(source,new Rectangle(0,0,b.Width,b.Height));}
    using(MemoryStream s=new MemoryStream()){b.Save(s,ImageFormat.Png);png[i]=s.ToArray();}
    if(sizes[i]==256)b.Save(Path.Combine(args[1],"minideck.png"),ImageFormat.Png);
   }
  }
  using(BinaryWriter w=new BinaryWriter(File.Create(Path.Combine(args[1],"minideck.ico")))) {
   w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+sizes.Length*16;
   for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(png[i].Length);w.Write(offset);offset+=png[i].Length;}
   foreach(byte[] image in png)w.Write(image);
  }
 }
}
