using Modding;
using System;

namespace Example
{
    public class ExampleMod : Mod
    {
        private static ExampleMod? _instance;

        internal static ExampleMod Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException($"An instance of {nameof(ExampleMod)} was never constructed");
                }
                return _instance;
            }
        }

        public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

        public ExampleMod() : base("Example")
        {
            _instance = this;
        }

        public override void Initialize()
        {
            Log("Initializing");

            // put additional initialization logic here

            Log("Initialized");
        }
    }
}
