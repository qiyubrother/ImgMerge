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
        /// 获取图像尺寸信息（不加载完整图像到内存）
        /// </summary>
        private static (int width, int height) GetImageDimensions(string filePath)
        {
            using (var img = Image.FromFile(filePath))
            {
                return (img.Width, img.Height);
            }
        }

        /// <summary>
        /// 预先计算所有图像的尺寸和总高度（内存优化版本）
        /// </summary>
        private static (int maxWidth, int totalHeight, List<(int width, int height)> dimensions) CalculateDimensions(IEnumerable<string> files)
        {
            var dimensions = new List<(int width, int height)>();
            int maxWidth = 0;
            int totalHeight = 0;

            foreach (var file in files)
            {
                var (width, height) = GetImageDimensions(file);
                dimensions.Add((width, height));
                maxWidth = Math.Max(maxWidth, width);
                totalHeight += height;
            }

            return (maxWidth, totalHeight, dimensions);
        }

        /// <summary>
        /// 优化的图像合并方法 - 流式处理，节省内存
        /// </summary>
        public static Bitmap CombinImage(IEnumerable<string> files, Action<int, int>? progressCallback = null)
        {
            var fileList = files.ToList();
            
            if (fileList.Count == 0)
            {
                throw new ArgumentException("没有可合并的图片");
            }

            if (fileList.Count == 1)
            {
                // 只有一张图片，直接返回
                return new Bitmap(Image.FromFile(fileList[0]));
            }

            // 第一步：预先计算所有图像的尺寸（只加载元数据，不加载完整图像）
            progressCallback?.Invoke(0, fileList.Count);
            var (maxWidth, totalHeight, dimensions) = CalculateDimensions(fileList);
            progressCallback?.Invoke(1, fileList.Count);

            // 第二步：一次性创建最终Bitmap，避免重复创建中间Bitmap
            Bitmap result = new Bitmap(maxWidth, totalHeight);
            
            try
            {
                // 配置Graphics以获得最佳性能
                using (Graphics g = Graphics.FromImage(result))
                {
                    // 优化Graphics设置以提高性能
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    
                    // 清除背景
                    g.Clear(Color.White);
                    
                    // 准备画笔和画笔（复用，避免重复创建）
                    using (var brush = new SolidBrush(Color.LightGray))
                    using (var pen = new Pen(brush, 1.0f))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        pen.DashPattern = new float[] { 5, 5 };
                        
                        int currentY = 0;
                        
                        // 第三步：流式处理 - 一次只加载一张图像
                        for (int i = 0; i < fileList.Count; i++)
                        {
                            using (var img = Image.FromFile(fileList[i]))
                            {
                                var (width, height) = dimensions[i];
                                
                                // 绘制当前图像
                                g.DrawImage(img, 0, currentY, width, height);
                                
                                // 如果不是最后一张，绘制分隔线
                                if (i < fileList.Count - 1)
                                {
                                    g.DrawLine(pen, 0, currentY + height, maxWidth, currentY + height);
                                }
                                
                                currentY += height;
                            }
                            
                            // 报告进度
                            progressCallback?.Invoke(i + 2, fileList.Count);
                            
                            // 强制垃圾回收以释放已处理的图像内存
                            if (i % 10 == 0 && i > 0)
                            {
                                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                            }
                        }
                    }
                }
                
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 旧版本的合并方法（保留以兼容，但不推荐使用）
        /// </summary>
        [Obsolete("此方法会一次性加载所有图像到内存，建议使用 CombinImage(IEnumerable<string>, Action<int, int>) 方法")]
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
    }
}
