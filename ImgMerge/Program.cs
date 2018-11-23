using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;

namespace ImgMerge
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1 && args.Length != 2)
            {
                ShowErrorMessage();
                return;
            }
            var outFileName = "result.png";
            var arr = args[0].Split('=');
            System.Drawing.Imaging.ImageFormat imgFormat = System.Drawing.Imaging.ImageFormat.Png;
            List<string> files = new List<string>();
            if (args.Length == 1)
            {
                #region 解析输入参数
                if (arr[0] == "--d")
                {
                    var fs = Directory.GetFiles(arr[1]);
                    foreach (var f in fs)
                    {
                        var fi = new FileInfo(f);
                        var ext = fi.Extension.ToLower();
                        if (ext == ".png"
                            || ext == ".jpg"
                            || ext == ".gif"
                            || ext == ".bmp"
                            )
                        {
                            files.Add(f);
                        }
                    }
                }
                else if (arr[0] == "--f")
                {
                    var fs = arr[1].Split(';');
                    foreach(var f in fs)
                    {
                        var fi = new FileInfo(f);
                        if (fi.Exists)
                        {
                            files.Add(f);
                        }
                    }
                }
                else if (arr[0] == "--s")
                {
                    var fs = Directory.GetFiles(arr[1].Substring(0, arr[1].LastIndexOf('\\')), arr[1].Substring(arr[1].LastIndexOf('\\') + 1));
                    //Array.Sort(fs);
                    foreach (var f in fs)
                    {
                        var fi = new FileInfo(f);
                        if (fi.Exists)
                        {
                            files.Add(f);
                        }
                    }
                }
                else
                {
                    ShowErrorMessage();
                    return;
                }
                #endregion
            }
            if (args.Length == 2)
            {
                #region 解析输入参数
                if (arr[0] == "--d")
                {
                    var fs = Directory.GetFiles(arr[1]);
                    foreach (var f in fs)
                    {
                        var fi = new FileInfo(f);
                        var ext = fi.Extension.ToLower();
                        if (ext == ".png"
                            || ext == ".jpg"
                            || ext == ".gif"
                            || ext == ".bmp"
                            )
                        {
                            files.Add(f);
                        }
                    }
                }
                else if (arr[0] == "--f")
                {
                    var fs = arr[1].Split(';');
                    foreach (var f in fs)
                    {
                        var fi = new FileInfo(f);
                        if (fi.Exists)
                        {
                            files.Add(f);
                        }
                    }
                }
                else if (arr[0] == "--s")
                {
                    var fs = Directory.GetFiles(arr[1].Substring(0, arr[1].LastIndexOf('\\')), arr[1].Substring(arr[1].LastIndexOf('\\') + 1));
                    //Array.Sort(fs);
                    foreach (var f in fs)
                    {
                        var fi = new FileInfo(f);
                        if (fi.Exists)
                        {
                            files.Add(f);
                        }
                    }
                }
                else
                {
                    ShowErrorMessage();
                    return;
                }
                #endregion
                #region 解析输出参数
                var arr2 = args[1].Split('=');
                if (arr2[0] == "--o")
                {
                    outFileName = arr2[1];
                    var fi = new FileInfo(outFileName);
                    var ext = fi.Extension.ToLower();
                    if (ext == ".png")
                    {
                        imgFormat = System.Drawing.Imaging.ImageFormat.Png;
                    }
                    else if (ext == ".jpg")
                    {
                        imgFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                    }
                    else if (ext == ".gif")
                    {
                        imgFormat = System.Drawing.Imaging.ImageFormat.Gif;
                    }
                    else if (ext == ".bmp")
                    {
                        imgFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                    }
                }
                else
                {
                    ShowErrorMessage();
                    return;
                }
                #endregion
            }
            if (files.Count < 2)
            {
                ShowErrorMessage();
                return;
            }
            //foreach (var f in files) Console.WriteLine(f);
            try
            {
                GC.Collect();
                var bmp = MergeImgHelper.CombinImage(files);
                var fileInfo = new FileInfo(outFileName);
                if (!Directory.Exists(fileInfo.DirectoryName))
                {
                    Directory.CreateDirectory(fileInfo.DirectoryName);
                }
                bmp.Save(outFileName, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch(ArgumentException ae)
            {
                Console.WriteLine($"{ae.Message}::Memory error." );
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
        }

        private static void ShowErrorMessage()
        {
            Console.WriteLine(@"imgMerge.exe --d=.\source --o=result.png");
            Console.WriteLine(@"imgMerge.exe --d=.\source");
            Console.WriteLine(@"imgMerge.exe --f=.\source\01.jpg;.\source\02.jpg --o=result.jpg");
            Console.WriteLine(@"imgMerge.exe --f=.\source\01.jpg;.\asource\02.jpg");
            Console.WriteLine(@"imgMerge.exe --s=.\source\*.jpg --o=result.jpg");
            Console.WriteLine(@"imgMerge.exe --s=.\source\*.jpg");
        }
    }
}
