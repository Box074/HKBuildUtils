using HKMirror;
using HKMirror.Hooks.ILHooks;
using HKMirror.Hooks.OnHooks;
using Modding;
using System;

namespace ExampleHKMirrorMerge
{
    public class ExampleHKMirrorMergeMod : Mod
    {
        private static ExampleHKMirrorMergeMod _instance;

        internal static ExampleHKMirrorMergeMod Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException($"An instance of {nameof(ExampleHKMirrorMergeMod)} was never constructed");
                }
                return _instance;
            }
        }

        public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

        public ExampleHKMirrorMergeMod() : base("ExampleHKMirrorMerge")
        {
            _instance = this;

            
        }

        private void BeforeOrig_FixedUpdate(OnHeroController.Delegates.Params_FixedUpdate args)
        {
            Log("Geo: " + PlayerDataAccess.geo);
        }

        public override void Initialize()
        {
            Log("Initializing");

            // put additional initialization logic here
            OnHeroController.BeforeOrig.FixedUpdate += BeforeOrig_FixedUpdate;
            OnHeroController.AfterOrig.FixedUpdate += AfterOrig_FixedUpdate;

            ILHeroController.FixedUpdate += ILHeroController_FixedUpdate;
            Log("Initialized");
        }

        private void ILHeroController_FixedUpdate(MonoMod.Cil.ILContext il)
        {
            
        }

        private void AfterOrig_FixedUpdate(OnHeroController.Delegates.Params_FixedUpdate args)
        {
            Log("Health: " + PlayerDataAccess.health);
        }
    }
}
