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
            
            On.UnityEngine.MonoBehaviour.StartCoroutine_IEnumerator += MonoBehaviour_StartCoroutine_IEnumerator;

            On.Modding.ModLoader.TryAddModInstance += ModLoader_TryAddModInstance;
        }

        private bool ModLoader_TryAddModInstance(On.Modding.ModLoader.orig_TryAddModInstance orig, Type ty, object mod)
        {
            Log("TryAddModInstance: " + ty.FullName);
            return orig(ty, mod);
        }

        private void BuildTest()
        {
            var hero = (HeroControllerR)HeroController.instance.Reflect();
            Log("H: " + hero.col2d.bounds.ToString());

            
           
        }

        private UnityEngine.Coroutine MonoBehaviour_StartCoroutine_IEnumerator(
            On.UnityEngine.MonoBehaviour.orig_StartCoroutine_IEnumerator orig, UnityEngine.MonoBehaviour self, 
            System.Collections.IEnumerator routine)
        {
            Log("Start new coroutine: " + routine.GetType().FullName);
            return orig(self, routine);
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
