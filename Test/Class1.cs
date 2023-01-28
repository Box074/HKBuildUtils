using HKMirror.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class Class1
    {
        public static void Test()
        {
            var a = ModResources.TEST2;
            HeroController.instance.Reflect().hitsSinceShielded = 1;

            HKMirror.Hooks.OnHooks.OnHeroController.AfterOrig.Awake += (self) => {
                throw new Exception();
            };
            HKMirror.Hooks.OnHooks.OnHeroController.BeforeOrig.AddMPCharge += (self) => {
                throw new Exception();
            };
            HKMirror.PlayerDataAccess.atBench = true;
            HKMirror.PlayerDataAccess.bankerTheft = 0;
        }
    }
}
