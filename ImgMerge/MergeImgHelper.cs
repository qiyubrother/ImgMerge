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
        /// 获取图像尺寸信息
        /// 注意：Image.FromFile 会加载整个图像到内存，但我们会立即释放
        /// </summary>
        private static (int width, int height) GetImageDimensions(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"图像文件不存在: {filePath}");
            }
            
            // Image.FromFile 会加载整个图像，但我们只读取尺寸后立即释放
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
            int processedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var (width, height) = GetImageDimensions(file);
                    
                    // 验证尺寸有效性
                    if (width <= 0 || height <= 0)
                    {
                        throw new ArgumentException($"图像文件尺寸无效: {file} (宽度: {width}, 高度: {height})");
                    }
                    
                    dimensions.Add((width, height));
                    maxWidth = Math.Max(maxWidth, width);
                    
                    // 检查是否会溢出
                    if (totalHeight > int.MaxValue - height)
                    {
                        throw new ArgumentException($"图像总高度过大，超出整数范围");
                    }
                    
                    totalHeight += height;
                    processedCount++;
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"处理图像文件时出错: {file}", ex);
                }
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

            // 验证尺寸合理性
            if (maxWidth <= 0 || totalHeight <= 0)
            {
                throw new ArgumentException("图像尺寸无效");
            }

            // 检查是否会超出内存限制（估算：宽度 × 高度 × 4字节/像素）
            long estimatedMemory = (long)maxWidth * totalHeight * 4;
            const long maxMemoryEstimate = 2L * 1024 * 1024 * 1024; // 2GB 限制
            if (estimatedMemory > maxMemoryEstimate)
            {
                throw new OutOfMemoryException($"图像太大，估算内存需求: {estimatedMemory / (1024.0 * 1024.0):F2} MB，超过限制 {maxMemoryEstimate / (1024.0 * 1024.0):F2} MB");
            }

            // 第二步：一次性创建最终Bitmap，避免重复创建中间Bitmap
            Bitmap result;
            try
            {
                result = new Bitmap(maxWidth, totalHeight);
            }
            catch (OutOfMemoryException)
            {
                throw new OutOfMemoryException($"无法创建 {maxWidth}x{totalHeight} 的图像，内存不足");
            }
            
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
                            
                            // 报告进度 (i+2 因为0=计算开始, 1=计算完成, 2+=处理图像)
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
