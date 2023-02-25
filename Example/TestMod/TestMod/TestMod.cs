using FSMProxy;
using HKMirror.Reflection;
using Modding;
using System;
using System.Text;

namespace TestMod
{
    public class TestModMod : Mod
    {
        private static TestModMod? _instance;

        internal static TestModMod Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException($"An instance of {nameof(TestModMod)} was never constructed");
                }
                return _instance;
            }
        }

        public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

        public TestModMod() : base("TestMod")
        {
            _instance = this;

            ModLoaderR.TryAddModInstance(typeof(TestModMod), new()
            {
                Enabled = true,
                Mod = this,
                Name = "Test This 1"
            });
        }

        private void BuildTest()
        {
            var hero = (HeroControllerR)HeroController.instance.Reflect();
            Log("H: " + hero.col2d.bounds.ToString());
        }

        private void AfterOrig_FixedUpdate(HKMirror.Hooks.OnHooks.OnHeroController.Delegates.Params_FixedUpdate args)
        {
            BuildTest();
        }

        public override void Initialize()
        {
            Log("Initializing");
            HKMirror.Hooks.OnHooks.OnHeroController.AfterOrig.FixedUpdate += AfterOrig_FixedUpdate;
            Log("Initialized");

        }
    }
}
