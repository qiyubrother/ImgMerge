using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace ImgMerge
{
    [SupportedOSPlatform("windows")]
    class MergeImgHelper
    {
        // 临时文件缓存，用于存储转换后的WebP文件
        private static readonly Dictionary<string, string> _webpCache = new Dictionary<string, string>();
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// 将WebP文件转换为PNG临时文件
        /// </summary>
        private static string ConvertWebPToPng(string webpPath)
        {
            lock (_cacheLock)
            {
                // 检查缓存
                if (_webpCache.TryGetValue(webpPath, out var cachedPath) && File.Exists(cachedPath))
                {
                    return cachedPath;
                }

                // 创建临时文件
                var tempDir = Path.Combine(Path.GetTempPath(), "ImgMerge_WebP");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var tempFile = Path.Combine(tempDir, $"{Guid.NewGuid()}.png");

                try
                {
                    // 使用ImageSharp读取WebP并保存为PNG
                    using (var image = SixLabors.ImageSharp.Image.Load(webpPath))
                    {
                        image.SaveAsPng(tempFile);
                    }

                    // 缓存转换后的文件路径
                    _webpCache[webpPath] = tempFile;
                    return tempFile;
                }
                catch (Exception ex)
                {
                    // 如果转换失败，清理临时文件
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }
                    throw new Exception($"无法转换WebP文件: {webpPath}", ex);
                }
            }
        }

        /// <summary>
        /// 清理WebP转换缓存
        /// </summary>
        public static void CleanupWebPCache()
        {
            lock (_cacheLock)
            {
                foreach (var tempFile in _webpCache.Values)
                {
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch { }
                }
                _webpCache.Clear();

                // 尝试删除临时目录
                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "ImgMerge_WebP");
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 获取图像尺寸信息
        /// 如果是WebP文件，先转换为PNG再读取
        /// </summary>
        private static (int width, int height) GetImageDimensions(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"图像文件不存在: {filePath}");
            }
            
            var ext = Path.GetExtension(filePath).ToLower();
            string actualPath = filePath;

            // 如果是WebP文件，先转换为PNG
            if (ext == ".webp")
            {
                try
                {
                    actualPath = ConvertWebPToPng(filePath);
                }
                catch (Exception ex)
                {
                    throw new Exception($"无法处理WebP文件: {filePath}。转换失败: {ex.Message}", ex);
                }
            }
            
            try
            {
                // Image.FromFile 会加载整个图像，但我们只读取尺寸后立即释放
                using (var img = System.Drawing.Image.FromFile(actualPath))
                {
                    return (img.Width, img.Height);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"无法读取图像文件: {filePath}。可能文件格式不支持或文件已损坏。", ex);
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
                    Console.WriteLine(ex.Message);
                    throw new ArgumentException($"处理图像文件时出错: {file}", ex);
                }
            }

            return (maxWidth, totalHeight, dimensions);
        }

        /// <summary>
        /// 优化的图像合并方法 - 流式处理，节省内存
        /// </summary>
        /// <param name="files">要合并的图像文件列表</param>
        /// <param name="progressCallback">进度回调函数</param>
        /// <param name="drawSeparatorLines">是否绘制分隔线（默认false）</param>
        public static Bitmap CombinImage(IEnumerable<string> files, Action<int, int>? progressCallback = null, bool drawSeparatorLines = false)
        {
            var fileList = files.ToList();
            
            if (fileList.Count == 0)
            {
                throw new ArgumentException("没有可合并的图片");
            }

            if (fileList.Count == 1)
            {
                // 只有一张图片，直接返回
                var singleFile = fileList[0];
                var ext = Path.GetExtension(singleFile).ToLower();
                string actualPath = singleFile;
                
                // 如果是WebP文件，先转换
                if (ext == ".webp")
                {
                    actualPath = ConvertWebPToPng(singleFile);
                }
                
                return new Bitmap(System.Drawing.Image.FromFile(actualPath));
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
                    g.Clear(System.Drawing.Color.White);
                    
                    // 准备画笔（仅在需要绘制分隔线时创建）
                    Pen? pen = null;
                    if (drawSeparatorLines)
                    {
                        var brush = new SolidBrush(System.Drawing.Color.LightGray);
                        pen = new Pen(brush, 1.0f);
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        pen.DashPattern = new float[] { 5, 5 };
                    }
                    
                    try
                    {
                        int currentY = 0;
                        
                        // 第三步：流式处理 - 一次只加载一张图像
                        for (int i = 0; i < fileList.Count; i++)
                        {
                            var filePath = fileList[i];
                            var ext = Path.GetExtension(filePath).ToLower();
                            
                            try
                            {
                                // 如果是WebP文件，使用转换后的PNG文件
                                string actualPath = filePath;
                                if (ext == ".webp")
                                {
                                    actualPath = ConvertWebPToPng(filePath);
                                }
                                
                                using (var img = System.Drawing.Image.FromFile(actualPath))
                                {
                                    var (width, height) = dimensions[i];
                                    
                                    // 绘制当前图像
                                    g.DrawImage(img, 0, currentY, width, height);
                                    
                                    // 如果需要绘制分隔线且不是最后一张，绘制分隔线
                                    if (drawSeparatorLines && pen != null && i < fileList.Count - 1)
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
                            catch (OutOfMemoryException ex)
                            {
                                if (ext == ".webp")
                                {
                                    throw new NotSupportedException($"处理WebP文件时出错: {filePath}。System.Drawing对WebP的支持有限，建议先将WebP转换为PNG或JPEG格式。", ex);
                                }
                                throw;
                            }
                            catch (ArgumentException ex)
                            {
                                if (ext == ".webp")
                                {
                                    throw new NotSupportedException($"处理WebP文件时出错: {filePath}。System.Drawing对WebP的支持有限，建议先将WebP转换为PNG或JPEG格式。", ex);
                                }
                                throw new ArgumentException($"处理图像文件时出错: {filePath}。可能文件格式不支持或文件已损坏。", ex);
                            }
                            catch (Exception ex)
                            {
                                if (ext == ".webp")
                                {
                                    throw new NotSupportedException($"处理WebP文件时出错: {filePath}。System.Drawing对WebP的支持有限，建议先将WebP转换为PNG或JPEG格式。", ex);
                                }
                                throw new Exception($"处理图像文件时出错: {filePath}", ex);
                            }
                        }
                    }
                    finally
                    {
                        // 释放画笔资源
                        if (pen != null)
                        {
                            pen.Dispose();
                            if (pen.Brush != null)
                            {
                                pen.Brush.Dispose();
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
        public static Bitmap CombinImage(System.Drawing.Image img1, System.Drawing.Image img2, int xDeviation = 0, int yDeviation = 0)
        {
            Bitmap bmp = new Bitmap(img1.Width, img1.Height + img2.Height);
            
            using (Graphics g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(System.Drawing.Color.LightGray))
            using (var p = new Pen(brush, 1.0f))
            {
                p.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                p.DashPattern = new float[] { 5, 5 };
                
                g.Clear(System.Drawing.Color.White);
                g.DrawImage(img1, 0, 0, img1.Width, img1.Height);
                g.DrawImage(img2, 0, img1.Height, img2.Width, img2.Height);
                g.DrawLine(p, 0, img1.Height, img1.Width, img1.Height);
            }
            
            return bmp;
        }
    }
}
