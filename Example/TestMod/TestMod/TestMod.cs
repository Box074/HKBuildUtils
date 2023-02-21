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

            
        }

        private void BuildTest()
        {
            var r = HeroController.instance.Reflect();
            Log(r.rb2d);
            Log(r.GetCState("A"));
            r.OnDisable();
        }

        private void AfterOrig_FixedUpdate(HKMirror.Hooks.OnHooks.OnHeroController.Delegates.Params_FixedUpdate args)
        {
            LogDebug(ModResources.TEXTFILE1);
        }

        public override void Initialize()
        {
            Log("Initializing");
            HKMirror.Hooks.OnHooks.OnHeroController.AfterOrig.FixedUpdate += AfterOrig_FixedUpdate;
            Log("Initialized");

        }
    }
}
