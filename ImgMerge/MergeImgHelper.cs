using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;

namespace ImgMerge
{
    class MergeImgHelper
    {
        /// <summary>
        /// 合并图片
        /// </summary>
        /// <param name="img1"></param>
        /// <param name="img2"></param>
        /// <param name="xDeviation"></param>
        /// <param name="yDeviation"></param>
        /// <returns></returns>
        public static Bitmap CombinImage(Image img1, Image img2, int xDeviation = 0, int yDeviation = 0)
        {
            Bitmap bmp = new Bitmap(img1.Width, img1.Height + img2.Height);
            var p = new Pen(new SolidBrush(Color.LightGray), 1.0f);
            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
            p.DashPattern = new float[] { 5, 5 };

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.DrawImage(img1, 0, 0, img1.Width, img1.Height);
                g.DrawImage(img2, 0, img1.Height, img2.Width, img2.Height);
                g.DrawLine(p, 0, img1.Height, img1.Width, img1.Height);
            }
            GC.Collect();
            return bmp;
        }

        public static Bitmap CombinImage(Image[] imgs)
        {
            Bitmap r = new Bitmap(imgs[0]);
            for (var i = 1; i < imgs.Length; i++)
            {
                r = CombinImage(r, imgs[i]);
            }
            return r;
        }

        public static Bitmap CombinImage(IEnumerable<string> files)
        {
            List<Image> imgs = new List<Image>();
            foreach (var f in files)
            {
                imgs.Add(Image.FromFile(f));
            }
            Bitmap r = new Bitmap(imgs[0]);
            for (var i = 1; i < imgs.Count; i++)
            {
                r = CombinImage(r, imgs[i]);
            }
            return r;
        }
    }
}
