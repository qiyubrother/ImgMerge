using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;

namespace ImgMerge
{
    [SupportedOSPlatform("windows")]
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
            
            using (Graphics g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(Color.LightGray))
            using (var p = new Pen(brush, 1.0f))
            {
                p.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                p.DashPattern = new float[] { 5, 5 };
                
                g.Clear(Color.White);
                g.DrawImage(img1, 0, 0, img1.Width, img1.Height);
                g.DrawImage(img2, 0, img1.Height, img2.Width, img2.Height);
                g.DrawLine(p, 0, img1.Height, img1.Width, img1.Height);
            }
            
            return bmp;
        }

        public static Bitmap CombinImage(Image[] imgs)
        {
            if (imgs == null || imgs.Length == 0)
            {
                throw new ArgumentException("图片数组不能为空");
            }
            
            Bitmap r = new Bitmap(imgs[0]);
            try
            {
                for (var i = 1; i < imgs.Length; i++)
                {
                    var temp = CombinImage(r, imgs[i]);
                    r.Dispose();
                    r = temp;
                }
                return r;
            }
            catch
            {
                r.Dispose();
                throw;
            }
        }

        public static Bitmap CombinImage(IEnumerable<string> files)
        {
            List<Image> imgs = new List<Image>();
            try
            {
                foreach (var f in files)
                {
                    imgs.Add(Image.FromFile(f));
                }
                
                if (imgs.Count == 0)
                {
                    throw new ArgumentException("没有可合并的图片");
                }

                Bitmap r = new Bitmap(imgs[0]);
                try
                {
                    for (var i = 1; i < imgs.Count; i++)
                    {
                        var temp = CombinImage(r, imgs[i]);
                        r.Dispose();
                        r = temp;
                    }
                    return r;
                }
                catch
                {
                    r.Dispose();
                    throw;
                }
            }
            finally
            {
                // 释放所有加载的图片资源
                foreach (var img in imgs)
                {
                    img.Dispose();
                }
            }
        }
    }
}
