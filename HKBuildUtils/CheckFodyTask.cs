using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace HKBuildUtils
{
    public class CheckFodyTask : Task
    {
        public string AllPackages { get; set; } = "";
        public override bool Execute()
        {
            if (AllPackages?.Contains(";Fody;") ?? false) return true;
            Log.LogError("The Fody package needs to be referenced for the next operation.");
            return false;
        }
    }
}
