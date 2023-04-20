using FSMProxy;
using HKMirror.Reflection;
using Modding;
using MonoMod.RuntimeDetour.HookGen;
using System;
using System.Runtime.CompilerServices;
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
                Mod = this.Reflect(),
                Name = "Test This 1"
            });


            On.UnityEngine.MonoBehaviour.StartCoroutine_IEnumerator += MonoBehaviour_StartCoroutine_IEnumerator;
            
        }

        private bool ModLoader_TryAddModInstance(On.Modding.ModLoader.orig_TryAddModInstance orig, Type ty, object mod)
        {
            Log("TryAddModInstance: " + ty.FullName);
            return orig(ty, mod);
        }

        private void BuildTest()
        {
            var hero = (HeroControllerR)HeroController.instance.Reflect();
            Log("H: " + hero.rb2d.velocity.ToString());
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
