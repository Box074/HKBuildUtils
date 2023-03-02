using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Modding;

namespace $safeprojectname$
{
	public class $safeprojectname$Mod : Mod
	{
		public $safeprojectname$Mod() : base("$safeprojectname$")
		{

		}

		public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

		public override void Initialize()
		{

		}
	}
}
