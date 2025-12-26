using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace ImgMerge
{
    [SupportedOSPlatform("windows")]
    class Program
    {
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

        static void Main(string[] args)
        {
            // 显示欢迎信息和使用提示
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "--help" || args[0] == "-h" || args[0] == "/?")))
            {
                ShowHelp();
                return;
            }

            if (args.Length < 1 || args.Length > 3)
            {
                ShowErrorMessage();
                return;
            }

            var outFileName = "result.png";
            var arr = args[0].Split('=');
            if (arr.Length != 2)
            {
                ShowErrorMessage();
                return;
            }

            ImageFormat imgFormat = ImageFormat.Png;
            List<string> files = new List<string>();
            bool drawSeparatorLines = false; // 默认不绘制分隔线

            // 解析输入参数
            if (!ParseInputFiles(arr[0], arr[1], ref files))
            {
                ShowErrorMessage();
                return;
            }

            // 解析其他参数
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("--o="))
                {
                    // 输出文件参数
                    var arr2 = args[i].Split('=');
                    if (arr2.Length == 2)
                    {
                        outFileName = arr2[1];
                        var fi = new FileInfo(outFileName);
                        var format = GetImageFormatFromExtension(fi.Extension);
                        if (format == null)
                        {
                            Console.WriteLine($"错误: 不支持的输出格式 '{fi.Extension}'");
                            Console.WriteLine($"支持的输出格式: PNG, JPG/JPEG, GIF, BMP, WebP");
                            return;
                        }
                        imgFormat = format;
                    }
                }
                else if (args[i] == "--line" || args[i] == "-l")
                {
                    // 绘制分隔线选项
                    drawSeparatorLines = true;
                }
                else if (args[i] == "--no-line" || args[i] == "-nl")
                {
                    // 不绘制分隔线选项（显式指定）
                    drawSeparatorLines = false;
                }
            }

            if (files.Count < 2)
            {
                Console.WriteLine("错误: 至少需要2张图片才能进行合并");
                Console.WriteLine($"当前找到 {files.Count} 张图片");
                ShowErrorMessage();
                return;
            }

            Console.WriteLine($"找到 {files.Count} 张图片，开始合并...");
            Console.WriteLine("提示: 使用优化的内存管理算法，可以处理更多图像");
            
            // 检查是否有WebP文件
            var webpCount = files.Count(f => Path.GetExtension(f).ToLower() == ".webp");
            if (webpCount > 0)
            {
                Console.WriteLine($"提示: 检测到 {webpCount} 个WebP文件，将自动转换为PNG格式处理");
            }
            
            try
            {
                // 显示内存使用情况
                var memoryBefore = GC.GetTotalMemory(false);
                
                // 使用优化的合并方法，带进度回调和分隔线选项
                // current: 0=开始计算尺寸, 1=计算完成, 2+=已处理的图像数（从1开始）
                // total: 图像总数
                using (var bmp = MergeImgHelper.CombinImage(files, (current, total) =>
                {
                    if (current == 0)
                    {
                        Console.Write("正在计算图像尺寸...");
                    }
                    else if (current == 1)
                    {
                        Console.WriteLine(" 完成");
                        Console.Write("正在合并图像: 0/" + total + " (0%)");
                    }
                    else if (current >= 2 && current <= total + 1)
                    {
                        // current >= 2 表示开始处理图像，current-1 表示已处理的图像数量
                        var processed = current - 1;
                        var percent = total > 0 ? (int)(processed * 100.0 / total) : 0;
                        Console.Write($"\r正在合并图像: {processed}/{total} ({percent}%)");
                    }
                }, drawSeparatorLines))
                {
                    Console.WriteLine(); // 换行
                    
                    var fileInfo = new FileInfo(outFileName);
                    if (!string.IsNullOrEmpty(fileInfo.DirectoryName) && !Directory.Exists(fileInfo.DirectoryName))
                    {
                        Directory.CreateDirectory(fileInfo.DirectoryName);
                    }

                    Console.Write("正在保存图像...");
                    // 保存图像
                    SaveImage(bmp, outFileName, imgFormat);
                    
                    var memoryAfter = GC.GetTotalMemory(false);
                    var memoryUsed = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);
                    
                    Console.WriteLine(" 完成");
                    Console.WriteLine($"合并完成！输出文件: {outFileName}");
                    Console.WriteLine($"内存使用: {memoryUsed:F2} MB");
                }
                
                // 清理WebP转换缓存
                MergeImgHelper.CleanupWebPCache();
            }
            catch (ArgumentException ae)
            {
                Console.WriteLine($"错误: {ae.Message}");
                return;
            }
            catch (OutOfMemoryException)
            {
                Console.WriteLine("错误: 内存不足，无法处理如此大的图像文件");
                return;
            }
            catch (FileNotFoundException fe)
            {
                Console.WriteLine($"错误: 文件未找到 - {fe.FileName}");
                return;
            }
            catch (NotSupportedException nse)
            {
                Console.WriteLine($"错误: {nse.Message}");
                if (nse.InnerException != null)
                {
                    Console.WriteLine($"详细信息: {nse.InnerException.Message}");
                }
                // 清理WebP转换缓存
                MergeImgHelper.CleanupWebPCache();
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine($"错误: {e.Message}");
                if (e.InnerException != null)
                {
                    Console.WriteLine($"内部错误: {e.InnerException.Message}");
                }
                // 清理WebP转换缓存
                MergeImgHelper.CleanupWebPCache();
                return;
            }
        }

        private static bool ParseInputFiles(string argType, string argValue, ref List<string> files)
        {
            if (argType == "--d")
            {
                // 从目录读取所有支持的图片
                if (!Directory.Exists(argValue))
                {
                    Console.WriteLine($"错误: 目录不存在 - {argValue}");
                    return false;
                }
                var fs = Directory.GetFiles(argValue);
                foreach (var f in fs)
                {
                    var fi = new FileInfo(f);
                    var ext = fi.Extension.ToLower();
                    if (IsSupportedExtension(ext))
                    {
                        files.Add(f);
                    }
                }
                return true;
            }
            else if (argType == "--f")
            {
                // 从文件列表读取（分号分隔）
                var fs = argValue.Split(';');
                foreach (var f in fs)
                {
                    var trimmed = f.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    var fi = new FileInfo(trimmed);
                    if (fi.Exists)
                    {
                        files.Add(trimmed);
                    }
                    else
                    {
                        Console.WriteLine($"警告: 文件不存在，已跳过 - {trimmed}");
                    }
                }
                return true;
            }
            else if (argType == "--s")
            {
                // 使用通配符匹配文件
                try
                {
                    var lastSlashIndex = argValue.LastIndexOf('\\');
                    if (lastSlashIndex < 0)
                    {
                        Console.WriteLine("错误: 通配符路径格式不正确");
                        return false;
                    }
                    var directory = argValue.Substring(0, lastSlashIndex);
                    var pattern = argValue.Substring(lastSlashIndex + 1);
                    if (!Directory.Exists(directory))
                    {
                        Console.WriteLine($"错误: 目录不存在 - {directory}");
                        return false;
                    }
                    var fs = Directory.GetFiles(directory, pattern);
                    foreach (var f in fs)
                    {
                        var fi = new FileInfo(f);
                        if (fi.Exists)
                        {
                            files.Add(f);
                        }
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        private static bool IsSupportedExtension(string ext)
        {
            return Array.Exists(SupportedExtensions, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        private static ImageFormat? GetImageFormatFromExtension(string ext)
        {
            ext = ext.ToLower();
            switch (ext)
            {
                case ".png":
                    return ImageFormat.Png;
                case ".jpg":
                case ".jpeg":
                    return ImageFormat.Jpeg;
                case ".gif":
                    return ImageFormat.Gif;
                case ".bmp":
                    return ImageFormat.Bmp;
                case ".webp":
                    // WebP格式，但由于System.Drawing不直接支持，将使用PNG格式保存
                    // 实际保存时会特殊处理
                    return ImageFormat.Png;
                default:
                    return null;
            }
        }

        private static void SaveImage(Bitmap bmp, string fileName, ImageFormat format)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            
            // WebP格式特殊处理
            if (ext == ".webp")
            {
                // 由于System.Drawing不直接支持WebP，保存为PNG格式
                // 用户需要其他工具转换，或者可以添加第三方库支持
                var pngFileName = Path.ChangeExtension(fileName, ".png");
                Console.WriteLine($"提示: System.Drawing不支持直接保存WebP格式，已保存为PNG格式: {pngFileName}");
                Console.WriteLine($"提示: 如需WebP格式，请使用其他工具将PNG转换为WebP");
                bmp.Save(pngFileName, ImageFormat.Png);
            }
            else
            {
                bmp.Save(fileName, format);
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("    图片合并工具 (ImgMerge)");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine("功能说明:");
            Console.WriteLine("  将多张图片垂直合并为一张图片");
            Console.WriteLine();
            Console.WriteLine("支持的输入格式:");
            Console.WriteLine("  PNG, JPG/JPEG, GIF, BMP, WebP");
            Console.WriteLine();
            Console.WriteLine("支持的输出格式:");
            Console.WriteLine("  PNG, JPG/JPEG, GIF, BMP, WebP (WebP将保存为PNG)");
            Console.WriteLine();
            Console.WriteLine("使用方法:");
            Console.WriteLine();
            Console.WriteLine("  1. 合并目录中的所有图片:");
            Console.WriteLine("     ImgMerge.exe --d=目录路径 [--o=输出文件]");
            Console.WriteLine("     示例: ImgMerge.exe --d=.\\source --o=result.png");
            Console.WriteLine();
            Console.WriteLine("  2. 合并指定的多个文件:");
            Console.WriteLine("     ImgMerge.exe --f=文件1;文件2;文件3 [--o=输出文件]");
            Console.WriteLine("     示例: ImgMerge.exe --f=.\\img1.jpg;.\\img2.png --o=result.jpg");
            Console.WriteLine();
            Console.WriteLine("  3. 使用通配符匹配文件:");
            Console.WriteLine("     ImgMerge.exe --s=目录\\*.jpg [--o=输出文件]");
            Console.WriteLine("     示例: ImgMerge.exe --s=.\\source\\*.jpg --o=result.jpg");
            Console.WriteLine();
            Console.WriteLine("参数说明:");
            Console.WriteLine("  --d       : 从指定目录读取所有支持的图片文件");
            Console.WriteLine("  --f       : 指定要合并的文件列表（用分号分隔）");
            Console.WriteLine("  --s       : 使用通配符匹配文件");
            Console.WriteLine("  --o       : 指定输出文件名（可选，默认为result.png）");
            Console.WriteLine("  --line    : 在图像之间绘制分隔线（可选，默认不绘制）");
            Console.WriteLine("  --no-line : 不绘制分隔线（可选，默认行为）");
            Console.WriteLine("  --help    : 显示此帮助信息");
            Console.WriteLine();
            Console.WriteLine("注意事项:");
            Console.WriteLine("  - 至少需要2张图片才能进行合并");
            Console.WriteLine("  - 图片将按文件名顺序垂直合并");
            Console.WriteLine("  - 如果输出目录不存在，将自动创建");
            Console.WriteLine("  - WebP格式的输出将自动转换为PNG格式");
            Console.WriteLine("  - 默认不绘制分隔线，使用 --line 选项可启用分隔线");
            Console.WriteLine();
        }

        private static void ShowErrorMessage()
        {
            Console.WriteLine("错误: 参数不正确");
            Console.WriteLine();
            Console.WriteLine("快速示例:");
            Console.WriteLine("  ImgMerge.exe --d=.\\source --o=result.png");
            Console.WriteLine("  ImgMerge.exe --f=img1.jpg;img2.jpg --o=result.jpg");
            Console.WriteLine("  ImgMerge.exe --s=.\\source\\*.jpg --o=result.jpg --line");
            Console.WriteLine();
            Console.WriteLine("输入 'ImgMerge.exe --help' 查看详细使用说明");
        }
    }
}
