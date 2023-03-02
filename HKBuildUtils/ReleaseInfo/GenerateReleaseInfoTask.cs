using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace HKBuildUtils.ReleaseInfo
{
    public class GenerateReleaseInfoTask : Task
    {
        [Required]
        public string Output { get; set; } = "";
        [Required]
        public string ZipSHA { get; set; } = "";
        [Required]
        public string ProjectDir { get; set; } = "";
        private string gitDir = "";
        public override bool Execute()
        {
            using var writer = new StreamWriter(Output, false, Encoding.UTF8);

            writer.WriteLine("SHA256: " + ZipSHA);

            string curDir = Path.GetFullPath(ProjectDir);
            string rootDir = Path.GetPathRoot(curDir);
            while (curDir != rootDir
                && (curDir + "\\") != rootDir
                && (curDir + "/") != rootDir)
            {
                var gdir = Path.Combine(curDir, ".git");
                if (Directory.Exists(gdir))
                {
                    gitDir = curDir;
                    break;
                }
                curDir = Path.GetDirectoryName(curDir);
            }
            if (!string.IsNullOrEmpty(gitDir))
            {
                var tags = InvokeGit("tag -l").Split('\n');
                if (tags.Length > 1)
                {
                    var curTag = tags[tags.Length - 1];
                    var prevTag = tags[tags.Length - 2];
                    var curTagSHA = InvokeGit("show --no-patch --pretty=tformat:%H " + curTag);
                    var prevTagSHA = InvokeGit("show --no-patch --pretty=tformat:%H " + prevTag);

                    writer.WriteLine($"Compare: {prevTagSHA}...{curTagSHA}");
                }
                
            }
            
            return true;
        }

        private string InvokeGit(string args)
        {
            var ph = Process.Start(new ProcessStartInfo()
            { 
                FileName = "git",
                Arguments = args,
                WorkingDirectory = gitDir,
                RedirectStandardOutput = true
                
            });
            ph.WaitForExit();
            return ph.StandardOutput.ReadToEnd().Trim();
        }
    }
}
